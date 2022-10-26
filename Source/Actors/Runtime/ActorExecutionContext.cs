﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
#if !DEBUG
using System.Diagnostics;
#endif
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Coyote.Actors.Coverage;
using Microsoft.Coyote.Actors.Mocks;
using Microsoft.Coyote.Actors.Timers;
using Microsoft.Coyote.Actors.Timers.Mocks;
using Microsoft.Coyote.Coverage;
using Microsoft.Coyote.Logging;
using Microsoft.Coyote.Runtime;
using Microsoft.Coyote.Specifications;

namespace Microsoft.Coyote.Actors
{
    /// <summary>
    /// The execution context of an actor program.
    /// </summary>
    internal class ActorExecutionContext : IActorRuntime, IRuntimeExtension
    {
        /// <summary>
        /// Object used to synchronize access to the runtime event handlers.
        /// </summary>
        private static readonly object EventHandlerSyncObject = new object();

        /// <summary>
        /// The configuration used by the runtime.
        /// </summary>
        internal readonly Configuration Configuration;

        /// <summary>
        /// The runtime associated with this context.
        /// </summary>
        internal CoyoteRuntime Runtime { get; private set; }

        /// <summary>
        /// Map from unique actor ids to actors.
        /// </summary>
        protected readonly ConcurrentDictionary<ActorId, Actor> ActorMap;

        /// <summary>
        /// Set of enabled actors.
        /// </summary>
        internal HashSet<ActorId> EnabledActors;

        /// <summary>
        /// Data structure containing information regarding testing coverage.
        /// </summary>
        internal readonly ActorCoverageInfo CoverageInfo;

        /// <summary>
        /// Responsible for writing to the installed <see cref="ILogger"/>.
        /// </summary>
        internal LogWriter LogWriter => this.Runtime.LogWriter;

        /// <summary>
        /// Manages all registered <see cref="IActorRuntimeLog"/> objects.
        /// </summary>
        internal readonly ActorLogManager LogManager;

        /// <inheritdoc/>
        public ILogger Logger
        {
            get => this.LogWriter;
            set => this.LogWriter.SetLogger(value);
        }

        /// <summary>
        /// Completes when actor quiescence is reached.
        /// </summary>
        internal TaskCompletionSource<bool> QuiescenceCompletionSource;

        /// <summary>
        /// True if the runtime is waiting for actor quiescence.
        /// </summary>
        private bool IsActorQuiescenceAwaited;

        /// <summary>
        /// Synchronizes access to the logic checking for actor quiescence.
        /// </summary>
        private readonly object QuiescenceSyncObject;

        /// <summary>
        /// True if the actor program is running, else false.
        /// </summary>
        internal bool IsRunning => this.Runtime.IsRunning;

        /// <summary>
        /// If true, the actor execution is controlled, else false.
        /// </summary>
        internal virtual bool IsExecutionControlled => false;

        /// <inheritdoc/>
        public event OnActorHaltedHandler OnActorHalted;

        /// <inheritdoc/>
        public event OnEventDroppedHandler OnEventDropped;

