﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;

namespace Microsoft.Coyote.Actors.SharedObjects
{
    /// <summary>
    /// A thread-safe counter that can be shared in-memory by actors.
    /// </summary>
    /// <remarks>
    /// See also <see href="/coyote/concepts/actors/sharing-objects">Sharing Objects</see>.
    /// </remarks>
    public class SharedCounter
    {
        /// <summary>
        /// The value of the shared counter.
        /// </summary>
        private volatile int Counter;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedCounter"/> class.
        /// </summary>
        private SharedCounter(int value)
        {
            this.Counter = value;
        }

        /// <summary>
        /// Creates a new shared counter.
        /// </summary>
        /// <param name="runtime">The actor runtime.</param>
        /// <param name="value">The initial value.</param>
        public static SharedCounter Create(IActorRuntime runtime, int value = 0)
        {
            if (runtime is ActorExecutionContext.Mock executionContext)
            {
                return new Mock(value, executionContext);
            }

            return new SharedCounter(value);
        }

        /// <summary>
        /// Increments the shared counter.
        /// </summary>
        public virtual void Increment()
        {
            Interlocked.Increment(ref this.Counter);
        }

        /// <summary>
        /// Decrements the shared counter.
        /// </summary>
        public virtual void Decrement()
        {
            Interlocked.Decrement(ref this.Counter);
        }

        /// <summary>
        /// Gets the current value of the shared counter.
        /// </summary>
        public virtual int GetValue() => this.Counter;

        /// <summary>
        /// Adds a value to the counter atomically.
        /// </summary>
        public virtual int Add(int value) => Interlocked.Add(ref this.Counter, value);

        /// <summary>
        /// Sets the counter to a value atomically.
        /// </summary>
        public virtual int Exchange(int value) => Interlocked.Exchange(ref this.Counter, value);

        /// <summary>
        /// Sets the counter to a value atomically if it is equal to a given value.
        /// </summary>
        public virtual int CompareExchange(int value, int comparand) =>
            Interlocked.CompareExchange(ref this.Counter, value, comparand);

        /// <summary>
        /// Mock implementation of <see cref="SharedCounter"/> that can be controlled during systematic testing.
        /// </summary>
        private sealed class Mock : SharedCounter
        {
            // TODO: port to the new resource API or controlled locks once we integrate actors with tasks.

            /// <summary>
            /// Actor modeling the shared counter.
            /// </summary>
            private readonly ActorId CounterActor;

            /// <summary>
            /// The execution context associated with this shared counter.
            /// </summary>
            private readonly ActorExecutionContext.Mock Context;

            /// <summary>
            /// Initializes a new instance of the <see cref="Mock"/> class.
            /// </summary>
            internal Mock(int value, ActorExecutionContext.Mock context)
                : base(value)
            {
                this.Context = context;
                this.CounterActor = context.CreateActor(typeof(SharedCounterActor));
                var op = context.Runtime.GetExecutingOperation<ActorOperation>();
                context.SendEvent(this.CounterActor, SharedCounterEvent.SetEvent(op.Actor.Id, value));
                op.Actor.ReceiveEventAsync(typeof(SharedCounterResponseEvent)).Wait();
            }

            /// <summary>
            /// Increments the shared counter.
            /// </summary>
            public override void Increment() =>
                this.Context.SendEvent(this.CounterActor, SharedCounterEvent.IncrementEvent());

            /// <summary>
            /// Decrements the shared counter.
            /// </summary>
            public override void Decrement() =>
                this.Context.SendEvent(this.CounterActor, SharedCounterEvent.DecrementEvent());

            /// <summary>
            /// Gets the current value of the shared counter.
            /// </summary>
            public override int GetValue()
            {
                var op = this.Context.Runtime.GetExecutingOperation<ActorOperation>();
                this.Context.SendEvent(this.CounterActor, SharedCounterEvent.GetEvent(op.Actor.Id));
                var response = op.Actor.ReceiveEventAsync(typeof(SharedCounterResponseEvent)).Result;
                return (response as SharedCounterResponseEvent).Value;
            }

            /// <summary>
            /// Adds a value to the counter atomically.
            /// </summary>
            public override int Add(int value)
            {
                var op = this.Context.Runtime.GetExecutingOperation<ActorOperation>();
                this.Context.SendEvent(this.CounterActor, SharedCounterEvent.AddEvent(op.Actor.Id, value));
                var response = op.Actor.ReceiveEventAsync(typeof(SharedCounterResponseEvent)).Result;
                return (response as SharedCounterResponseEvent).Value;
            }

            /// <summary>
            /// Sets the counter to a value atomically.
            /// </summary>
            public override int Exchange(int value)
            {
                var op = this.Context.Runtime.GetExecutingOperation<ActorOperation>();
                this.Context.SendEvent(this.CounterActor, SharedCounterEvent.SetEvent(op.Actor.Id, value));
                var response = op.Actor.ReceiveEventAsync(typeof(SharedCounterResponseEvent)).Result;
                return (response as SharedCounterResponseEvent).Value;
            }

            /// <summary>
            /// Sets the counter to a value atomically if it is equal to a given value.
            /// </summary>
            public override int CompareExchange(int value, int comparand)
            {
                var op = this.Context.Runtime.GetExecutingOperation<ActorOperation>();
                this.Context.SendEvent(this.CounterActor, SharedCounterEvent.CompareExchangeEvent(op.Actor.Id, value, comparand));
                var response = op.Actor.ReceiveEventAsync(typeof(SharedCounterResponseEvent)).Result;
                return (response as SharedCounterResponseEvent).Value;
            }
        }
    }
}
