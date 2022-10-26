﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Coyote.Coverage;
using Microsoft.Coyote.Logging;
using Microsoft.Coyote.Runtime;

namespace Microsoft.Coyote.Specifications
{
    /// <summary>
    /// Abstract class representing a specification monitor.
    /// </summary>
    /// <remarks>
    /// See <see href="/coyote/concepts/specifications">Specifications Overview</see>
    /// for more information.
    /// </remarks>
    public abstract class Monitor
    {
        /// <summary>
        /// Map from monitor types to a set of all possible states types.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, HashSet<Type>> StateTypeMap =
            new ConcurrentDictionary<Type, HashSet<Type>>();

        /// <summary>
        /// Map from monitor types to a set of all available states.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, HashSet<State>> StateMap =
            new ConcurrentDictionary<Type, HashSet<State>>();

        /// <summary>
        /// Map from monitor types to a set of all available actions.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Dictionary<string, MethodInfo>> MonitorActionMap =
            new ConcurrentDictionary<Type, Dictionary<string, MethodInfo>>();

        /// <summary>
        /// A set of lockable objects used to protect static initialization of the ActionCache while
        /// also enabling multithreaded initialization of different Actor types.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, object> ActionCacheLocks =
            new ConcurrentDictionary<Type, object>();

        /// <summary>
        /// A cached array that contains a single event type.
        /// </summary>
        private static readonly Type[] SingleEventTypeArray = new Type[] { typeof(Event) };

        /// <summary>
        /// The configuration used by the runtime.
        /// </summary>
        private Configuration Configuration;

        /// <summary>
        /// The runtime that is managing this monitor.
        /// </summary>
        private CoyoteRuntime Runtime;

        /// <summary>
        /// The active monitor state.
        /// </summary>
        private State ActiveState;

        /// <summary>
        /// Dictionary containing all the current action bindings.
        /// </summary>
        private Dictionary<Type, EventHandlerDeclaration> EventHandlers;

        /// <summary>
        /// Map from action names to cached action delegates.
        /// </summary>
        private readonly Dictionary<string, CachedDelegate> ActionMap;

        /// <summary>
        /// Set of currently ignored event types.
        /// </summary>
        private HashSet<Type> IgnoredEvents;

        /// <summary>
        /// A counter that increases in each step of the execution, as long as the monitor
        /// remains in a hot state. If the temperature reaches the specified limit, then
        /// a potential liveness bug has been found.
        /// </summary>
        private int LivenessTemperature;

        /// <summary>
        /// Gets the name of this monitor.
        /// </summary>
        internal string Name => this.GetType().FullName;

        /// <summary>
        /// The logger installed to the runtime.
        /// </summary>
        /// <remarks>
        /// See <see href="/coyote/concepts/actors/logging">Logging</see> for more information.
        /// </remarks>
        protected ILogger Logger => this.Runtime.LogWriter;

        /// <summary>
        /// Gets the current state.
        /// </summary>
        protected internal Type CurrentState
        {
            get => this.ActiveState?.GetType();
        }

        /// <summary>
        /// Gets the current state name.
        /// </summary>
        internal string CurrentStateName
        {
            get => NameResolver.GetQualifiedStateName(this.CurrentState);
        }

        /// <summary>
        /// Gets the current state name with temperature.
        /// </summary>
        private string CurrentStateNameWithTemperature
        {
            get
            {
                return this.CurrentStateName +
                    (this.IsInHotState() ? "[hot]" :
                    this.IsInColdState() ? "[cold]" :
                    string.Empty);
            }
        }

        /// <summary>
        /// User-defined hashed state of the monitor. Override to improve the
        /// accuracy of stateful techniques during testing.
        /// </summary>
        protected virtual int HashedState => 0;

        /// <summary>
        /// A pending transition object that has not been returned from ExecuteAction yet.
        /// </summary>
        private Transition PendingTransition;

        /// <summary>
        /// Initializes a new instance of the <see cref="Monitor"/> class.
        /// </summary>
        protected Monitor()
            : base()
        {
            this.ActionMap = new Dictionary<string, CachedDelegate>();
            this.LivenessTemperature = 0;
        }

        /// <summary>
        /// Initializes this monitor.
        /// </summary>
        internal void Initialize(Configuration configuration, CoyoteRuntime runtime)
        {
            this.Configuration = configuration;
            this.Runtime = runtime;
        }

        /// <summary>
        /// Raises the specified <see cref="Event"/> at the end of the current action.
        /// </summary>
        /// <remarks>
        /// This event is not handled until the action that calls this method returns control back
        /// to the Coyote runtime.  It is handled before any other events are dequeued from the inbox.
        /// Only one of the following can be called per action:
        /// <see cref="RaiseEvent"/>, <see cref="RaiseGotoStateEvent{T}"/>.
        /// An Assert is raised if you accidentally try and do two of these operations in a single action.
        /// </remarks>
        /// <param name="e">The event to raise.</param>
        protected void RaiseEvent(Event e)
        {
            this.Assert(e != null, "{0} is raising a null event.", this.Name);
            this.CheckDanglingTransition();
            this.PendingTransition = new Transition(Transition.Type.Raise, default, e);
        }

        /// <summary>
        /// Raise a special event that performs a goto state operation at the end of the current action.
        /// </summary>
        /// <remarks>
        /// Goto state pops the current <see cref="State"/> and pushes the specified <see cref="State"/> on the active state stack.
        ///
        /// This is shorthand for the following code:
        /// <code>
        /// class Event E { }
        /// [OnEventGotoState(typeof(E), typeof(S))]
        /// this.RaiseEvent(new E());
        /// </code>
        /// This event is not handled until the action that calls this method returns control back
        /// to the Coyote runtime.  It is handled before any other events are dequeued from the inbox.
        /// Only one of the following can be called per action:
        /// <see cref="RaiseEvent"/>, <see cref="RaiseGotoStateEvent{T}"/>.
        /// An Assert is raised if you accidentally try and do two of these operations in a single action.
        /// </remarks>
        /// <typeparam name="TState">Type of the state.</typeparam>
        protected void RaiseGotoStateEvent<TState>()
            where TState : State =>
            this.RaiseGotoStateEvent(typeof(TState));