        /// <inheritdoc/>
        public event OnFailureHandler OnFailure
        {
            add
            {
                lock (EventHandlerSyncObject)
                {
                    this.Runtime.OnFailure += value;
                }
            }

            remove
            {
                lock (EventHandlerSyncObject)
                {
                    this.Runtime.OnFailure -= value;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorExecutionContext"/> class.
        /// </summary>
        internal ActorExecutionContext(Configuration configuration, ActorLogManager logManager)
        {
            this.Configuration = configuration;
            this.ActorMap = new ConcurrentDictionary<ActorId, Actor>();
            this.EnabledActors = new HashSet<ActorId>();
            this.CoverageInfo = new ActorCoverageInfo();
            this.LogManager = logManager;
            this.QuiescenceCompletionSource = new TaskCompletionSource<bool>();
            this.IsActorQuiescenceAwaited = false;
            this.QuiescenceSyncObject = new object();
        }

        /// <summary>
        /// Installs the specified <see cref="CoyoteRuntime"/>. Only one runtime can be installed
        /// at a time, and this method can only be called once.
        /// </summary>
        internal ActorExecutionContext WithRuntime(CoyoteRuntime runtime)
        {
            if (this.Runtime != null)
            {
                throw new InvalidOperationException("A runtime is already installed.");
            }

            this.Runtime = runtime;
            return this;
        }

        /// <inheritdoc/>
        bool IRuntimeExtension.RunTest(Delegate test, out Task task)
        {
            if (test is Action<IActorRuntime> actionWithRuntime)
            {
                actionWithRuntime(this);
                task = Task.CompletedTask;
                return true;
            }
            else if (test is Func<IActorRuntime, Task> functionWithRuntime)
            {
                task = functionWithRuntime(this);
                return true;
            }

            task = Task.CompletedTask;
            return false;
        }

        /// <inheritdoc/>
        public ActorId CreateActorId(Type type, string name = null) => new ActorId(type, this.GetNextOperationId(), name, this);

        /// <inheritdoc/>
        public virtual ActorId CreateActorIdFromName(Type type, string name) => new ActorId(type, 0, name, this, true);

        /// <inheritdoc/>
        public virtual ActorId CreateActor(Type type, Event initialEvent = null, EventGroup eventGroup = null) =>
            this.CreateActor(null, type, null, initialEvent, null, eventGroup);

        /// <inheritdoc/>
        public virtual ActorId CreateActor(Type type, string name, Event initialEvent = null, EventGroup eventGroup = null) =>
            this.CreateActor(null, type, name, initialEvent, null, eventGroup);

        /// <inheritdoc/>
        public virtual ActorId CreateActor(ActorId id, Type type, Event initialEvent = null, EventGroup eventGroup = null) =>
            this.CreateActor(id, type, null, initialEvent, null, eventGroup);

        /// <inheritdoc/>
        public virtual Task<ActorId> CreateActorAndExecuteAsync(Type type, Event initialEvent = null,
            EventGroup eventGroup = null) =>
            this.CreateActorAndExecuteAsync(null, type, null, initialEvent, null, eventGroup);

        /// <inheritdoc/>
        public virtual Task<ActorId> CreateActorAndExecuteAsync(Type type, string name, Event initialEvent = null,
            EventGroup eventGroup = null) =>
            this.CreateActorAndExecuteAsync(null, type, name, initialEvent, null, eventGroup);

        /// <inheritdoc/>
        public virtual Task<ActorId> CreateActorAndExecuteAsync(ActorId id, Type type, Event initialEvent = null,
            EventGroup eventGroup = null) =>
            this.CreateActorAndExecuteAsync(id, type, null, initialEvent, null, eventGroup);

        /// <summary>
        /// Creates a new <see cref="Actor"/> of the specified <see cref="Type"/>.
        /// </summary>
        internal virtual ActorId CreateActor(ActorId id, Type type, string name, Event initialEvent, Actor creator,
            EventGroup eventGroup)
        {
            Actor actor = this.CreateActor(id, type, name, creator, eventGroup);
            if (actor is StateMachine)
            {
                this.LogManager.LogCreateStateMachine(actor.Id, creator?.Id.Name, creator?.Id.Type);
            }
            else
            {
                this.LogManager.LogCreateActor(actor.Id, creator?.Id.Name, creator?.Id.Type);
            }

            this.OnActorEventHandlerStarted(actor.Id);
            this.RunActorEventHandler(actor, initialEvent, true);
            return actor.Id;
        }

        /// <summary>
        /// Creates a new <see cref="Actor"/> of the specified <see cref="Type"/>. The method
        /// returns only when the actor is initialized and the <see cref="Event"/> (if any)
        /// is handled.
        /// </summary>
        internal virtual async Task<ActorId> CreateActorAndExecuteAsync(ActorId id, Type type, string name, Event initialEvent,
            Actor creator, EventGroup eventGroup)
        {
            Actor actor = this.CreateActor(id, type, name, creator, eventGroup);
            if (actor is StateMachine)
            {
                this.LogManager.LogCreateStateMachine(actor.Id, creator?.Id.Name, creator?.Id.Type);
            }
            else
            {
                this.LogManager.LogCreateActor(actor.Id, creator?.Id.Name, creator?.Id.Type);
            }

            this.OnActorEventHandlerStarted(actor.Id);
            await this.RunActorEventHandlerAsync(actor, initialEvent, true);
            return actor.Id;
        }

        /// <summary>
        /// Creates a new <see cref="Actor"/> of the specified <see cref="Type"/>.
        /// </summary>
        internal virtual Actor CreateActor(ActorId id, Type type, string name, Actor creator, EventGroup eventGroup)
        {
            if (!type.IsSubclassOf(typeof(Actor)))
            {
                this.Assert(false, "Type '{0}' is not an actor.", type.FullName);
            }

            if (id is null)
            {
                id = this.CreateActorId(type, name);
            }
            else if (id.Runtime != null && id.Runtime != this)
            {
                this.Assert(false, "Unbound actor id '{0}' was created by another runtime.", id.Value);
            }
            else if (id.Type != type.FullName)
            {
                this.Assert(false, "Cannot bind actor id '{0}' of type '{1}' to an actor of type '{2}'.",
                    id.Value, id.Type, type.FullName);
            }
            else
            {
                id.Bind(this);
            }

            // If no event group is provided then inherit the current group from the creator.
            if (eventGroup is null && creator != null)
            {
                eventGroup = creator.EventGroup;
            }

            Actor actor = ActorFactory.Create(type);
            ActorOperation op = this.Runtime.SchedulingPolicy is SchedulingPolicy.Fuzzing ?
                this.GetOrCreateActorOperation(id, actor) : null;
            IEventQueue eventQueue = new EventQueue(actor);
            actor.Configure(this, id, op, eventQueue, eventGroup);
            actor.SetupEventHandlers();

            if (!this.ActorMap.TryAdd(id, actor))
            {
                throw new InvalidOperationException($"An actor with id '{id.Value}' already exists.");
            }

            return actor;
        }

        /// <summary>
        /// Returns the operation for the specified actor id, or creates a new
        /// operation if it does not exist yet.
        /// </summary>
        protected ActorOperation GetOrCreateActorOperation(ActorId id, Actor actor)
        {
            var op = this.Runtime.GetOperationWithId<ActorOperation>(id.Value);
            return op ?? new ActorOperation(id.Value, id.Name, actor, this.Runtime);
        }

        /// <inheritdoc/>
        public virtual void SendEvent(ActorId targetId, Event initialEvent, EventGroup eventGroup = default, SendOptions options = null) =>
            this.SendEvent(targetId, initialEvent, null, eventGroup, options);

        /// <inheritdoc/>
        public virtual Task<bool> SendEventAndExecuteAsync(ActorId targetId, Event initialEvent,
            EventGroup eventGroup = null, SendOptions options = null) =>
            this.SendEventAndExecuteAsync(targetId, initialEvent, null, eventGroup, options);

        /// <summary>
        /// Sends an asynchronous <see cref="Event"/> to an actor.
        /// </summary>
        internal virtual void SendEvent(ActorId targetId, Event e, Actor sender, EventGroup eventGroup, SendOptions options)
        {
            EnqueueStatus enqueueStatus = this.EnqueueEvent(targetId, e, sender, eventGroup, out Actor target);
            if (enqueueStatus is EnqueueStatus.EventHandlerNotRunning)
            {
                this.OnActorEventHandlerStarted(target.Id);
                this.RunActorEventHandler(target, null, false);
            }
        }

        /// <summary>
        /// Sends an asynchronous <see cref="Event"/> to an actor. Returns immediately if the target was
        /// already running. Otherwise blocks until the target handles the event and reaches quiescence.
        /// </summary>
        internal virtual async Task<bool> SendEventAndExecuteAsync(ActorId targetId, Event e, Actor sender,
            EventGroup eventGroup, SendOptions options)
        {
            EnqueueStatus enqueueStatus = this.EnqueueEvent(targetId, e, sender, eventGroup, out Actor target);
            if (enqueueStatus is EnqueueStatus.EventHandlerNotRunning)
            {
                this.OnActorEventHandlerStarted(target.Id);
                await this.RunActorEventHandlerAsync(target, null, false);
                return true;
            }

            return enqueueStatus is EnqueueStatus.Dropped;
        }

        /// <summary>
        /// Enqueues an event to the actor with the specified id.
        /// </summary>
        private EnqueueStatus EnqueueEvent(ActorId targetId, Event e, Actor sender, EventGroup eventGroup, out Actor target)
        {
            if (e is null)
            {
                string message = sender != null ?
                    string.Format("{0} is sending a null event.", sender.Id.ToString()) :
                    "Cannot send a null event.";
                this.Assert(false, message);
            }

            if (targetId is null)
            {
                string message = (sender != null) ?
                    string.Format("{0} is sending event {1} to a null actor.", sender.Id.ToString(), e.ToString())
                    : string.Format("Cannot send event {0} to a null actor.", e.ToString());
                this.Assert(false, message);
            }

            if (this.Runtime.SchedulingPolicy is SchedulingPolicy.Fuzzing &&
                this.Runtime.TryGetExecutingOperation(out ControlledOperation current))
            {
                this.Runtime.DelayOperation(current);
            }

            target = this.GetActorWithId<Actor>(targetId);

            // If no group is provided we default to passing along the group from the sender.
            if (eventGroup is null && sender != null)
            {
                eventGroup = sender.EventGroup;
            }

            Guid opId = eventGroup is null ? Guid.Empty : eventGroup.Id;
            if (target is null || target.IsHalted)
            {
                this.LogManager.LogSendEvent(targetId, sender?.Id.Name, sender?.Id.Type,
                    (sender as StateMachine)?.CurrentStateName ?? default, e, opId, isTargetHalted: true);
                this.HandleDroppedEvent(e, targetId);
                return EnqueueStatus.Dropped;
            }

            this.LogManager.LogSendEvent(targetId, sender?.Id.Name, sender?.Id.Type,
                (sender as StateMachine)?.CurrentStateName ?? default, e, opId, isTargetHalted: false);

            EnqueueStatus enqueueStatus = target.Enqueue(e, eventGroup, null);
            if (enqueueStatus == EnqueueStatus.Dropped)
            {
                this.HandleDroppedEvent(e, targetId);
            }

            return enqueueStatus;
        }

        /// <summary>
        /// Runs a new asynchronous actor event handler.
        /// This is a fire and forget invocation.
        /// </summary>
        private void RunActorEventHandler(Actor actor, Event initialEvent, bool isFresh)
        {
            if (this.Runtime.SchedulingPolicy is SchedulingPolicy.Fuzzing)
            {
                this.Runtime.TaskFactory.StartNew(async state =>
                {
                    await this.RunActorEventHandlerAsync(actor, initialEvent, isFresh);
                },
                actor.Operation,
                default,
                this.Runtime.TaskFactory.CreationOptions | TaskCreationOptions.DenyChildAttach,
                this.Runtime.TaskFactory.Scheduler);
            }
            else
            {
                Task.Run(async () => await this.RunActorEventHandlerAsync(actor, initialEvent, isFresh));
            }
        }

        /// <summary>
        /// Runs a new asynchronous actor event handler.
        /// </summary>
        private async Task RunActorEventHandlerAsync(Actor actor, Event initialEvent, bool isFresh)
        {
            try
            {
                if (isFresh)
                {
                    await actor.InitializeAsync(initialEvent);
                }

                await actor.RunEventHandlerAsync();
            }
            catch (Exception ex)
            {
                this.Runtime.IsRunning = false;
                this.RaiseOnFailureEvent(ex);
            }
            finally
            {
                if (actor.IsHalted)
                {
                    this.ActorMap.TryRemove(actor.Id, out Actor _);
                    this.HandleActorHalted(actor.Id);
                }

                this.OnActorEventHandlerCompleted(actor.Id);
            }
        }

        /// <summary>
        /// Invoked when the event handler of the specified actor starts.
        /// </summary>
        protected void OnActorEventHandlerStarted(ActorId actorId)
        {
            if (this.Runtime.SchedulingPolicy != SchedulingPolicy.None ||
                this.Configuration.IsActorQuiescenceCheckingEnabledOutsideTesting)
            {
                lock (this.QuiescenceSyncObject)
                {
                    this.EnabledActors.Add(actorId);
                }
            }
        }

        /// <summary>
        /// Invoked when the event handler of the specified actor completes.
        /// </summary>
        protected void OnActorEventHandlerCompleted(ActorId actorId)
        {
            if (this.Runtime.SchedulingPolicy != SchedulingPolicy.None ||
                this.Configuration.IsActorQuiescenceCheckingEnabledOutsideTesting)
            {
                lock (this.QuiescenceSyncObject)
                {
                    this.EnabledActors.Remove(actorId);
                    if (this.IsActorQuiescenceAwaited && this.EnabledActors.Count is 0)
                    {
                        this.QuiescenceCompletionSource.TrySetResult(true);
                    }
                }
            }
        }

        /// <summary>
        /// Creates a new timer that sends a <see cref="TimerElapsedEvent"/> to its owner actor.
        /// </summary>
        internal virtual IActorTimer CreateActorTimer(TimerInfo info, Actor owner) => new ActorTimer(info, owner);

        /// <inheritdoc/>
        public virtual EventGroup GetCurrentEventGroup(ActorId currentActorId)
        {
            Actor actor = this.GetActorWithId<Actor>(currentActorId);
            return actor?.CurrentEventGroup;
        }

        /// <inheritdoc/>
        public ActorExecutionStatus GetActorExecutionStatus(ActorId id) => this.ActorMap.TryGetValue(id, out Actor actor) ?
            actor.ExecutionStatus : ActorExecutionStatus.None;

        /// <inheritdoc/>
        public IEnumerable<ActorId> GetCurrentActorIds() => this.ActorMap.Keys.ToList();

        /// <inheritdoc/>
        public IEnumerable<Type> GetCurrentActorTypes() => this.ActorMap.Values.Select(a => a.GetType()).Distinct();

        /// <inheritdoc/>
        public int GetCurrentActorCount() => this.IsRunning ? this.ActorMap.Count : 0;

        /// <summary>
        /// Gets the actor of type <typeparamref name="TActor"/> with the specified id,
        /// or null if no such actor exists.
        /// </summary>
        private TActor GetActorWithId<TActor>(ActorId id)
            where TActor : Actor =>
            id != null && this.ActorMap.TryGetValue(id, out Actor value) &&
            value is TActor actor ? actor : null;

        /// <summary>
        /// Returns the next available unique operation id.
        /// </summary>
        /// <returns>Value representing the next available unique operation id.</returns>
        private ulong GetNextOperationId() => this.Runtime.GetNextOperationId();

        /// <inheritdoc/>
        public bool RandomBoolean() => this.GetNondeterministicBooleanChoice(null, null);

        /// <summary>
        /// Returns a controlled nondeterministic boolean choice.
        /// </summary>
        internal virtual bool GetNondeterministicBooleanChoice(string callerName, string callerType) =>
            this.Runtime.GetNextNondeterministicBooleanChoice(callerName, callerType);

        /// <inheritdoc/>
        public int RandomInteger(int maxValue) => this.GetNondeterministicIntegerChoice(maxValue, null, null);

        /// <summary>
        /// Returns a controlled nondeterministic integer choice.
        /// </summary>
        internal virtual int GetNondeterministicIntegerChoice(int maxValue, string callerName, string callerType) =>
            this.Runtime.GetNextNondeterministicIntegerChoice(maxValue, callerName, callerType);

        /// <summary>
        /// Logs that the specified actor invoked an action.
        /// </summary>
        internal void LogInvokedAction(Actor actor, MethodInfo action, string handlingStateName, string currentStateName) =>
            this.LogManager.LogExecuteAction(actor.Id, handlingStateName, currentStateName, action.Name);

        /// <summary>
        /// Logs that the specified actor enqueued an <see cref="Event"/>.
        /// </summary>
        internal void LogEnqueuedEvent(Actor actor, Event e) => this.LogManager.LogEnqueueEvent(actor.Id, e);

        /// <summary>
        /// Logs that the specified actor dequeued an <see cref="Event"/>.
        /// </summary>
        internal virtual void LogDequeuedEvent(Actor actor, Event e, EventInfo eventInfo, bool isFreshDequeue)
        {
            string stateName = actor is StateMachine stateMachine ? stateMachine.CurrentStateName : null;
            this.LogManager.LogDequeueEvent(actor.Id, stateName, e);
        }

        /// <summary>
        /// Logs that the specified actor dequeued the default <see cref="Event"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal virtual void LogDefaultEventDequeued(Actor actor)
        {
        }

        /// <summary>
        /// Logs that the specified actor raised an <see cref="Event"/>.
        /// </summary>
        internal void LogRaisedEvent(Actor actor, Event e)
        {
            string stateName = actor is StateMachine stateMachine ? stateMachine.CurrentStateName : null;
            this.LogManager.LogRaiseEvent(actor.Id, stateName, e);
        }

        /// <summary>
        /// Logs that the specified actor is handling a raised <see cref="Event"/>.
        /// </summary>
        internal void LogHandleRaisedEvent(Actor actor, Event e)
        {
            string stateName = actor is StateMachine stateMachine ? stateMachine.CurrentStateName : null;
            this.LogManager.LogHandleRaisedEvent(actor.Id, stateName, e);
        }

        /// <summary>
        /// Logs that the specified actor is handling a raised <see cref="HaltEvent"/>.
        /// </summary>
        internal virtual void LogHandleHaltEvent(Actor actor, int inboxSize) => this.LogManager.LogHalt(actor.Id, inboxSize);

        /// <summary>
        /// Logs that the specified actor called <see cref="Actor.ReceiveEventAsync(Type[])"/>
        /// or one of its overloaded methods.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal virtual void LogReceiveCalled(Actor actor)
        {
        }

        /// <summary>
        /// Logs that the specified actor enqueued an event that it was waiting to receive.
        /// </summary>
        internal void LogReceivedEvent(Actor actor, Event e)
        {
            string stateName = actor is StateMachine stateMachine ? stateMachine.CurrentStateName : null;
            this.LogManager.LogReceiveEvent(actor.Id, stateName, e, wasBlocked: true);
        }

        /// <summary>
        /// Logs that the specified actor received an event without waiting because the event
        /// was already in the inbox when the actor invoked the receive statement.
        /// </summary>
        internal virtual void LogReceivedEventWithoutWaiting(Actor actor, Event e)
        {
            string stateName = actor is StateMachine stateMachine ? stateMachine.CurrentStateName : null;
            this.LogManager.LogReceiveEvent(actor.Id, stateName, e, wasBlocked: false);
        }

        /// <summary>
        /// Logs that the specified actor is waiting to receive an event of one of the specified types.
        /// </summary>
        internal virtual void LogWaitEvent(Actor actor, IEnumerable<Type> eventTypes)
        {
            string stateName = actor is StateMachine stateMachine ? stateMachine.CurrentStateName : null;
            if (eventTypes.Skip(1).Any())
            {
                this.LogManager.LogWaitEvent(actor.Id, stateName, eventTypes.ToArray());
            }
            else
            {
                this.LogManager.LogWaitEvent(actor.Id, stateName, eventTypes.First());
            }
        }

        /// <summary>
        /// Logs that the event handler of the specified actor terminated.
        /// </summary>
        internal void LogEventHandlerTerminated(Actor actor, DequeueStatus dequeueStatus)
        {
            string stateName = actor is StateMachine stateMachine ? stateMachine.CurrentStateName : null;
            this.LogManager.LogEventHandlerTerminated(actor.Id, stateName, dequeueStatus);
        }

        /// <summary>
        /// Logs that the specified state machine entered a state.
        /// </summary>
        internal void LogEnteredState(StateMachine stateMachine) =>
            this.LogManager.LogStateTransition(stateMachine.Id, stateMachine.CurrentStateName, isEntry: true);

        /// <summary>
        /// Logs that the specified state machine exited a state.
        /// </summary>
        internal void LogExitedState(StateMachine stateMachine) =>
            this.LogManager.LogStateTransition(stateMachine.Id, stateMachine.CurrentStateName, isEntry: false);

        /// <summary>
        /// Logs that the specified state machine invoked pop.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal virtual void LogPopState(StateMachine stateMachine)
        {
        }

        /// <summary>
        /// Logs that the specified state machine invoked an action.
        /// </summary>
        internal void LogInvokedOnEntryAction(StateMachine stateMachine, MethodInfo action) =>
            this.LogManager.LogExecuteAction(stateMachine.Id, stateMachine.CurrentStateName,
                stateMachine.CurrentStateName, action.Name);

        /// <summary>
        /// Logs that the specified state machine invoked an action.
        /// </summary>
        internal void LogInvokedOnExitAction(StateMachine stateMachine, MethodInfo action) =>
            this.LogManager.LogExecuteAction(stateMachine.Id, stateMachine.CurrentStateName,
                stateMachine.CurrentStateName, action.Name);

        /// <inheritdoc/>
        CoverageInfo IRuntimeExtension.BuildCoverageInfo()
        {
            var result = this.CoverageInfo;
            if (result != null)
            {
                var builder = this.LogManager.GetLogsOfType<ActorRuntimeLogGraphBuilder>()
                    .FirstOrDefault(builder => builder.CollapseInstances);
                if (builder != null)
                {
                    result.CoverageGraph = builder.SnapshotGraph(false);
                }

                var eventCoverage = this.LogManager.GetLogsOfType<ActorRuntimeLogEventCoverage>().FirstOrDefault();
                if (eventCoverage != null)
                {
                    result.ActorEventInfo = eventCoverage.ActorEventCoverage;
                    result.MonitorEventInfo = eventCoverage.MonitorEventCoverage;
                }
            }

            return result;
        }

        /// <inheritdoc/>
        CoverageInfo IRuntimeExtension.GetCoverageInfo() => this.CoverageInfo;

        /// <inheritdoc/>
        CoverageGraph IRuntimeExtension.GetCoverageGraph()
        {
            CoverageGraph result = null;
            var builder = this.LogManager.GetLogsOfType<ActorRuntimeLogGraphBuilder>()
                .FirstOrDefault(builder => !builder.CollapseInstances);
            if (builder != null)
            {
                result = builder.SnapshotGraph(true);
            }

            return result;
        }

        /// <summary>
        /// Returns the program counter of the specified actor.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal virtual int GetActorProgramCounter(ActorId actorId) => 0;

        /// <inheritdoc/>
        public void RegisterMonitor<T>()
            where T : Monitor =>
            this.Runtime.RegisterMonitor<T>();

        /// <inheritdoc/>
        public void Monitor<T>(Monitor.Event e)
            where T : Monitor =>
            this.Runtime.Monitor<T>(e);

        /// <summary>
        /// Invokes the specified <see cref="Specifications.Monitor"/> with the specified <see cref="Event"/>.
        /// </summary>
        internal void InvokeMonitor(Type type, Monitor.Event e, string senderName, string senderType, string senderStateName) =>
            this.Runtime.InvokeMonitor(type, e, senderName, senderType, senderStateName);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Assert(bool predicate) => this.Runtime.Assert(predicate);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Assert(bool predicate, string s, object arg0) => this.Runtime.Assert(predicate, s, arg0);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Assert(bool predicate, string s, object arg0, object arg1) => this.Runtime.Assert(predicate, s, arg0, arg1);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Assert(bool predicate, string s, object arg0, object arg1, object arg2) =>
            this.Runtime.Assert(predicate, s, arg0, arg1, arg2);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Assert(bool predicate, string s, params object[] args) => this.Runtime.Assert(predicate, s, args);

        /// <summary>
        /// Asserts that the actor calling an actor method is also the actor that is currently executing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal virtual void AssertExpectedCallerActor(Actor caller, string calledAPI)
        {
        }

        /// <summary>
        /// Raises the <see cref="OnFailure"/> event with the specified <see cref="Exception"/>.
        /// </summary>
        internal void RaiseOnFailureEvent(Exception exception) => this.Runtime.RaiseOnFailureEvent(exception);

        /// <summary>
        /// Handle the halted actor with the specified <see cref="ActorId"/>.
        /// </summary>
        internal void HandleActorHalted(ActorId id) => this.OnActorHalted?.Invoke(id);

        /// <summary>
        /// Handle the specified dropped <see cref="Event"/>.
        /// </summary>
        internal void HandleDroppedEvent(Event e, ActorId id) => this.OnEventDropped?.Invoke(e, id);

        /// <summary>
        /// Throws an <see cref="AssertionFailureException"/> exception containing the specified exception.
        /// </summary>
#if !DEBUG
        [DebuggerHidden]
#endif
        internal void WrapAndThrowException(Exception exception, string s, params object[] args) =>
            this.Runtime.WrapAndThrowException(exception, s, args);

        /// <inheritdoc/>
        public void RegisterLog(IRuntimeLog log) => this.LogManager.RegisterLog(log, this.LogWriter);

        /// <inheritdoc/>
        public void RemoveLog(IRuntimeLog log) => this.LogManager.RemoveLog(log);

        /// <inheritdoc/>
        Task IRuntimeExtension.WaitUntilQuiescenceAsync()
        {
            lock (this.QuiescenceSyncObject)
            {
                if (this.EnabledActors.Count > 0)
                {
                    this.IsActorQuiescenceAwaited = true;
                    return this.QuiescenceCompletionSource.Task;
                }
                else
                {
                    return Task.CompletedTask;
                }
            }
        }

        /// <inheritdoc/>
        public void Stop() => this.Runtime.Stop();

        /// <summary>
        /// Disposes runtime resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.ActorMap.Clear();
                this.EnabledActors.Clear();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The mocked execution context of an actor program.
        /// </summary>
        internal sealed class Mock : ActorExecutionContext
        {
            /// <summary>
            /// Set of all created actor ids.
            /// </summary>
            private readonly ConcurrentDictionary<ActorId, byte> ActorIds;

            /// <summary>
            /// Map that stores all unique names and their corresponding actor ids.
            /// </summary>
            private readonly ConcurrentDictionary<string, ActorId> NameValueToActorId;

            /// <summary>
            /// Map of program counters used for state-caching to distinguish
            /// scheduling from non-deterministic choices.
            /// </summary>
            private readonly ConcurrentDictionary<ActorId, int> ProgramCounterMap;

            /// <summary>
            /// If true, the actor execution is controlled, else false.
            /// </summary>
            internal override bool IsExecutionControlled => true;

            /// <summary>
            /// Initializes a new instance of the <see cref="Mock"/> class.
            /// </summary>
            internal Mock(Configuration configuration, ActorLogManager logManager)
                : base(configuration, logManager)
            {
                this.ActorIds = new ConcurrentDictionary<ActorId, byte>();
                this.NameValueToActorId = new ConcurrentDictionary<string, ActorId>();
                this.ProgramCounterMap = new ConcurrentDictionary<ActorId, int>();
            }

            /// <inheritdoc/>
            public override ActorId CreateActorIdFromName(Type type, string name)
            {
                // It is important that all actor ids use the monotonically incrementing
                // value as the id during testing, and not the unique name.
                var id = this.NameValueToActorId.GetOrAdd(name, key => this.CreateActorId(type, key));
                this.ActorIds.TryAdd(id, 0);
                return id;
            }

            /// <inheritdoc/>
            public override ActorId CreateActor(Type type, Event initialEvent = null, EventGroup eventGroup = null) =>
                this.CreateActor(null, type, null, initialEvent, eventGroup);

            /// <inheritdoc/>
            public override ActorId CreateActor(Type type, string name, Event initialEvent = null, EventGroup eventGroup = null) =>
                this.CreateActor(null, type, name, initialEvent, eventGroup);

            /// <inheritdoc/>
            public override ActorId CreateActor(ActorId id, Type type, Event initialEvent = null, EventGroup eventGroup = null)
            {
                this.Assert(id != null, "Cannot create an actor using a null actor id.");
                return this.CreateActor(id, type, null, initialEvent, eventGroup);
            }

            /// <inheritdoc/>
            public override Task<ActorId> CreateActorAndExecuteAsync(Type type, Event initialEvent = null, EventGroup eventGroup = null) =>
                this.CreateActorAndExecuteAsync(null, type, null, initialEvent, eventGroup);

            /// <inheritdoc/>
            public override Task<ActorId> CreateActorAndExecuteAsync(Type type, string name, Event initialEvent = null, EventGroup eventGroup = null) =>
                this.CreateActorAndExecuteAsync(null, type, name, initialEvent, eventGroup);

            /// <inheritdoc/>
            public override Task<ActorId> CreateActorAndExecuteAsync(ActorId id, Type type, Event initialEvent = null, EventGroup eventGroup = null)
            {
                this.Assert(id != null, "Cannot create an actor using a null actor id.");
                return this.CreateActorAndExecuteAsync(id, type, null, initialEvent, eventGroup);
            }

            /// <summary>
            /// Creates a new actor of the specified <see cref="Type"/> and name, using the specified
            /// unbound actor id, and passes the specified optional <see cref="Event"/>. This event
            /// can only be used to access its payload, and cannot be handled.
            /// </summary>
            internal ActorId CreateActor(ActorId id, Type type, string name, Event initialEvent = null, EventGroup eventGroup = null)
            {
                var creatorOp = this.Runtime.GetExecutingOperation<ActorOperation>();
                return this.CreateActor(id, type, name, initialEvent, creatorOp?.Actor, eventGroup);
            }

            /// <summary>
            /// Creates a new <see cref="Actor"/> of the specified <see cref="Type"/>.
            /// </summary>
            internal override ActorId CreateActor(ActorId id, Type type, string name, Event initialEvent, Actor creator, EventGroup eventGroup)
            {
                this.AssertExpectedCallerActor(creator, "CreateActor");
                Actor actor = this.CreateActor(id, type, name, creator, eventGroup);
                this.OnActorEventHandlerStarted(actor.Id);
                this.RunActorEventHandler(actor, initialEvent, true, null);
                return actor.Id;
            }

            /// <summary>
            /// Creates a new actor of the specified <see cref="Type"/> and name, using the specified
            /// unbound actor id, and passes the specified optional <see cref="Event"/>. This event
            /// can only be used to access its payload, and cannot be handled. The method returns only
            /// when the actor is initialized and the <see cref="Event"/> (if any) is handled.
            /// </summary>
            internal Task<ActorId> CreateActorAndExecuteAsync(ActorId id, Type type, string name, Event initialEvent = null,
                EventGroup eventGroup = null)
            {
                var creatorOp = this.Runtime.GetExecutingOperation<ActorOperation>();
                return this.CreateActorAndExecuteAsync(id, type, name, initialEvent, creatorOp?.Actor, eventGroup);
            }

            /// <summary>
            /// Creates a new <see cref="Actor"/> of the specified <see cref="Type"/>. The method
            /// returns only when the actor is initialized and the <see cref="Event"/> (if any)
            /// is handled.
            /// </summary>
            internal override async Task<ActorId> CreateActorAndExecuteAsync(ActorId id, Type type, string name, Event initialEvent,
                Actor creator, EventGroup eventGroup)
            {
                this.AssertExpectedCallerActor(creator, "CreateActorAndExecuteAsync");
                this.Assert(creator != null, "Only an actor can call 'CreateActorAndExecuteAsync': avoid calling " +
                    "it directly from the test method; instead call it through a test driver actor.");

                Actor actor = this.CreateActor(id, type, name, creator, eventGroup);
                this.OnActorEventHandlerStarted(actor.Id);
                this.RunActorEventHandler(actor, initialEvent, true, creator);

                // Wait until the actor reaches quiescence.
                await creator.ReceiveEventAsync(typeof(QuiescentEvent), rev => (rev as QuiescentEvent).ActorId == actor.Id);
                return await Task.FromResult(actor.Id);
            }

            /// <summary>
            /// Creates a new <see cref="Actor"/> of the specified <see cref="Type"/>.
            /// </summary>
            internal override Actor CreateActor(ActorId id, Type type, string name, Actor creator, EventGroup eventGroup)
            {
                this.Assert(type.IsSubclassOf(typeof(Actor)), "Type '{0}' is not an actor.", type.FullName);

                // Using ulong.MaxValue because a Create operation cannot specify
                // the id of its target, because the id does not exist yet.
                this.Runtime.ScheduleNextOperation(creator?.Operation, SchedulingPointType.Create);
                this.ResetProgramCounter(creator);

                if (id is null)
                {
                    id = this.CreateActorId(type, name);
                    this.ActorIds.TryAdd(id, 0);
                }
                else
                {
                    if (this.ActorMap.ContainsKey(id))
                    {
                        throw new InvalidOperationException($"An actor with id '{id.Value}' already exists.");
                    }

                    this.Assert(id.Runtime is null || id.Runtime == this, "Unbound actor id '{0}' was created by another runtime.", id.Value);
                    this.Assert(id.Type == type.FullName, "Cannot bind actor id '{0}' of type '{1}' to an actor of type '{2}'.",
                        id.Value, id.Type, type.FullName);
                    id.Bind(this);
                }

                // If a group was not provided, inherit the current event group from the creator (if any).
                if (eventGroup is null && creator != null)
                {
                    eventGroup = creator.EventGroup;
                }

                Actor actor = ActorFactory.Create(type);
                ActorOperation op = this.GetOrCreateActorOperation(id, actor);
                IEventQueue eventQueue = new MockEventQueue(actor);
                actor.Configure(this, id, op, eventQueue, eventGroup);
                actor.SetupEventHandlers();

                // This should always succeed, because it is either a new id or it has already passed
                // the assertion check, which still holds due to the schedule serialization during
                // systematic testing, but we still do the check defensively.
                if (!this.ActorMap.TryAdd(id, actor))
                {
                    throw new InvalidOperationException($"An actor with id '{id.Value}' already exists.");
                }

                if (this.Configuration.IsActivityCoverageReported)
                {
                    actor.ReportActivityCoverage(this.CoverageInfo);
                }

                if (actor is StateMachine)
                {
                    this.LogManager.LogCreateStateMachine(id, creator?.Id.Name, creator?.Id.Type);
                }
                else
                {
                    this.LogManager.LogCreateActor(id, creator?.Id.Name, creator?.Id.Type);
                }

                return actor;
            }

            /// <inheritdoc/>
            public override void SendEvent(ActorId targetId, Event initialEvent, EventGroup eventGroup = default, SendOptions options = null)
            {
                var senderOp = this.Runtime.GetExecutingOperation<ActorOperation>();
                this.SendEvent(targetId, initialEvent, senderOp?.Actor, eventGroup, options);
            }

            /// <inheritdoc/>
            public override Task<bool> SendEventAndExecuteAsync(ActorId targetId, Event initialEvent,
                EventGroup eventGroup = null, SendOptions options = null)
            {
                var senderOp = this.Runtime.GetExecutingOperation<ActorOperation>();
                return this.SendEventAndExecuteAsync(targetId, initialEvent, senderOp?.Actor, eventGroup, options);
            }

            /// <summary>
            /// Sends an asynchronous <see cref="Event"/> to an actor.
            /// </summary>
            internal override void SendEvent(ActorId targetId, Event e, Actor sender, EventGroup eventGroup, SendOptions options)
            {
                if (e is null)
                {
                    string message = sender != null ?
                        string.Format("{0} is sending a null event.", sender.Id.ToString()) :
                        "Cannot send a null event.";
                    this.Assert(false, message);
                }

                if (sender != null)
                {
                    this.Assert(targetId != null, "{0} is sending event {1} to a null actor.", sender.Id, e);
                }
                else
                {
                    this.Assert(targetId != null, "Cannot send event {1} to a null actor.", e);
                }

                this.AssertExpectedCallerActor(sender, "SendEvent");

                EnqueueStatus enqueueStatus = this.EnqueueEvent(targetId, e, sender, eventGroup, options, out Actor target);
                if (enqueueStatus is EnqueueStatus.EventHandlerNotRunning)
                {
                    this.OnActorEventHandlerStarted(target.Id);
                    this.RunActorEventHandler(target, null, false, null);
                }
            }

            /// <summary>
            /// Sends an asynchronous <see cref="Event"/> to an actor. Returns immediately if the target was
            /// already running. Otherwise blocks until the target handles the event and reaches quiescence.
            /// </summary>
            internal override async Task<bool> SendEventAndExecuteAsync(ActorId targetId, Event e, Actor sender,
                EventGroup eventGroup, SendOptions options)
            {
                this.Assert(sender is StateMachine, "Only an actor can call 'SendEventAndExecuteAsync': avoid " +
                    "calling it directly from the test method; instead call it through a test driver actor.");
                this.Assert(e != null, "{0} is sending a null event.", sender.Id);
                this.Assert(targetId != null, "{0} is sending event {1} to a null actor.", sender.Id, e);
                this.AssertExpectedCallerActor(sender, "SendEventAndExecuteAsync");
                EnqueueStatus enqueueStatus = this.EnqueueEvent(targetId, e, sender, eventGroup, options, out Actor target);
                if (enqueueStatus is EnqueueStatus.EventHandlerNotRunning)
                {
                    this.OnActorEventHandlerStarted(target.Id);
                    this.RunActorEventHandler(target, null, false, sender as StateMachine);
                    // Wait until the actor reaches quiescence.
                    await (sender as StateMachine).ReceiveEventAsync(typeof(QuiescentEvent), rev => (rev as QuiescentEvent).ActorId == targetId);
                    return true;
                }

                // EnqueueStatus.EventHandlerNotRunning is not returned by EnqueueEvent
                // (even when the actor was previously inactive) when the event e requires
                // no action by the actor (i.e., it implicitly handles the event).
                return enqueueStatus is EnqueueStatus.Dropped || enqueueStatus is EnqueueStatus.NextEventUnavailable;
            }

            /// <summary>
            /// Enqueues an event to the actor with the specified id.
            /// </summary>
            private EnqueueStatus EnqueueEvent(ActorId targetId, Event e, Actor sender, EventGroup eventGroup,
                SendOptions options, out Actor target)
            {
                target = this.Runtime.GetOperationWithId<ActorOperation>(targetId.Value)?.Actor;
                this.Assert(target != null,
                    "Cannot send event '{0}' to actor id '{1}' that is not bound to an actor instance.",
                    e.GetType().FullName, targetId.Value);

                this.Runtime.ScheduleNextOperation(sender?.Operation, SchedulingPointType.Send);
                this.ResetProgramCounter(sender as StateMachine);

                // If no group is provided we default to passing along the group from the sender.
                if (eventGroup is null && sender != null)
                {
                    eventGroup = sender.EventGroup;
                }

                if (target.IsHalted)
                {
                    Guid groupId = eventGroup is null ? Guid.Empty : eventGroup.Id;
                    this.LogManager.LogSendEvent(targetId, sender?.Id.Name, sender?.Id.Type,
                        (sender as StateMachine)?.CurrentStateName ?? default, e, groupId, isTargetHalted: true);
                    this.Assert(options is null || !options.MustHandle,
                        "A must-handle event '{0}' was sent to {1} which has halted.", e.GetType().FullName, targetId);
                    this.HandleDroppedEvent(e, targetId);
                    return EnqueueStatus.Dropped;
                }

                EnqueueStatus enqueueStatus = this.EnqueueEvent(target, e, sender, eventGroup, options);
                if (enqueueStatus == EnqueueStatus.Dropped)
                {
                    this.HandleDroppedEvent(e, targetId);
                }

                return enqueueStatus;
            }

            /// <summary>
            /// Enqueues an event to the actor with the specified id.
            /// </summary>
            private EnqueueStatus EnqueueEvent(Actor actor, Event e, Actor sender, EventGroup eventGroup, SendOptions options)
            {
                EventOriginInfo originInfo;

                string stateName = null;
                if (sender is StateMachine senderStateMachine)
                {
                    originInfo = new EventOriginInfo(sender.Id, senderStateMachine.GetType().FullName,
                        NameResolver.GetStateNameForLogging(senderStateMachine.CurrentState));
                    stateName = senderStateMachine.CurrentStateName;
                }
                else if (sender is Actor senderActor)
                {
                    originInfo = new EventOriginInfo(sender.Id, senderActor.GetType().FullName, string.Empty);
                }
                else
                {
                    // Message comes from the environment.
                    originInfo = new EventOriginInfo(null, "Env", "Env");
                }

                EventInfo eventInfo = new EventInfo(e, originInfo)
                {
                    MustHandle = options?.MustHandle ?? false,
                    Assert = options?.Assert ?? -1
                };

                Guid opId = eventGroup is null ? Guid.Empty : eventGroup.Id;
                this.LogManager.LogSendEvent(actor.Id, sender?.Id.Name, sender?.Id.Type, stateName,
                    e, opId, isTargetHalted: false);
                return actor.Enqueue(e, eventGroup, eventInfo);
            }

            /// <summary>
            /// Runs a new asynchronous event handler for the specified actor.
            /// This is a fire and forget invocation.
            /// </summary>
            /// <param name="actor">The actor that executes this event handler.</param>
            /// <param name="initialEvent">Optional event for initializing the actor.</param>
            /// <param name="isFresh">If true, then this is a new actor.</param>
            /// <param name="syncCaller">Caller actor that is blocked for quiescence.</param>
            private void RunActorEventHandler(Actor actor, Event initialEvent, bool isFresh, Actor syncCaller)
            {
                this.Runtime.TaskFactory.StartNew(async state =>
                {
                    try
                    {
                        if (isFresh)
                        {
                            await actor.InitializeAsync(initialEvent);
                        }

                        await actor.RunEventHandlerAsync();
                        if (syncCaller != null)
                        {
                            this.EnqueueEvent(syncCaller, new QuiescentEvent(actor.Id), actor, actor.CurrentEventGroup, null);
                        }

                        if (!actor.IsHalted)
                        {
                            this.ResetProgramCounter(actor);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.Runtime.ProcessUnhandledExceptionInOperation(actor.Operation, ex);
                    }
                    finally
                    {
                        if (actor.IsHalted)
                        {
                            this.ActorMap.TryRemove(actor.Id, out Actor _);
                            this.HandleActorHalted(actor.Id);
                        }

                        this.OnActorEventHandlerCompleted(actor.Id);
                    }
                },
                actor.Operation,
                default,
                this.Runtime.TaskFactory.CreationOptions | TaskCreationOptions.DenyChildAttach,
                this.Runtime.TaskFactory.Scheduler);
            }

            /// <summary>
            /// Creates a new timer that sends a <see cref="TimerElapsedEvent"/> to its owner actor.
            /// </summary>
            internal override IActorTimer CreateActorTimer(TimerInfo info, Actor owner)
            {
                var id = this.CreateActorId(typeof(MockStateMachineTimer));
                this.CreateActor(id, typeof(MockStateMachineTimer), new TimerSetupEvent(info, owner, this.Configuration.TimeoutDelay));
                return this.Runtime.GetOperationWithId<ActorOperation>(id.Value).Actor as MockStateMachineTimer;
            }

            /// <inheritdoc/>
            public override EventGroup GetCurrentEventGroup(ActorId currentActorId)
            {
                var callerOp = this.Runtime.GetExecutingOperation<ActorOperation>();
                this.Assert(callerOp != null && currentActorId == callerOp.Actor.Id,
                    "Trying to access the event group id of {0}, which is not the currently executing actor.",
                    currentActorId);
                return callerOp.Actor.CurrentEventGroup;
            }

            /// <summary>
            /// Returns a controlled nondeterministic boolean choice.
            /// </summary>
            internal override bool GetNondeterministicBooleanChoice(string callerName, string callerType)
            {
                var caller = this.Runtime.GetExecutingOperation<ActorOperation>()?.Actor;
                if (caller is Actor callerActor)
                {
                    this.IncrementActorProgramCounter(callerActor.Id);
                }

                return this.Runtime.GetNextNondeterministicBooleanChoice(callerName ?? caller?.Id.Name, callerType ?? caller?.Id.Type);
            }

            /// <summary>
            /// Returns a controlled nondeterministic integer choice.
            /// </summary>
            internal override int GetNondeterministicIntegerChoice(int maxValue, string callerName, string callerType)
            {
                var caller = this.Runtime.GetExecutingOperation<ActorOperation>()?.Actor;
                if (caller is Actor callerActor)
                {
                    this.IncrementActorProgramCounter(callerActor.Id);
                }

                return this.Runtime.GetNextNondeterministicIntegerChoice(maxValue, callerName ?? caller?.Id.Name, callerType ?? caller?.Id.Type);
            }

            /// <inheritdoc/>
            internal override void LogDequeuedEvent(Actor actor, Event e, EventInfo eventInfo, bool isFreshDequeue)
            {
                if (!isFreshDequeue)
                {
                    // Skip the scheduling point, as this is the first dequeue of the event handler,
                    // to avoid unnecessary context switches.
                    this.Runtime.ScheduleNextOperation(actor.Operation, SchedulingPointType.Receive);
                    this.ResetProgramCounter(actor);
                }

                base.LogDequeuedEvent(actor, e, eventInfo, isFreshDequeue);
            }

            /// <inheritdoc/>
            internal override void LogDefaultEventDequeued(Actor actor)
            {
                this.Runtime.ScheduleNextOperation(actor.Operation, SchedulingPointType.Receive);
                this.ResetProgramCounter(actor);
            }

            /// <inheritdoc/>
            internal override void LogHandleHaltEvent(Actor actor, int inboxSize)
            {
                this.Runtime.ScheduleNextOperation(actor.Operation, SchedulingPointType.Halt);
                base.LogHandleHaltEvent(actor, inboxSize);
            }

            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal override void LogReceiveCalled(Actor actor) => this.AssertExpectedCallerActor(actor, "ReceiveEventAsync");

            /// <inheritdoc/>
            internal override void LogReceivedEventWithoutWaiting(Actor actor, Event e)
            {
                base.LogReceivedEventWithoutWaiting(actor, e);
                this.Runtime.ScheduleNextOperation(actor.Operation, SchedulingPointType.Receive);
                this.ResetProgramCounter(actor);
            }

            /// <inheritdoc/>
            internal override void LogWaitEvent(Actor actor, IEnumerable<Type> eventTypes)
            {
                base.LogWaitEvent(actor, eventTypes);
                this.Runtime.ScheduleNextOperation(actor.Operation, SchedulingPointType.Pause);
                this.ResetProgramCounter(actor);
            }

            /// <inheritdoc/>
            internal override void LogPopState(StateMachine stateMachine)
            {
                this.AssertExpectedCallerActor(stateMachine, "Pop");
                this.LogManager.LogPopState(stateMachine.Id, default, stateMachine.CurrentStateName);
            }

            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal override int GetActorProgramCounter(ActorId actorId) =>
                this.ProgramCounterMap.GetOrAdd(actorId, 0);

            /// <summary>
            /// Increments the program counter of the specified actor.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void IncrementActorProgramCounter(ActorId actorId) =>
                this.ProgramCounterMap.AddOrUpdate(actorId, 1, (id, value) => value + 1);

            /// <summary>
            /// Resets the program counter of the specified actor.
            /// </summary>
            private void ResetProgramCounter(Actor actor)
            {
                if (actor != null)
                {
                    this.ProgramCounterMap.AddOrUpdate(actor.Id, 0, (id, value) => 0);
                }
            }

            /// <inheritdoc/>
#if !DEBUG
            [DebuggerHidden]
#endif
            internal override void AssertExpectedCallerActor(Actor caller, string calledAPI)
            {
                if (caller is null)
                {
                    return;
                }

                var op = this.Runtime.GetExecutingOperation<ActorOperation>();
                if (op is null)
                {
                    return;
                }

                this.Assert(op.Actor.Equals(caller), "{0} invoked {1} on behalf of {2}.",
                    op.Actor.Id, calledAPI, caller.Id);
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.NameValueToActorId.Clear();
                    this.ProgramCounterMap.Clear();
                    foreach (var id in this.ActorIds)
                    {
                        // Unbind the runtime to avoid memory leaks if the user holds the id.
                        id.Key.Bind(null);
                    }

                    this.ActorIds.Clear();
                }

                base.Dispose(disposing);
            }
        }
    }
}
