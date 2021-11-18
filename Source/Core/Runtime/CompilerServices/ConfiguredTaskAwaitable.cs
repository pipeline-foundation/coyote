﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SystemCompiler = System.Runtime.CompilerServices;

namespace Microsoft.Coyote.Runtime.CompilerServices
{
    /// <summary>
    /// Provides an awaitable object that is the outcome of invoking <see cref="Task.ConfigureAwait"/>.
    /// This type is intended for compiler use only.
    /// </summary>
    /// <remarks>This type is intended for compiler use rather than use directly in code.</remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public struct ConfiguredTaskAwaitable
    {
        /// <summary>
        /// The task awaiter.
        /// </summary>
        private readonly ConfiguredTaskAwaiter Awaiter;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfiguredTaskAwaitable"/> struct.
        /// </summary>
        internal ConfiguredTaskAwaitable(Task awaitedTask, bool continueOnCapturedContext)
        {
            this.Awaiter = new ConfiguredTaskAwaiter(awaitedTask, continueOnCapturedContext);
        }

        /// <summary>
        /// Returns an awaiter for this awaitable object.
        /// </summary>
        /// <returns>The awaiter.</returns>
        public ConfiguredTaskAwaiter GetAwaiter() => this.Awaiter;

        /// <summary>
        /// Provides an awaiter for an awaitable object. This type is intended for compiler use only.
        /// </summary>
        /// <remarks>This type is intended for compiler use rather than use directly in code.</remarks>
        public struct ConfiguredTaskAwaiter : ICriticalNotifyCompletion, INotifyCompletion
        {
            /// <summary>
            /// The task being awaited.
            /// </summary>
            private readonly Task AwaitedTask;

            /// <summary>
            /// The task awaiter.
            /// </summary>
            private readonly SystemCompiler.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter Awaiter;

            /// <summary>
            /// Gets a value that indicates whether the controlled task has completed.
            /// </summary>
            public bool IsCompleted => this.AwaitedTask.IsCompleted;

            /// <summary>
            /// Initializes a new instance of the <see cref="ConfiguredTaskAwaiter"/> struct.
            /// </summary>
            internal ConfiguredTaskAwaiter(Task awaitedTask, bool continueOnCapturedContext)
            {
                if (SynchronizationContext.Current is ControlledSynchronizationContext)
                {
                    // Force the continuation to run on the current context so that it can be controlled.
                    continueOnCapturedContext = true;
                }

                this.AwaitedTask = awaitedTask;
                this.Awaiter = awaitedTask.ConfigureAwait(continueOnCapturedContext).GetAwaiter();
            }

            /// <summary>
            /// Ends the await on the completed task.
            /// </summary>
            public void GetResult()
            {
                if (SynchronizationContext.Current is ControlledSynchronizationContext context)
                {
                    context.Runtime?.OnWaitTask(this.AwaitedTask);
                }

                this.Awaiter.GetResult();
            }

            /// <summary>
            /// Schedules the continuation action for the task associated with this awaiter.
            /// </summary>
            /// <param name="continuation">The action to invoke when the await operation completes.</param>
            public void OnCompleted(Action continuation) => this.Awaiter.OnCompleted(continuation);

            /// <summary>
            /// Schedules the continuation action for the task associated with this awaiter.
            /// </summary>
            /// <param name="continuation">The action to invoke when the await operation completes.</param>
            public void UnsafeOnCompleted(Action continuation) => this.Awaiter.UnsafeOnCompleted(continuation);
        }
    }

    /// <summary>
    /// Provides an awaitable object that enables configured awaits on a <see cref="Task{TResult}"/>.
    /// This type is intended for compiler use only.
    /// </summary>
    /// <remarks>This type is intended for compiler use rather than use directly in code.</remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public struct ConfiguredTaskAwaitable<TResult>
    {
        /// <summary>
        /// The task awaiter.
        /// </summary>
        private readonly ConfiguredTaskAwaiter Awaiter;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfiguredTaskAwaitable{TResult}"/> struct.
        /// </summary>
        internal ConfiguredTaskAwaitable(Task<TResult> awaitedTask, bool continueOnCapturedContext)
        {
            this.Awaiter = new ConfiguredTaskAwaiter(awaitedTask, continueOnCapturedContext);
        }

        /// <summary>
        /// Returns an awaiter for this awaitable object.
        /// </summary>
        /// <returns>The awaiter.</returns>
        public ConfiguredTaskAwaiter GetAwaiter() => this.Awaiter;

        /// <summary>
        /// Provides an awaiter for an awaitable object. This type is intended for compiler use only.
        /// </summary>
        /// <remarks>This type is intended for compiler use rather than use directly in code.</remarks>
        public struct ConfiguredTaskAwaiter : ICriticalNotifyCompletion, INotifyCompletion
        {
            /// <summary>
            /// The task being awaited.
            /// </summary>
            private readonly Task<TResult> AwaitedTask;

            /// <summary>
            /// The task awaiter.
            /// </summary>
            private readonly SystemCompiler.ConfiguredTaskAwaitable<TResult>.ConfiguredTaskAwaiter Awaiter;

            /// <summary>
            /// Gets a value that indicates whether the controlled task has completed.
            /// </summary>
            public bool IsCompleted => this.AwaitedTask.IsCompleted;

            /// <summary>
            /// Initializes a new instance of the <see cref="ConfiguredTaskAwaiter"/> struct.
            /// </summary>
            internal ConfiguredTaskAwaiter(Task<TResult> awaitedTask, bool continueOnCapturedContext)
            {
                if (SynchronizationContext.Current is ControlledSynchronizationContext)
                {
                    // Force the continuation to run on the current context so that it can be controlled.
                    continueOnCapturedContext = true;
                }

                this.AwaitedTask = awaitedTask;
                this.Awaiter = awaitedTask.ConfigureAwait(continueOnCapturedContext).GetAwaiter();
            }

            /// <summary>
            /// Ends the await on the completed task.
            /// </summary>
            public TResult GetResult()
            {
                if (SynchronizationContext.Current is ControlledSynchronizationContext context)
                {
                    context.Runtime?.OnWaitTask(this.AwaitedTask);
                }

                return this.Awaiter.GetResult();
            }

            /// <summary>
            /// Schedules the continuation action for the task associated with this awaiter.
            /// </summary>
            /// <param name="continuation">The action to invoke when the await operation completes.</param>
            public void OnCompleted(Action continuation) => this.Awaiter.OnCompleted(continuation);

            /// <summary>
            /// Schedules the continuation action for the task associated with this awaiter.
            /// </summary>
            /// <param name="continuation">The action to invoke when the await operation completes.</param>
            public void UnsafeOnCompleted(Action continuation) => this.Awaiter.UnsafeOnCompleted(continuation);
        }
    }
}
