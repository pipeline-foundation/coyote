﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Coyote.Logging;

namespace Microsoft.Coyote.Actors.Tests
{
    internal class TestEventQueue : EventQueue
    {
        internal enum Notification
        {
            EnqueueEvent = 0,
            RaiseEvent,
            WaitEvent,
            ReceiveEvent,
            ReceiveEventWithoutWaiting,
            IgnoreEvent,
            DeferEvent,
            DropEvent
        }

        private readonly Action<Notification, Event, EventInfo> Notify;
        private readonly Type[] IgnoredEvents;
        private readonly Type[] DeferredEvents;
        private readonly bool IsDefaultHandlerInstalled;
        private readonly ILogger Logger;

        protected override bool IsEventHandlerRunning { get; set; }

        internal TestEventQueue(ILogger logger, Action<Notification, Event, EventInfo> notify,
            Type[] ignoredEvents = null, Type[] deferredEvents = null, bool isDefaultHandlerInstalled = false)
            : base(null)
        {
            this.Logger = logger;
            this.Notify = notify;
            this.IgnoredEvents = ignoredEvents ?? Array.Empty<Type>();
            this.DeferredEvents = deferredEvents ?? Array.Empty<Type>();
            this.IsDefaultHandlerInstalled = isDefaultHandlerInstalled;
            this.IsEventHandlerRunning = true;
        }

        protected override bool IsEventIgnored(Event e) => this.IgnoredEvents.Contains(e.GetType());

        protected override bool IsEventDeferred(Event e) => this.DeferredEvents.Contains(e.GetType());

        protected override bool IsDefaultHandlerAvailable() => this.IsDefaultHandlerInstalled;

        protected override void OnEnqueueEvent(Event e, EventGroup eventGroup, EventInfo eventInfo)
        {
            this.Logger.WriteLine("Enqueued event of type '{0}'.", e.GetType().FullName);
            this.Notify(Notification.EnqueueEvent, e, eventInfo);
        }

        protected override void OnRaiseEvent(Event e, EventGroup eventGroup, EventInfo eventInfo)
        {
            this.Logger.WriteLine("Raised event of type '{0}'.", e.GetType().FullName);
            this.Notify(Notification.RaiseEvent, e, eventInfo);
        }

        protected override void OnWaitEvent(IEnumerable<Type> eventTypes)
        {
            foreach (var type in eventTypes)
            {
                this.Logger.WriteLine("Waits to receive event of type '{0}'.", type.FullName);
            }

            this.Notify(Notification.WaitEvent, null, null);
        }

        protected override void OnReceiveEvent(Event e, EventGroup eventGroup, EventInfo eventInfo)
        {
            this.Logger.WriteLine("Received event of type '{0}'.", e.GetType().FullName);
            this.Notify(Notification.ReceiveEvent, e, eventInfo);
        }

        protected override void OnReceiveEventWithoutWaiting(Event e, EventGroup eventGroup, EventInfo eventInfo)
        {
            this.Logger.WriteLine("Received event of type '{0}' without waiting.", e.GetType().FullName);
            this.Notify(Notification.ReceiveEventWithoutWaiting, e, eventInfo);
        }

        protected override void OnIgnoreEvent(Event e, EventGroup eventGroup, EventInfo eventInfo)
        {
            this.Logger.WriteLine("Ignored event of type '{0}'.", e.GetType().FullName);
            this.Notify(Notification.IgnoreEvent, e, eventInfo);
        }

        protected override void OnDeferEvent(Event e, EventGroup eventGroup, EventInfo eventInfo)
        {
            this.Logger.WriteLine("Deferred event of type '{0}'.", e.GetType().FullName);
            this.Notify(Notification.DeferEvent, e, eventInfo);
        }

        protected override void OnDropEvent(Event e, EventGroup eventGroup, EventInfo eventInfo)
        {
            this.Logger.WriteLine("Dropped event of type '{0}'.", e.GetType().FullName);
            this.Notify(Notification.DropEvent, e, eventInfo);
        }
    }
}
