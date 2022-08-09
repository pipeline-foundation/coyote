﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Coyote.Actors.Timers;
using Microsoft.Coyote.Runtime;

namespace Microsoft.Coyote.Actors
{
    /// <summary>
    /// Interface that allows an external module to track what is happening in the <see cref="IActorRuntime"/>.
    /// </summary>
    /// <remarks>
    /// See <see href="/coyote/concepts/actors/logging">Logging</see> for more information.
    /// </remarks>
    public interface IActorRuntimeLog : IRuntimeLog
    {
        /// <summary>
        /// Invoked when the specified actor has been created.
        /// </summary>
        /// <param name="id">The id of the actor that has been created.</param>
        /// <param name="creatorName">The name of the creator, or null.</param>
        /// <param name="creatorType">The type of the creator, or null.</param>
        void OnCreateActor(ActorId id, string creatorName, string creatorType);

        /// <summary>
        /// Invoked when the specified state machine has been created.
        /// </summary>
        /// <param name="id">The id of the state machine that has been created.</param>
        /// <param name="creatorName">The name of the creator, or null.</param>
        /// <param name="creatorType">The type of the creator, or null.</param>
        void OnCreateStateMachine(ActorId id, string creatorName, string creatorType);

        /// <summary>
        /// Invoked when the specified actor executes an action.
        /// </summary>
        /// <param name="id">The id of the actor executing the action.</param>
        /// <param name="handlingStateName">The state that declared this action (can be different from currentStateName in the case of pushed states.</param>
        /// <param name="currentStateName">The state name, if the actor is a state machine and a state exists, else null.</param>
        /// <param name="actionName">The name of the action being executed.</param>
        void OnExecuteAction(ActorId id, string handlingStateName, string currentStateName, string actionName);

        /// <summary>
        /// Invoked when the specified event is sent to a target actor.
        /// </summary>
        /// <param name="targetActorId">The id of the target actor.</param>
        /// <param name="senderName">The name of the sender, if any.</param>
        /// <param name="senderType">The type of the sender, if any.</param>
        /// <param name="senderStateName">The state name, if the sender is a state machine, else null.</param>
        /// <param name="e">The event being sent.</param>
        /// <param name="eventGroupId">The id used to identify the send operation.</param>
        /// <param name="isTargetHalted">Is the target actor halted.</param>
        void OnSendEvent(ActorId targetActorId, string senderName, string senderType, string senderStateName,
            Event e, Guid eventGroupId, bool isTargetHalted);

        /// <summary>
        /// Invoked when the specified state machine raises an event.
        /// </summary>
        /// <param name="id">The id of the actor raising the event.</param>
        /// <param name="stateName">The name of the current state.</param>
        /// <param name="e">The event being raised.</param>
        void OnRaiseEvent(ActorId id, string stateName, Event e);

        /// <summary>
        /// Invoked when the specified actor handled a raised event.
        /// </summary>
        /// <param name="id">The id of the actor handling the event.</param>
        /// <param name="stateName">The state name, if the actor is a state machine and a state exists, else null.</param>
        /// <param name="e">The event being handled.</param>
        void OnHandleRaisedEvent(ActorId id, string stateName, Event e);

        /// <summary>
        /// Invoked when the specified event is about to be enqueued to an actor.
        /// </summary>
        /// <param name="id">The id of the actor that the event is being enqueued to.</param>
        /// <param name="e">The event being enqueued.</param>
        void OnEnqueueEvent(ActorId id, Event e);

        /// <summary>
        /// Invoked when the specified event is dequeued by an actor.
        /// </summary>
        /// <param name="id">The id of the actor that the event is being dequeued by.</param>
        /// <param name="stateName">The state name, if the actor is a state machine and a state exists, else null.</param>
        /// <param name="e">The event being dequeued.</param>
        void OnDequeueEvent(ActorId id, string stateName, Event e);

        /// <summary>
        /// Invoked when the specified event is received by an actor.
        /// </summary>
        /// <param name="id">The id of the actor that received the event.</param>
        /// <param name="stateName">The state name, if the actor is a state machine and a state exists, else null.</param>
        /// <param name="e">The the event being received.</param>
        /// <param name="wasBlocked">The actor was waiting for one or more specific events,
        /// and <paramref name="e"/> was one of them.</param>
        void OnReceiveEvent(ActorId id, string stateName, Event e, bool wasBlocked);

        /// <summary>
        /// Invoked when the specified actor waits to receive an event of a specified type.
        /// </summary>
        /// <param name="id">The id of the actor that is entering the wait state.</param>
        /// <param name="stateName">The state name, if the actor is a state machine and a state exists, else null.</param>
        /// <param name="eventType">The type of the event being waited for.</param>
        void OnWaitEvent(ActorId id, string stateName, Type eventType);

