﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using SystemCompiler = System.Runtime.CompilerServices;
using SystemTask = System.Threading.Tasks.Task;
using SystemTasks = System.Threading.Tasks;

namespace Microsoft.Coyote.Runtime.CompilerServices
{
    /// <summary>
    /// Provides an awaitable object that is the outcome of invoking <see cref="SystemTask.ConfigureAwait"/>.
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
        internal ConfiguredTaskAwaitable(SystemTask awaitedTask, bool continueOnCapturedContext)
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
        public struct ConfiguredTaskAwaiter : IControllableAwaiter, ICriticalNotifyCompletion, INotifyCompletion
        {
            /// <summary>
            /// The task being awaited.
            /// </summary>
            private readonly SystemTask AwaitedTask;

            /// <summary>
            /// The task awaiter.
            /// </summary>
            private readonly SystemCompiler.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter Awaiter;

            /// <summary>
            /// The runtime controlling this awaiter.
            /// </summary>
            private readonly CoyoteRuntime Runtime;

            /// <summary>
            /// Gets a value that indicates whether the controlled task has completed.
            /// </summary>
            public bool IsCompleted => this.AwaitedTask?.IsCompleted ?? this.Awaiter.IsCompleted;

            /// <inheritdoc/>
            bool IControllableAwaiter.IsControlled =>
                !this.Runtime?.IsTaskUncontrolled(this.AwaitedTask) ?? false;

            /// <summary>
            /// Initializes a new instance of the <see cref="ConfiguredTaskAwaiter"/> struct.
            /// </summary>
            internal ConfiguredTaskAwaiter(SystemTask awaitedTask, bool continueOnCapturedContext)
            {
                if (RuntimeProvider.TryGetFromSynchronizationContext(out CoyoteRuntime runtime))
                {
                    // Force the continuation to run on the current context so that it can be controlled.
                    continueOnCapturedContext = true;
                }

                this.AwaitedTask = awaitedTask;
                this.Awaiter = awaitedTask.ConfigureAwait(continueOnCapturedContext).GetAwaiter();
                this.Runtime = runtime;
            }

            /// <summary>
            /// Ends the await on the completed task.
            /// </summary>
            public void GetResult()
            {
                this.Runtime?.WaitUntilTaskCompletes(this.AwaitedTask);
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
    /// Provides an awaitable object that enables configured awaits on a <see cref="SystemTasks.Task{TResult}"/>.
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
        internal ConfiguredTaskAwaitable(SystemTasks.Task<TResult> awaitedTask, bool continueOnCapturedContext)
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
        public struct ConfiguredTaskAwaiter : IControllableAwaiter, ICriticalNotifyCompletion, INotifyCompletion
        {
            /// <summary>
            /// The task being awaited.
            /// </summary>
            private readonly SystemTasks.Task<TResult> AwaitedTask;

            /// <summary>
            /// The task awaiter.
            /// </summary>
            private readonly SystemCompiler.ConfiguredTaskAwaitable<TResult>.ConfiguredTaskAwaiter Awaiter;

            /// <summary>
            /// The runtime controlling this awaiter.
            /// </summary>
            private readonly CoyoteRuntime Runtime;

            /// <summary>
            /// Gets a value that indicates whether the controlled task has completed.
            /// </summary>
            public bool IsCompleted => this.AwaitedTask?.IsCompleted ?? this.Awaiter.IsCompleted;

            /// <inheritdoc/>
            bool IControllableAwaiter.IsControlled =>
                !this.Runtime?.IsTaskUncontrolled(this.AwaitedTask) ?? false;

            /// <summary>
            /// Initializes a new instance of the <see cref="ConfiguredTaskAwaiter"/> struct.
            /// </summary>
            internal ConfiguredTaskAwaiter(SystemTasks.Task<TResult> awaitedTask, bool continueOnCapturedContext)
            {
                if (RuntimeProvider.TryGetFromSynchronizationContext(out CoyoteRuntime runtime))
                {
                    // Force the continuation to run on the current context so that it can be controlled.
                    continueOnCapturedContext = true;
                }

                this.AwaitedTask = awaitedTask;
                this.Awaiter = awaitedTask.ConfigureAwait(continueOnCapturedContext).GetAwaiter();
                this.Runtime = runtime;
            }

            /// <summary>
            /// Ends the await on the completed task.
            /// </summary>
            public TResult GetResult()
            {
                this.Runtime?.WaitUntilTaskCompletes(this.AwaitedTask);
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