        /// <summary>
        /// Raise a special event that performs a goto state operation at the end of the current action.
        /// </summary>
        /// <remarks>
        /// Goto state pops the current <see cref="State"/> and pushes the specified <see cref="State"/> on the active state stack.
        ///
        /// This is shorthand for the following code:
        /// <code>
        /// class Event E { }
        /// [OnEventGotoState(typeof(E), typeof(S))]
        /// this.RaiseEvent(new E());
        /// </code>
        /// This event is not handled until the action that calls this method returns control back
        /// to the Coyote runtime.  It is handled before any other events are dequeued from the inbox.
        /// Only one of the following can be called per action:
        /// <see cref="RaiseEvent"/>, <see cref="RaiseGotoStateEvent{T}"/>.
        /// An Assert is raised if you accidentally try and do two of these operations in a single action.
        /// </remarks>
        /// <param name="state">Type of the state.</param>
        protected void RaiseGotoStateEvent(Type state)
        {
            // If the state is not a state of the monitor, then report an error and exit.
            this.Assert(StateTypeMap[this.GetType()].Any(val => val.DeclaringType.Equals(state.DeclaringType) && val.Name.Equals(state.Name)),
                "{0} is trying to transition to non-existing state '{1}'.", this.Name, state.Name);
            this.CheckDanglingTransition();
            this.PendingTransition = new Transition(Transition.Type.Goto, state, default);
        }

        /// <summary>
        /// Checks if the assertion holds, and if not, throws an <see cref="AssertionFailureException"/> exception.
        /// </summary>
        protected void Assert(bool predicate)
        {
            if (!predicate)
            {
                this.LogMonitorError(this);
                this.Runtime.Assert(false);
            }
        }

        /// <summary>
        /// Checks if the assertion holds, and if not, throws an <see cref="AssertionFailureException"/> exception.
        /// </summary>
        protected void Assert(bool predicate, string s, object arg0)
        {
            if (!predicate)
            {
                this.LogMonitorError(this);
                this.Runtime.Assert(false, s, arg0);
            }
        }

        /// <summary>
        /// Checks if the assertion holds, and if not, throws an <see cref="AssertionFailureException"/> exception.
        /// </summary>
        protected void Assert(bool predicate, string s, object arg0, object arg1)
        {
            if (!predicate)
            {
                this.LogMonitorError(this);
                this.Runtime.Assert(false, s, arg0, arg1);
            }
        }

        /// <summary>
        /// Checks if the assertion holds, and if not, throws an <see cref="AssertionFailureException"/> exception.
        /// </summary>
        protected void Assert(bool predicate, string s, object arg0, object arg1, object arg2)
        {
            if (!predicate)
            {
                this.LogMonitorError(this);
                this.Runtime.Assert(false, s, arg0, arg1, arg2);
            }
        }

        /// <summary>
        /// Checks if the assertion holds, and if not, throws an <see cref="AssertionFailureException"/> exception.
        /// </summary>
        protected void Assert(bool predicate, string s, params object[] args)
        {
            if (!predicate)
            {
                this.LogMonitorError(this);
                this.Runtime.Assert(false, s, args);
            }
        }

        /// <summary>
        /// Notifies the monitor to handle the received event.
        /// </summary>
        internal void MonitorEvent(Event e, string senderName, string senderType, string senderState)
        {
            this.Runtime.LogManager.LogMonitorProcessEvent(this.Name, this.CurrentStateName, senderName, senderType, senderState, e);
            this.HandleEvent(e);
        }

        /// <summary>
        /// Handles the given event.
        /// </summary>
        private void HandleEvent(Event e)
        {
            // Do not process an ignored event.
            if (this.IsEventIgnoredInCurrentState(e))
            {
                return;
            }

            while (true)
            {
                if (this.ActiveState is null)
                {
                    // If the event cannot be handled, then report an error and exit.
                    this.Assert(false, "{0} received event '{1}' that cannot be handled.",
                        this.Name, e.GetType().FullName);
                }

                // If current state cannot handle the event then null the state.
                if (!this.CanHandleEvent(e.GetType()))
                {
                    this.LogExitedState(this);
                    this.ActiveState = null;
                    continue;
                }

                if (e.GetType() == typeof(GotoStateEvent))
                {
                    // Checks if the event is a goto state event.
                    Type targetState = (e as GotoStateEvent).State;
                    this.GotoState(targetState, null, e);
                }
                else if (this.EventHandlers.ContainsKey(e.GetType()))
                {
                    // Checks if the event can trigger an action.
                    var handler = this.EventHandlers[e.GetType()];
                    if (handler is ActionEventHandlerDeclaration action)
                    {
                        this.Do(action.Name, e);
                    }
                    else if (handler is GotoStateTransition transition)
                    {
                        this.GotoState(transition.TargetState, transition.Lambda, e);
                    }
                }
                else if (this.EventHandlers.ContainsKey(typeof(WildCardEvent)))
                {
                    // Checks if the event can trigger an action.
                    var handler = this.EventHandlers[typeof(WildCardEvent)];
                    if (handler is ActionEventHandlerDeclaration action)
                    {
                        this.Do(action.Name, e);
                    }
                    else if (handler is GotoStateTransition transition)
                    {
                        this.GotoState(transition.TargetState, transition.Lambda, e);
                    }
                }

                break;
            }
        }