        /// <summary>
        /// Invoked when the specified actor waits to receive an event of one of the specified types.
        /// </summary>
        /// <param name="id">The id of the actor that is entering the wait state.</param>
        /// <param name="stateName">The state name, if the actor is a state machine and a state exists, else null.</param>
        /// <param name="eventTypes">The types of the events being waited for, if any.</param>
        void OnWaitEvent(ActorId id, string stateName, params Type[] eventTypes);

        /// <summary>
        /// Invoked when the specified state machine enters or exits a state.
        /// </summary>
        /// <param name="id">The id of the actor entering or exiting the state.</param>
        /// <param name="stateName">The name of the state being entered or exited.</param>
        /// <param name="isEntry">If true, this is called for a state entry; otherwise, exit.</param>
        void OnStateTransition(ActorId id, string stateName, bool isEntry);

        /// <summary>
        /// Invoked when the specified state machine performs a goto transition to the specified state.
        /// </summary>
        /// <param name="id">The id of the actor.</param>
        /// <param name="currentStateName">The name of the current state.</param>
        /// <param name="newStateName">The target state of the transition.</param>
        void OnGotoState(ActorId id, string currentStateName, string newStateName);

        /// <summary>
        /// Invoked when the specified state machine is being pushed to a state.
        /// </summary>
        /// <param name="id">The id of the actor being pushed to the state.</param>
        /// <param name="currentStateName">The name of the current state.</param>
        /// <param name="newStateName">The target state of the transition.</param>
        void OnPushState(ActorId id, string currentStateName, string newStateName);

        /// <summary>
        /// Invoked when the specified state machine has popped its current state.
        /// </summary>
        /// <param name="id">The id of the actor that the pop executed in.</param>
        /// <param name="currentStateName">The name of the current state.</param>
        /// <param name="restoredStateName">The name of the state being re-entered, if any.</param>
        void OnPopState(ActorId id, string currentStateName, string restoredStateName);

        /// <summary>
        /// Invoked when the specified actor is idle (there is nothing to dequeue) and the default
        /// event handler is about to be executed.
        /// </summary>
        /// <param name="id">The id of the actor that the state will execute in.</param>
        /// <param name="stateName">The state name, if the actor is a state machine and a state exists, else null.</param>
        void OnDefaultEventHandler(ActorId id, string stateName);

        /// <summary>
        /// Invoked when the event handler of the specified actor terminated.
        /// </summary>
        /// <param name="id">The id of the actor with the handler that terminated.</param>
        /// <param name="stateName">The state name, if the actor is a state machine and a state exists, else null.</param>
        /// <param name="dequeueStatus">The status returned as the result of the last dequeue operation.</param>
        void OnEventHandlerTerminated(ActorId id, string stateName, DequeueStatus dequeueStatus);

        /// <summary>
        /// Invoked when the specified actor has been halted.
        /// </summary>
        /// <param name="id">The id of the actor that has been halted.</param>
        /// <param name="inboxSize">Approximate size of the inbox.</param>
        void OnHalt(ActorId id, int inboxSize);

        /// <summary>
        /// Invoked when the specified event cannot be handled in the current state, its exit
        /// handler is executed and then the state is popped and any previous "current state"
        /// is reentered. This handler is called when that pop has been done.
        /// </summary>
        /// <param name="id">The id of the actor that the pop executed in.</param>
        /// <param name="stateName">The state name, if the actor is a state machine and a state exists, else null.</param>
        /// <param name="e">The event that cannot be handled.</param>
        void OnPopStateUnhandledEvent(ActorId id, string stateName, Event e);

        /// <summary>
        /// Invoked when the specified actor throws an exception without handling it.
        /// </summary>
        /// <param name="id">The id of the actor that threw the exception.</param>
        /// <param name="stateName">The state name, if the actor is a state machine and a state exists, else null.</param>
        /// <param name="actionName">The name of the action being executed.</param>
        /// <param name="ex">The exception.</param>
        void OnExceptionThrown(ActorId id, string stateName, string actionName, Exception ex);

        /// <summary>
        /// Invoked when the specified actor has handled a thrown exception.
        /// </summary>
        /// <param name="id">The id of the actor that handled the exception.</param>
        /// <param name="stateName">The state name, if the actor is a state machine and a state exists, else null.</param>
        /// <param name="actionName">The name of the action being executed.</param>
        /// <param name="ex">The exception.</param>
        void OnExceptionHandled(ActorId id, string stateName, string actionName, Exception ex);

        /// <summary>
        /// Invoked when the specified actor timer has been created.
        /// </summary>
        /// <param name="info">Handle that contains information about the timer.</param>
        void OnCreateTimer(TimerInfo info);

        /// <summary>
        /// Invoked when the specified actor timer has been stopped.
        /// </summary>
        /// <param name="info">Handle that contains information about the timer.</param>
        void OnStopTimer(TimerInfo info);
    }
}