        /// <summary>
        /// Checks if the specified event is ignored in the current monitor state.
        /// </summary>
        private bool IsEventIgnoredInCurrentState(Event e)
        {
            if (this.IgnoredEvents.Contains(e.GetType()) ||
                this.IgnoredEvents.Contains(typeof(WildCardEvent)))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Invokes an action.
        /// </summary>
#if !DEBUG
        [System.Diagnostics.DebuggerStepThrough]
#endif
        private void Do(string actionName, Event e)
        {
            CachedDelegate cachedAction = this.ActionMap[actionName];
            this.LogInvokedAction(this, cachedAction.MethodInfo, this.CurrentStateNameWithTemperature);
            this.ExecuteAction(cachedAction, e);
            this.ApplyEventHandlerTransition(this.PendingTransition);
        }

        /// <summary>
        /// Executes the on entry function of the current state.
        /// </summary>
#if !DEBUG
        [System.Diagnostics.DebuggerStepThrough]
#endif
        private void ExecuteCurrentStateOnEntry(Event e)
        {
            this.LogEnteredState(this);

            CachedDelegate entryAction = null;
            if (this.ActiveState.EntryAction != null)
            {
                entryAction = this.ActionMap[this.ActiveState.EntryAction];
            }

            // Invokes the entry action of the new state,
            // if there is one available.
            if (entryAction != null)
            {
                this.ExecuteAction(entryAction, e);
                this.ApplyEventHandlerTransition(this.PendingTransition);
            }
        }

        /// <summary>
        /// Executes the on exit function of the current state.
        /// </summary>
#if !DEBUG
        [System.Diagnostics.DebuggerStepThrough]
#endif
        private void ExecuteCurrentStateOnExit(string eventHandlerExitActionName, Event e)
        {
            this.LogExitedState(this);

            CachedDelegate exitAction = null;
            if (this.ActiveState.ExitAction != null)
            {
                exitAction = this.ActionMap[this.ActiveState.ExitAction];
            }

            // Invokes the exit action of the current state,
            // if there is one available.
            if (exitAction != null)
            {
                this.ExecuteAction(exitAction, e);
                Transition transition = this.PendingTransition;
                this.Assert(transition.TypeValue is Transition.Type.None,
                    "{0} has performed a '{1}' transition from an OnExit action.",
                    this.Name, transition.TypeValue);
                this.ApplyEventHandlerTransition(transition);
            }

            // Invokes the exit action of the event handler,
            // if there is one available.
            if (eventHandlerExitActionName != null)
            {
                CachedDelegate eventHandlerExitAction = this.ActionMap[eventHandlerExitActionName];
                this.ExecuteAction(eventHandlerExitAction, e);
                Transition transition = this.PendingTransition;
                this.Assert(transition.TypeValue is Transition.Type.None,
                    "{0} has performed a '{1}' transition from an OnExit action.",
                    this.Name, transition.TypeValue);
                this.ApplyEventHandlerTransition(transition);
            }
        }

        /// <summary>
        /// Executes the specified action.
        /// </summary>
#if !DEBUG
        [System.Diagnostics.DebuggerStepThrough]
#endif
        private void ExecuteAction(CachedDelegate cachedAction, Event e)
        {
            try
            {
                if (cachedAction.Handler is Action<Event> actionWithEvent)
                {
                    actionWithEvent(e);
                }
                else if (cachedAction.Handler is Action action)
                {
                    action();
                }
            }
            catch (Exception ex)
            {
                Exception innerException = ex;
                while (innerException is TargetInvocationException)
                {
                    innerException = innerException.InnerException;
                }

                if (innerException is AggregateException)
                {
                    innerException = innerException.InnerException;
                }

                if (innerException.GetBaseException() is System.Threading.ThreadInterruptedException threadEx)
                {
                    ExceptionDispatchInfo.Capture(threadEx).Throw();
                }
                else
                {
                    // Reports the unhandled exception.
                    this.ReportUnhandledException(innerException, cachedAction.MethodInfo.Name);
                }
            }
        }

        /// <summary>
        /// Applies the specified event handler transition.
        /// </summary>
        private void ApplyEventHandlerTransition(Transition transition)
        {
            if (transition.TypeValue != this.PendingTransition.TypeValue && this.PendingTransition.TypeValue != Transition.Type.None)
            {
                this.CheckDanglingTransition();
            }
            else if (transition.TypeValue is Transition.Type.Raise)
            {
                this.PendingTransition = default;
                var e = transition.Event;
                this.LogRaisedEvent(this, e);
                this.HandleEvent(e);
            }
            else if (transition.TypeValue is Transition.Type.Goto)
            {
                this.PendingTransition = default;
                var e = new GotoStateEvent(transition.State);
                this.LogRaisedEvent(this, e);
                this.HandleEvent(e);
            }
            else
            {
                this.PendingTransition = default;
            }
        }

        /// <summary>
        /// Notifies that a Transition was created but not returned to the Monitor.
        /// </summary>
        private void CheckDanglingTransition()
        {
            var transition = this.PendingTransition;
            this.PendingTransition = default;

            if (transition.TypeValue != Transition.Type.None)
            {
                string prefix = string.Format("{0} Transition created by {1} in state {2} was not processed",
                    transition.TypeValue, this.Name, this.CurrentStateName);
                string suffix = null;

                if (transition.State != null && transition.Event != null)
                {
                    suffix = string.Format(", state {0}, event {1}.", transition.State, transition.Event);
                }
                else if (transition.State != null)
                {
                    suffix = string.Format(", state {0}.", transition.State);
                }
                else if (transition.Event != null)
                {
                    suffix = string.Format(", event {0}.", transition.Event);
                }

                this.Assert(false, prefix + suffix);
            }
        }

        /// <summary>
        /// Performs a goto transition to the given state.
        /// </summary>
        private void GotoState(Type s, string onExitActionName, Event e)
        {
            // The monitor performs the on exit statements of the current state.
            this.ExecuteCurrentStateOnExit(onExitActionName, e);

            var nextState = StateMap[this.GetType()].First(val => val.GetType().Equals(s));
            this.ConfigureStateTransitions(nextState);

            // The monitor transitions to the new state.
            this.ActiveState = nextState;

            if (nextState.IsCold)
            {
                this.LivenessTemperature = 0;
            }

            // The monitor performs the on entry statements of the new state.
            this.ExecuteCurrentStateOnEntry(e);
        }

        /// <summary>
        /// Checks if the state can handle the given event type. An event
        /// can be handled if it is deferred, or leads to a transition or
        /// action binding.
        /// </summary>
        private bool CanHandleEvent(Type e)
        {
            if (this.EventHandlers.ContainsKey(e) ||
                this.EventHandlers.ContainsKey(typeof(WildCardEvent)) ||
                e == typeof(GotoStateEvent))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks the liveness temperature of the monitor and report a potential liveness bug if the
        /// the value exceeded the specified threshold.
        /// </summary>
        /// <remarks>
        /// This method only works if this is a liveness monitor.
        /// </remarks>
        internal bool IsLivenessThresholdExceeded(int threshold)
        {
            if (this.ActiveState.IsHot && threshold > 0)
            {
                this.LivenessTemperature++;
                if (this.LivenessTemperature > threshold)
                {
                    this.LogMonitorError(this);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if the monitor is in a hot state.
        /// </summary>
        private bool IsInHotState() => this.ActiveState?.IsHot ?? false;

        /// <summary>
        /// Returns true if the monitor is in a hot state. Also outputs
        /// the name of the current state.
        /// </summary>
        internal bool IsInHotState(out string stateName)
        {
            stateName = this.CurrentStateName;
            return this.IsInHotState();
        }

        /// <summary>
        /// Returns true if the monitor is in a cold state.
        /// </summary>
        private bool IsInColdState() => this.ActiveState?.IsCold ?? false;

        /// <summary>
        /// Returns a nullable boolean indicating liveness temperature: true for hot, false for cold, else null.
        /// </summary>
        internal bool? GetHotState() => this.IsInHotState() ? true : this.IsInColdState() ? (bool?)false : null;

        /// <summary>
        /// Returns the hashed state of this monitor.
        /// </summary>
        internal int GetHashedState()
        {
            unchecked
            {
                var hash = 19;

                hash = (hash * 31) + this.GetType().GetHashCode();
                hash = (hash * 31) + this.CurrentState.GetHashCode();

                if (this.HashedState != 0)
                {
                    // Adds the user-defined hashed state.
                    hash = (hash * 31) + this.HashedState;
                }

                return hash;
            }
        }

        /// <summary>
        /// Returns a string that represents the current monitor.
        /// </summary>
        public override string ToString() => this.Name;

        /// <summary>
        /// Transitions to the start state, and executes the
        /// entry action, if there is any.
        /// </summary>
        internal void GotoStartState()
        {
            this.Runtime.LogManager.LogCreateMonitor(this.Name);
            if (this.Runtime.SchedulingPolicy is SchedulingPolicy.Interleaving
                && this.Configuration.IsActivityCoverageReported)
            {
                this.ReportActivityCoverage(this.Runtime.CoverageInfo);
            }

            this.ExecuteCurrentStateOnEntry(DefaultEvent.Instance);
        }

        /// <summary>
        /// Initializes information about the states of the monitor.
        /// </summary>
        internal void InitializeStateInformation()
        {
            Type monitorType = this.GetType();

            // If this type has not already been setup in the MonitorActionMap, then we need to try and grab the ActionCacheLock
            // for this type.  First make sure we have one and only one lockable object for this type.
            object syncObject = ActionCacheLocks.GetOrAdd(monitorType, _ => new object());

            // Locking this syncObject ensures only one thread enters the initialization code to update
            // the ActionCache for this specific Actor type.
            lock (syncObject)
            {
                if (MonitorActionMap.ContainsKey(monitorType))
                {
                    // Note: even if we won the GetOrAdd, there is a tiny window of opportunity for another thread
                    // to slip in and lock the syncObject before us, so we have to check the ActionCache again
                    // here just in case.
                }
                else
                {
                    // Caches the actions declarations for this monitor type.
                    if (MonitorActionMap.TryAdd(monitorType, new Dictionary<string, MethodInfo>()))
                    {
                        // Caches the available state types for this monitor type.
                        if (StateTypeMap.TryAdd(monitorType, new HashSet<Type>()))
                        {
                            Type baseType = monitorType;
                            while (baseType != typeof(Monitor))
                            {
                                foreach (var s in baseType.GetNestedTypes(BindingFlags.Instance |
                                    BindingFlags.NonPublic | BindingFlags.Public |
                                    BindingFlags.DeclaredOnly))
                                {
                                    this.ExtractStateTypes(s);
                                }

                                baseType = baseType.BaseType;
                            }
                        }

                        // Caches the available state instances for this monitor type.
                        if (StateMap.TryAdd(monitorType, new HashSet<State>()))
                        {
                            foreach (var type in StateTypeMap[monitorType])
                            {
                                Type stateType = type;
                                if (type.IsAbstract)
                                {
                                    continue;
                                }

                                if (type.IsGenericType)
                                {
                                    // If the state type is generic (only possible if inherited by a
                                    // generic monitor declaration), then iterate through the base
                                    // monitor classes to identify the runtime generic type, and use
                                    // it to instantiate the runtime state type. This type can be
                                    // then used to create the state constructor.
                                    Type declaringType = this.GetType();
                                    while (!declaringType.IsGenericType ||
                                        !type.DeclaringType.FullName.Equals(declaringType.FullName.Substring(
                                        0, declaringType.FullName.IndexOf('['))))
                                    {
                                        declaringType = declaringType.BaseType;
                                    }

                                    if (declaringType.IsGenericType)
                                    {
                                        stateType = type.MakeGenericType(declaringType.GetGenericArguments());
                                    }
                                }

                                ConstructorInfo constructor = stateType.GetConstructor(Type.EmptyTypes);
                                var lambda = Expression.Lambda<Func<State>>(Expression.New(constructor)).Compile();
                                State state = lambda();

                                state.InitializeState();

                                this.Assert(
                                    (state.IsCold && !state.IsHot) ||
                                    (!state.IsCold && state.IsHot) ||
                                    (!state.IsCold && !state.IsHot),
                                    "State '{0}' of {1} cannot be both cold and hot.", type.FullName, this.Name);

                                StateMap[monitorType].Add(state);
                            }
                        }

                        foreach (var state in StateMap[monitorType])
                        {
                            if (state.EntryAction != null &&
                                !MonitorActionMap[monitorType].ContainsKey(state.EntryAction))
                            {
                                MonitorActionMap[monitorType].Add(
                                    state.EntryAction,
                                    this.GetActionWithName(state.EntryAction));
                            }

                            if (state.ExitAction != null &&
                                !MonitorActionMap[monitorType].ContainsKey(state.ExitAction))
                            {
                                MonitorActionMap[monitorType].Add(
                                    state.ExitAction,
                                    this.GetActionWithName(state.ExitAction));
                            }

                            foreach (var handler in state.EventHandlers.Values)
                            {
                                if (handler is ActionEventHandlerDeclaration action)
                                {
                                    if (!MonitorActionMap[monitorType].ContainsKey(action.Name))
                                    {
                                        MonitorActionMap[monitorType].Add(
                                            action.Name,
                                            this.GetActionWithName(action.Name));
                                    }
                                }
                                else if (handler is GotoStateTransition transition)
                                {
                                    if (transition.Lambda != null &&
                                        !MonitorActionMap[monitorType].ContainsKey(transition.Lambda))
                                    {
                                        MonitorActionMap[monitorType].Add(
                                            transition.Lambda,
                                            this.GetActionWithName(transition.Lambda));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Populates the map of actions for this monitor instance.
            foreach (var kvp in MonitorActionMap[monitorType])
            {
                this.ActionMap.Add(kvp.Key, new CachedDelegate(kvp.Value, this));
            }

            var initialStates = StateMap[monitorType].Where(state => state.IsStart).ToList();
            this.Assert(initialStates.Count != 0, "{0} must declare a start state.", this.Name);
            this.Assert(initialStates.Count is 1, "{0} can not declare more than one start states.", this.Name);

            this.ConfigureStateTransitions(initialStates.Single());
            this.ActiveState = initialStates.Single();

            this.AssertStateValidity();
        }

        /// <summary>
        /// Processes a type, looking for monitor states.
        /// </summary>
        private void ExtractStateTypes(Type type)
        {
            Stack<Type> stack = new Stack<Type>();
            stack.Push(type);

            while (stack.Count > 0)
            {
                Type nextType = stack.Pop();

                if (nextType.IsClass && nextType.IsSubclassOf(typeof(State)))
                {
                    StateTypeMap[this.GetType()].Add(nextType);
                }
                else if (nextType.IsClass && nextType.IsSubclassOf(typeof(StateGroup)))
                {
                    // Adds the contents of the group of states to the stack.
                    foreach (var t in nextType.GetNestedTypes(BindingFlags.Instance |
                        BindingFlags.NonPublic | BindingFlags.Public |
                        BindingFlags.DeclaredOnly))
                    {
                        this.Assert(t.IsSubclassOf(typeof(StateGroup)) || t.IsSubclassOf(typeof(State)),
                            "'{0}' is neither a group of states nor a state.", t.Name);
                        stack.Push(t);
                    }
                }
            }
        }

        /// <summary>
        /// Configures the state transitions of the monitor.
        /// </summary>
        private void ConfigureStateTransitions(State state)
        {
            this.EventHandlers = state.EventHandlers;
            this.IgnoredEvents = state.IgnoredEvents;
        }

        /// <summary>
        /// Returns the action with the specified name.
        /// </summary>
        private MethodInfo GetActionWithName(string actionName)
        {
            MethodInfo action;
            Type monitorType = this.GetType();

            do
            {
                BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.FlattenHierarchy;
                action = monitorType.GetMethod(actionName, bindingFlags, Type.DefaultBinder, SingleEventTypeArray, null);
                if (action is null)
                {
                    action = monitorType.GetMethod(actionName, bindingFlags, Type.DefaultBinder, Array.Empty<Type>(), null);
                }

                monitorType = monitorType.BaseType;
            }
            while (action is null && monitorType != typeof(Monitor));

            this.Assert(action != null, "Cannot detect action declaration '{0}' in {1}.",
                actionName, this.Name);

            ParameterInfo[] parameters = action.GetParameters();
            this.Assert(parameters.Length is 0 ||
                (parameters.Length is 1 && parameters[0].ParameterType == typeof(Event)),
                "Action '{0}' in {1} must either accept no parameters or a single parameter of type 'Event'.",
                action.Name, this.Name);

            this.Assert(action.ReturnType == typeof(void),
                "Action '{0}' in {1} must have 'void' return type.",
                action.Name, this.Name);

            return action;
        }

        /// <summary>
        /// Logs that the monitor entered a state.
        /// </summary>
        private void LogEnteredState(Monitor monitor) =>
            this.Runtime.LogManager.LogMonitorStateTransition(monitor.GetType().FullName,
            monitor.CurrentStateName, true, monitor.GetHotState());

        /// <summary>
        /// Logs that the monitor exited a state.
        /// </summary>
        private void LogExitedState(Monitor monitor) =>
            this.Runtime.LogManager.LogMonitorStateTransition(monitor.GetType().FullName,
            monitor.CurrentStateName, false, monitor.GetHotState());

        /// <summary>
        /// Logs that the monitor invoked an action.
        /// </summary>
        private void LogInvokedAction(Monitor monitor, MethodInfo action, string stateName) =>
            this.Runtime.LogManager.LogMonitorExecuteAction(monitor.GetType().FullName, stateName, action.Name);

        /// <summary>
        /// Logs that the monitor raised an <see cref="Event"/>.
        /// </summary>
        private void LogRaisedEvent(Monitor monitor, Event e) =>
            this.Runtime.LogManager.LogMonitorRaiseEvent(monitor.GetType().FullName, monitor.CurrentStateNameWithTemperature, e);

        /// <summary>
        /// Logs that the monitor found an error.
        /// </summary>
        private void LogMonitorError(Monitor monitor) =>
            this.Runtime.LogManager.LogMonitorError(monitor.GetType().FullName, monitor.CurrentStateName, monitor.GetHotState());

        /// <summary>
        /// Reports the activity coverage of this monitor.
        /// </summary>
        internal void ReportActivityCoverage(CoverageInfo coverageInfo)
        {
            var monitorName = this.Name;
            if (coverageInfo.IsMonitorDeclared(monitorName))
            {
                return;
            }

            this.Assert(StateMap.ContainsKey(this.GetType()), "{0} has not populated its states yet.", this.Name);

            // Fetch the monitor states.
            var states = new HashSet<string>();
            foreach (var state in StateMap[this.GetType()])
            {
                states.Add(NameResolver.GetQualifiedStateName(state.GetType()));
            }

            foreach (var state in states)
            {
                coverageInfo.DeclareMonitorState(monitorName, state);
            }

            // Fetch the registered events.
            var pairs = new HashSet<Tuple<string, string>>();
            foreach (var state in StateMap[this.GetType()])
            {
                foreach (var binding in state.EventHandlers)
                {
                    pairs.Add(Tuple.Create(NameResolver.GetQualifiedStateName(state.GetType()), binding.Key.FullName));
                }
            }

            foreach (var tup in pairs)
            {
                coverageInfo.DeclareMonitorStateEventPair(monitorName, tup.Item1, tup.Item2);
            }
        }

        /// <summary>
        /// Check monitor for state related errors.
        /// </summary>
        private void AssertStateValidity()
        {
            this.Assert(StateTypeMap[this.GetType()].Count > 0, "{0} must have one or more states.", this.Name);
            this.Assert(this.ActiveState != null, "{0} must not have a null current state.", this.Name);
        }

        /// <summary>
        /// Wraps the unhandled exception inside an <see cref="AssertionFailureException"/>
        /// exception, and throws it to the user.
        /// </summary>
        private void ReportUnhandledException(Exception ex, string actionName)
        {
            var state = this.CurrentState is null ? "<unknown>" : this.CurrentStateName;
            this.Runtime.WrapAndThrowException(ex, "{0} (state '{1}', action '{2}')",
                this.Name, state, actionName);
        }

        /// <summary>
        /// Defines the <see cref="Monitor"/> transition that is the
        /// result of executing an event handler.  Transitions are created by using
        /// <see cref="RaiseGotoStateEvent{T}"/>, or <see cref="RaiseEvent"/>.
        /// The Transition is processed by the Coyote runtime when
        /// an event handling method returns a Transition object.
        /// This means such a method can only do one such Transition per method call.
        /// If the method wants to do a conditional transition it can return
        /// Transition.None to indicate no transition is to be performed.
        /// </summary>
        internal readonly struct Transition
        {
            /// <summary>
            /// The type of the transition.
            /// </summary>
            public readonly Type TypeValue;

            /// <summary>
            /// The target state of the transition, if there is one.
            /// </summary>
            internal readonly System.Type State;

            /// <summary>
            /// The event participating in the transition, if there is one.
            /// </summary>
            internal readonly Event Event;

            /// <summary>
            /// This special transition represents a transition that does not change the current <see cref="Monitor.State"/>.
            /// </summary>
            public static Transition None = default;

            /// <summary>
            /// Initializes a new instance of the <see cref="Transition"/> struct.
            /// </summary>
            /// <param name="type">The type of the transition.</param>
            /// <param name="state">The target state of the transition, if there is one.</param>
            /// <param name="e">The event participating in the transition, if there is one.</param>
            internal Transition(Type type, System.Type state, Event e)
            {
                this.TypeValue = type;
                this.State = state;
                this.Event = e;
            }

            /// <summary>
            /// Defines the type of a <see cref="Monitor"/> transition.
            /// </summary>
            public enum Type
            {
                /// <summary>
                /// A transition that does not change the <see cref="Monitor.State"/>.
                /// This is the value used by <see cref="Transition.None"/>.
                /// </summary>
                None = 0,

                /// <summary>
                /// A transition created by <see cref="RaiseEvent(Event)"/> that raises an <see cref="Event"/> bypassing
                /// the <see cref="Monitor.State"/> inbox.
                /// </summary>
                Raise,

                /// <summary>
                /// A transition created by <see cref="RaiseGotoStateEvent{S}"/> from the current <see cref="Monitor.State"/>
                /// to the specified <see cref="Monitor.State"/>.
                /// </summary>
                Goto
            }
        }

        /// <summary>
        /// Abstract class representing a state.
        /// </summary>
        public abstract class State
        {
            /// <summary>
            /// The entry action of the state.
            /// </summary>
            internal string EntryAction { get; private set; }

            /// <summary>
            /// The exit action of the state.
            /// </summary>
            internal string ExitAction { get; private set; }

            /// <summary>
            /// Dictionary containing all the event handlers.
            /// </summary>
            internal Dictionary<Type, EventHandlerDeclaration> EventHandlers;

            /// <summary>
            /// Set of ignored event types.
            /// </summary>
            internal HashSet<Type> IgnoredEvents;

            /// <summary>
            /// True if this is the start state.
            /// </summary>
            internal bool IsStart { get; private set; }

            /// <summary>
            /// Returns true if this is a hot state.
            /// </summary>
            internal bool IsHot { get; private set; }

            /// <summary>
            /// Returns true if this is a cold state.
            /// </summary>
            internal bool IsCold { get; private set; }

            /// <summary>
            /// Initializes a new instance of the <see cref="State"/> class.
            /// </summary>
            protected State()
            {
            }

            /// <summary>
            /// Initializes the state.
            /// </summary>
            internal void InitializeState()
            {
                this.IsStart = false;
                this.IsHot = false;
                this.IsCold = false;

                this.EventHandlers = new Dictionary<Type, EventHandlerDeclaration>();

                this.IgnoredEvents = new HashSet<Type>();

                if (this.GetType().GetCustomAttribute(typeof(OnEntryAttribute), true) is OnEntryAttribute entryAttribute)
                {
                    this.EntryAction = entryAttribute.Action;
                }

                if (this.GetType().GetCustomAttribute(typeof(OnExitAttribute), true) is OnExitAttribute exitAttribute)
                {
                    this.ExitAction = exitAttribute.Action;
                }

                if (this.GetType().IsDefined(typeof(StartAttribute), false))
                {
                    this.IsStart = true;
                }

                if (this.GetType().IsDefined(typeof(HotAttribute), false))
                {
                    this.IsHot = true;
                }

                if (this.GetType().IsDefined(typeof(ColdAttribute), false))
                {
                    this.IsCold = true;
                }

                // Events with already declared handlers.
                var handledEvents = new HashSet<Type>();

                // Install event handlers.
                this.InstallGotoTransitions(handledEvents);
                this.InstallActionHandlers(handledEvents);
                this.InstallIgnoreHandlers(handledEvents);
            }

            /// <summary>
            /// Declares goto event handlers, if there are any.
            /// </summary>
            private void InstallGotoTransitions(HashSet<Type> handledEvents)
            {
                var gotoAttributes = this.GetType().GetCustomAttributes(typeof(OnEventGotoStateAttribute), false)
                    as OnEventGotoStateAttribute[];

                foreach (var attr in gotoAttributes)
                {
                    CheckEventHandlerAlreadyDeclared(attr.Event, handledEvents);

                    if (attr.Action is null)
                    {
                        this.EventHandlers.Add(attr.Event, new GotoStateTransition(attr.State));
                    }
                    else
                    {
                        this.EventHandlers.Add(attr.Event, new GotoStateTransition(attr.State, attr.Action));
                    }

                    handledEvents.Add(attr.Event);
                }

                this.InheritGotoTransitions(this.GetType().BaseType, handledEvents);
            }

            /// <summary>
            /// Inherits goto event handlers from a base state, if there is one.
            /// </summary>
            private void InheritGotoTransitions(Type baseState, HashSet<Type> handledEvents)
            {
                if (!baseState.IsSubclassOf(typeof(State)))
                {
                    return;
                }

                var gotoAttributesInherited = baseState.GetCustomAttributes(typeof(OnEventGotoStateAttribute), false)
                    as OnEventGotoStateAttribute[];

                var gotoTransitionsInherited = new Dictionary<Type, GotoStateTransition>();
                foreach (var attr in gotoAttributesInherited)
                {
                    if (this.EventHandlers.ContainsKey(attr.Event))
                    {
                        continue;
                    }

                    CheckEventHandlerAlreadyInherited(attr.Event, baseState, handledEvents);

                    if (attr.Action is null)
                    {
                        gotoTransitionsInherited.Add(attr.Event, new GotoStateTransition(attr.State));
                    }
                    else
                    {
                        gotoTransitionsInherited.Add(attr.Event, new GotoStateTransition(attr.State, attr.Action));
                    }

                    handledEvents.Add(attr.Event);
                }

                foreach (var kvp in gotoTransitionsInherited)
                {
                    this.EventHandlers.Add(kvp.Key, kvp.Value);
                }

                this.InheritGotoTransitions(baseState.BaseType, handledEvents);
            }

            /// <summary>
            /// Declares action event handlers, if there are any.
            /// </summary>
            private void InstallActionHandlers(HashSet<Type> handledEvents)
            {
                var doAttributes = this.GetType().GetCustomAttributes(typeof(OnEventDoActionAttribute), false)
                    as OnEventDoActionAttribute[];

                foreach (var attr in doAttributes)
                {
                    CheckEventHandlerAlreadyDeclared(attr.Event, handledEvents);

                    this.EventHandlers.Add(attr.Event, new ActionEventHandlerDeclaration(attr.Action));
                    handledEvents.Add(attr.Event);
                }

                this.InheritActionHandlers(this.GetType().BaseType, handledEvents);
            }

            /// <summary>
            /// Inherits action event handlers from a base state, if there is one.
            /// </summary>
            private void InheritActionHandlers(Type baseState, HashSet<Type> handledEvents)
            {
                if (!baseState.IsSubclassOf(typeof(State)))
                {
                    return;
                }

                var doAttributesInherited = baseState.GetCustomAttributes(typeof(OnEventDoActionAttribute), false)
                    as OnEventDoActionAttribute[];

                var actionBindingsInherited = new Dictionary<Type, ActionEventHandlerDeclaration>();
                foreach (var attr in doAttributesInherited)
                {
                    if (this.EventHandlers.ContainsKey(attr.Event))
                    {
                        continue;
                    }

                    CheckEventHandlerAlreadyInherited(attr.Event, baseState, handledEvents);

                    actionBindingsInherited.Add(attr.Event, new ActionEventHandlerDeclaration(attr.Action));
                    handledEvents.Add(attr.Event);
                }

                foreach (var kvp in actionBindingsInherited)
                {
                    this.EventHandlers.Add(kvp.Key, kvp.Value);
                }

                this.InheritActionHandlers(baseState.BaseType, handledEvents);
            }

            /// <summary>
            /// Declares ignore event handlers, if there are any.
            /// </summary>
            private void InstallIgnoreHandlers(HashSet<Type> handledEvents)
            {
                if (this.GetType().GetCustomAttribute(typeof(IgnoreEventsAttribute), false) is IgnoreEventsAttribute ignoreEventsAttribute)
                {
                    foreach (var e in ignoreEventsAttribute.Events)
                    {
                        CheckEventHandlerAlreadyDeclared(e, handledEvents);
                    }

                    this.IgnoredEvents.UnionWith(ignoreEventsAttribute.Events);
                    handledEvents.UnionWith(ignoreEventsAttribute.Events);
                }

                this.InheritIgnoreHandlers(this.GetType().BaseType, handledEvents);
            }

            /// <summary>
            /// Inherits ignore event handlers from a base state, if there is one.
            /// </summary>
            private void InheritIgnoreHandlers(Type baseState, HashSet<Type> handledEvents)
            {
                if (!baseState.IsSubclassOf(typeof(State)))
                {
                    return;
                }

                if (baseState.GetCustomAttribute(typeof(IgnoreEventsAttribute), false) is IgnoreEventsAttribute ignoreEventsAttribute)
                {
                    foreach (var e in ignoreEventsAttribute.Events)
                    {
                        if (this.IgnoredEvents.Contains(e))
                        {
                            continue;
                        }

                        CheckEventHandlerAlreadyInherited(e, baseState, handledEvents);
                    }

                    this.IgnoredEvents.UnionWith(ignoreEventsAttribute.Events);
                    handledEvents.UnionWith(ignoreEventsAttribute.Events);
                }

                this.InheritIgnoreHandlers(baseState.BaseType, handledEvents);
            }

            /// <summary>
            /// Checks if an event handler has been already declared.
            /// </summary>
            private static void CheckEventHandlerAlreadyDeclared(Type e, HashSet<Type> handledEvents)
            {
                if (handledEvents.Contains(e))
                {
                    throw new InvalidOperationException($"declared multiple handlers for event '{e}'");
                }
            }

            /// <summary>
            /// Checks if an event handler has been already inherited.
            /// </summary>
            private static void CheckEventHandlerAlreadyInherited(Type e, Type baseState, HashSet<Type> handledEvents)
            {
                if (handledEvents.Contains(e))
                {
                    throw new InvalidOperationException($"inherited multiple handlers for event '{e}' from state '{baseState}'");
                }
            }

            /// <summary>
            /// Attribute for declaring that a state of a monitor
            /// is the start one.
            /// </summary>
            [AttributeUsage(AttributeTargets.Class)]
            protected sealed class StartAttribute : Attribute
            {
            }

            /// <summary>
            /// Attribute for declaring what action to perform
            /// when entering a monitor state.
            /// </summary>
            [AttributeUsage(AttributeTargets.Class)]
            protected sealed class OnEntryAttribute : Attribute
            {
                /// <summary>
                /// Action name.
                /// </summary>
                internal readonly string Action;

                /// <summary>
                /// Initializes a new instance of the <see cref="OnEntryAttribute"/> class.
                /// </summary>
                /// <param name="actionName">Action name.</param>
                public OnEntryAttribute(string actionName)
                {
                    this.Action = actionName;
                }
            }

            /// <summary>
            /// Attribute for declaring what action to perform
            /// when exiting a monitor state.
            /// </summary>
            [AttributeUsage(AttributeTargets.Class)]
            protected sealed class OnExitAttribute : Attribute
            {
                /// <summary>
                /// Action name.
                /// </summary>
                internal string Action;

                /// <summary>
                /// Initializes a new instance of the <see cref="OnExitAttribute"/> class.
                /// </summary>
                /// <param name="actionName">Action name.</param>
                public OnExitAttribute(string actionName)
                {
                    this.Action = actionName;
                }
            }

            /// <summary>
            /// Attribute for declaring which state a monitor should transition to
            /// when it receives an event in a given state.
            /// </summary>
            [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
            protected sealed class OnEventGotoStateAttribute : Attribute
            {
                /// <summary>
                /// Event type.
                /// </summary>
                internal readonly Type Event;

                /// <summary>
                /// State type.
                /// </summary>
                internal readonly Type State;

                /// <summary>
                /// Action name.
                /// </summary>
                internal readonly string Action;

                /// <summary>
                /// Initializes a new instance of the <see cref="OnEventGotoStateAttribute"/> class.
                /// </summary>
                /// <param name="eventType">Event type.</param>
                /// <param name="stateType">State type.</param>
                public OnEventGotoStateAttribute(Type eventType, Type stateType)
                {
                    this.Event = eventType;
                    this.State = stateType;
                }

                /// <summary>
                /// Initializes a new instance of the <see cref="OnEventGotoStateAttribute"/> class.
                /// </summary>
                /// <param name="eventType">Event type.</param>
                /// <param name="stateType">State type.</param>
                /// <param name="actionName">Name of action to perform on exit.</param>
                public OnEventGotoStateAttribute(Type eventType, Type stateType, string actionName)
                {
                    this.Event = eventType;
                    this.State = stateType;
                    this.Action = actionName;
                }
            }

            /// <summary>
            /// Attribute for declaring what action a monitor should perform
            /// when it receives an event in a given state.
            /// </summary>
            [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
            protected sealed class OnEventDoActionAttribute : Attribute
            {
                /// <summary>
                /// Event type.
                /// </summary>
                internal Type Event;

                /// <summary>
                /// Action name.
                /// </summary>
                internal string Action;

                /// <summary>
                /// Initializes a new instance of the <see cref="OnEventDoActionAttribute"/> class.
                /// </summary>
                /// <param name="eventType">Event type.</param>
                /// <param name="actionName">Action name.</param>
                public OnEventDoActionAttribute(Type eventType, string actionName)
                {
                    this.Event = eventType;
                    this.Action = actionName;
                }
            }

            /// <summary>
            /// Attribute for declaring what events should be ignored in
            /// a monitor state.
            /// </summary>
            [AttributeUsage(AttributeTargets.Class)]
            protected sealed class IgnoreEventsAttribute : Attribute
            {
                /// <summary>
                /// Event types.
                /// </summary>
                internal Type[] Events;

                /// <summary>
                /// Initializes a new instance of the <see cref="IgnoreEventsAttribute"/> class.
                /// </summary>
                /// <param name="eventTypes">Event types.</param>
                public IgnoreEventsAttribute(params Type[] eventTypes)
                {
                    this.Events = eventTypes;
                }
            }

            /// <summary>
            /// Attribute for declaring a cold monitor state. A monitor that
            /// is in a cold state satisfies a liveness property.
            /// </summary>
            [AttributeUsage(AttributeTargets.Class)]
            protected sealed class ColdAttribute : Attribute
            {
            }

            /// <summary>
            /// Attribute for declaring a hot monitor state. A monitor that
            /// is in a hot state violates a liveness property.
            /// </summary>
            [AttributeUsage(AttributeTargets.Class)]
            protected sealed class HotAttribute : Attribute
            {
            }
        }

        /// <summary>
        /// Abstract class used for representing a group of related states.
        /// </summary>
        public abstract class StateGroup
        {
        }

        /// <summary>
        /// Abstract class representing an event that can be send to a <see cref="Monitor"/>.
        /// </summary>
        public abstract class Event
        {
        }

        /// <summary>
        /// A default event that is generated when no user-defined event is dequeued or received.
        /// </summary>
        internal sealed class DefaultEvent : Event
        {
            /// <summary>
            /// Gets a <see cref="DefaultEvent"/> instance.
            /// </summary>
            internal static DefaultEvent Instance { get; } = new DefaultEvent();

            /// <summary>
            /// Initializes a new instance of the <see cref="DefaultEvent"/> class.
            /// </summary>
            private DefaultEvent()
                : base()
            {
            }
        }

        /// <summary>
        /// The goto state event.
        /// </summary>
        internal sealed class GotoStateEvent : Event
        {
            /// <summary>
            /// Type of the state to transition to.
            /// </summary>
            internal readonly Type State;

            /// <summary>
            /// Initializes a new instance of the <see cref="GotoStateEvent"/> class.
            /// </summary>
            /// <param name="s">Type of the state.</param>
            internal GotoStateEvent(Type s)
                : base()
            {
                this.State = s;
            }
        }

        /// <summary>
        /// The wild card event.
        /// </summary>
        public sealed class WildCardEvent : Event
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="WildCardEvent"/> class.
            /// </summary>
            public WildCardEvent()
                : base()
            {
            }
        }

        /// <summary>
        /// An abstract event handler declaration.
        /// </summary>
        internal abstract class EventHandlerDeclaration
        {
            internal abstract bool Inheritable { get; }
        }

        /// <summary>
        /// Defines an action event handler declaration.
        /// </summary>
        private sealed class ActionEventHandlerDeclaration : EventHandlerDeclaration
        {
            /// <summary>
            /// Name of the action.
            /// </summary>
            internal string Name;

            /// <summary>
            /// Initializes a new instance of the <see cref="ActionEventHandlerDeclaration"/> class.
            /// </summary>
            internal ActionEventHandlerDeclaration(string actionName)
            {
                this.Name = actionName;
            }

            internal override bool Inheritable => true;
        }

        /// <summary>
        /// Defines a goto state transition.
        /// </summary>
        private sealed class GotoStateTransition : EventHandlerDeclaration
        {
            /// <summary>
            /// The target state.
            /// </summary>
            internal Type TargetState;

            /// <summary>
            /// An optional lambda function that executes after the
            /// on-exit handler of the exiting state.
            /// </summary>
            internal string Lambda;

            /// <summary>
            /// Initializes a new instance of the <see cref="GotoStateTransition"/> class.
            /// </summary>
            /// <param name="targetState">The target state.</param>
            /// <param name="lambda">Lambda function that executes after the on-exit handler of the exiting state.</param>
            internal GotoStateTransition(Type targetState, string lambda)
            {
                this.TargetState = targetState;
                this.Lambda = lambda;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="GotoStateTransition"/> class.
            /// </summary>
            /// <param name="targetState">The target state.</param>
            internal GotoStateTransition(Type targetState)
            {
                this.TargetState = targetState;
                this.Lambda = null;
            }

            internal override bool Inheritable => false;
        }

        /// <summary>
        /// A monitor delegate that has been cached to optimize performance of invocations.
        /// </summary>
        private class CachedDelegate
        {
            internal readonly MethodInfo MethodInfo;
            internal readonly Delegate Handler;

            internal CachedDelegate(MethodInfo method, object caller)
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length is 1 && method.ReturnType == typeof(void))
                {
                    this.Handler = Delegate.CreateDelegate(typeof(Action<Event>), caller, method);
                }
                else if (method.ReturnType == typeof(void))
                {
                    this.Handler = Delegate.CreateDelegate(typeof(Action), caller, method);
                }
                else if (parameters.Length is 1 && method.ReturnType == typeof(Monitor.Transition))
                {
                    this.Handler = Delegate.CreateDelegate(typeof(Func<Event, Monitor.Transition>), caller, method);
                }
                else if (method.ReturnType == typeof(Monitor.Transition))
                {
                    this.Handler = Delegate.CreateDelegate(typeof(Func<Monitor.Transition>), caller, method);
                }
                else
                {
                    throw new InvalidOperationException($"Trying to cache invalid action delegate '{method.Name}'.");
                }

                this.MethodInfo = method;
            }
        }

        /// <summary>
        /// Utility class for resolving names.
        /// </summary>
        private static class NameResolver
        {
            /// <summary>
            /// Cache of state names.
            /// </summary>
            private static readonly ConcurrentDictionary<Type, string> StateNamesCache =
                new ConcurrentDictionary<Type, string>();

            /// <summary>
            /// Returns the qualified (i.e. <see cref="Monitor.StateGroup"/>) name of the specified
            /// state machine or monitor state, or the empty string if there is no such name.
            /// </summary>
            internal static string GetQualifiedStateName(Type state)
            {
                if (state is null)
                {
                    return string.Empty;
                }

                if (!StateNamesCache.TryGetValue(state, out string name))
                {
                    name = state.Name;

                    var nextState = state;
                    while (nextState.DeclaringType != null)
                    {
                        if (!nextState.DeclaringType.IsSubclassOf(typeof(Monitor.StateGroup)))
                        {
                            break;
                        }

                        name = string.Format(CultureInfo.InvariantCulture, "{0}.{1}", nextState.DeclaringType.Name, name);
                        nextState = nextState.DeclaringType;
                    }

                    StateNamesCache.GetOrAdd(state, name);
                }

                return name;
            }

            /// <summary>
            /// Returns the state name to be used for logging purposes.
            /// </summary>
            internal static string GetStateNameForLogging(Type state) => state is null ? "None" : GetQualifiedStateName(state);
        }
    }
}
