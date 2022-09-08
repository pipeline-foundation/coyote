﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SystemCompiler = System.Runtime.CompilerServices;

namespace Microsoft.Coyote.Runtime.CompilerServices
{
    /// <summary>
    /// Represents a builder for asynchronous methods that return a controlled value task.
    /// This type is intended for compiler use only.
    /// </summary>
    /// <remarks>This type is intended for compiler use rather than use directly in code.</remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [StructLayout(LayoutKind.Auto)]
    public struct AsyncValueTaskMethodBuilder
    {
        /// <summary>
        /// Responsible for controlling the execution of tasks during systematic testing.
        /// </summary>
        private readonly CoyoteRuntime Runtime;

        /// <summary>
        /// The value task builder to which most operations are delegated.
        /// </summary>
#pragma warning disable IDE0044 // Add readonly modifier
        private SystemCompiler.AsyncValueTaskMethodBuilder MethodBuilder;
#pragma warning restore IDE0044 // Add readonly modifier

        /// <summary>
        /// Gets the value task for this builder.
        /// </summary>
        public ValueTask Task => this.MethodBuilder.Task;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncValueTaskMethodBuilder"/> struct.
        /// </summary>
        private AsyncValueTaskMethodBuilder(CoyoteRuntime runtime)
        {
            this.Runtime = runtime;
            this.MethodBuilder = default;
        }

        /// <summary>
        /// Creates an instance of the <see cref="AsyncValueTaskMethodBuilder"/> struct.
        /// </summary>
        public static AsyncValueTaskMethodBuilder Create()
        {
            RuntimeProvider.TryGetFromSynchronizationContext(out CoyoteRuntime runtime);
            return new AsyncValueTaskMethodBuilder(runtime);
        }

        /// <summary>
        /// Begins running the builder with the associated state machine.
        /// </summary>
        [DebuggerStepThrough]
        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            IO.Debug.WriteLine("[coyote::debug] Started state machine on runtime '{0}' and thread '{1}'.",
                this.Runtime?.Id, Thread.CurrentThread.ManagedThreadId);
            this.MethodBuilder.Start(ref stateMachine);
        }

        /// <summary>
        /// Associates the builder with the specified state machine.
        /// </summary>
        public void SetStateMachine(IAsyncStateMachine stateMachine) =>
            this.MethodBuilder.SetStateMachine(stateMachine);

        /// <summary>
        /// Marks the task as successfully completed.
        /// </summary>
        public void SetResult()
        {
            IO.Debug.WriteLine("[coyote::debug] Set state machine value task from thread '{0}'.",
                Thread.CurrentThread.ManagedThreadId);
            this.MethodBuilder.SetResult();
        }

        /// <summary>
        /// Marks the task as failed and binds the specified exception to the task.
        /// </summary>
        public void SetException(Exception exception) => this.MethodBuilder.SetException(exception);

        /// <summary>
        /// Schedules the state machine to proceed to the next action when the specified awaiter completes.
        /// </summary>
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            this.MethodBuilder.AwaitOnCompleted(ref awaiter, ref stateMachine);
            if (this.Runtime != null && awaiter is IControllableAwaiter controllableAwaiter &&
                controllableAwaiter.IsControlled)
            {
                var builderTask = this.MethodBuilder.Task;
                if (ValueTaskAwaiter.TryGetTask(ref builderTask, out Task innerTask))
                {
                    this.AssignStateMachineTask(innerTask);
                }
            }
        }

        /// <summary>
        /// Schedules the state machine to proceed to the next action when the specified awaiter completes.
        /// </summary>
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            this.MethodBuilder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
            if (this.Runtime != null && awaiter is IControllableAwaiter controllableAwaiter &&
                controllableAwaiter.IsControlled)
            {
                var builderTask = this.MethodBuilder.Task;
                if (ValueTaskAwaiter.TryGetTask(ref builderTask, out Task innerTask))
                {
                    this.AssignStateMachineTask(innerTask);
                }
            }
        }

        /// <summary>
        /// Assigns the state machine task with the runtime.
        /// </summary>
        private void AssignStateMachineTask(Task builderTask)
        {
            IO.Debug.WriteLine("[coyote::debug] Assigned state machine value task '{0}' from thread '{1}'.",
                builderTask.Id, Thread.CurrentThread.ManagedThreadId);
            this.Runtime.RegisterKnownControlledTask(builderTask);
        }
    }

    /// <summary>
    /// Represents a builder for asynchronous methods that return a <see cref="Task{TResult}"/>.
    /// This type is intended for compiler use only.
    /// </summary>
    /// <remarks>This type is intended for compiler use rather than use directly in code.</remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [StructLayout(LayoutKind.Auto)]
    public struct AsyncValueTaskMethodBuilder<TResult>
    {
        /// <summary>
        /// Responsible for controlling the execution of tasks during systematic testing.
        /// </summary>
        private readonly CoyoteRuntime Runtime;

        /// <summary>
        /// The value task builder to which most operations are delegated.
        /// </summary>
#pragma warning disable IDE0044 // Add readonly modifier
        private SystemCompiler.AsyncValueTaskMethodBuilder<TResult> MethodBuilder;
#pragma warning restore IDE0044 // Add readonly modifier

        /// <summary>
        /// Gets the value task for this builder.
        /// </summary>
        public ValueTask<TResult> Task => this.MethodBuilder.Task;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncValueTaskMethodBuilder{TResult}"/> struct.
        /// </summary>
        private AsyncValueTaskMethodBuilder(CoyoteRuntime runtime)
        {
            this.Runtime = runtime;
            this.MethodBuilder = default;
        }

        /// <summary>
        /// Creates an instance of the <see cref="AsyncValueTaskMethodBuilder{TResult}"/> struct.
        /// </summary>
#pragma warning disable CA1000 // Do not declare static members on generic types
        public static AsyncValueTaskMethodBuilder<TResult> Create()
        {
            RuntimeProvider.TryGetFromSynchronizationContext(out CoyoteRuntime runtime);
            return new AsyncValueTaskMethodBuilder<TResult>(runtime);
        }
#pragma warning restore CA1000 // Do not declare static members on generic types

        /// <summary>
        /// Begins running the builder with the associated state machine.
        /// </summary>
        [DebuggerStepThrough]
        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            IO.Debug.WriteLine("[coyote::debug] Started state machine on runtime '{0}' and thread '{1}'.",
                this.Runtime?.Id, Thread.CurrentThread.ManagedThreadId);
            this.MethodBuilder.Start(ref stateMachine);
        }

        /// <summary>
        /// Associates the builder with the specified state machine.
        /// </summary>
        public void SetStateMachine(IAsyncStateMachine stateMachine) =>
            this.MethodBuilder.SetStateMachine(stateMachine);

        /// <summary>
        /// Marks the task as successfully completed.
        /// </summary>
        /// <param name="result">The result to use to complete the task.</param>
        public void SetResult(TResult result)
        {
            IO.Debug.WriteLine("[coyote::debug] Set state machine value task from thread '{0}'.",
                Thread.CurrentThread.ManagedThreadId);
            this.MethodBuilder.SetResult(result);
        }

        /// <summary>
        /// Marks the task as failed and binds the specified exception to the task.
        /// </summary>
        public void SetException(Exception exception) => this.MethodBuilder.SetException(exception);

        /// <summary>
        /// Schedules the state machine to proceed to the next action when the specified awaiter completes.
        /// </summary>
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
                where TAwaiter : INotifyCompletion
                where TStateMachine : IAsyncStateMachine
        {
            this.MethodBuilder.AwaitOnCompleted(ref awaiter, ref stateMachine);
            if (this.Runtime != null && awaiter is IControllableAwaiter controllableAwaiter &&
                controllableAwaiter.IsControlled)
            {
                var builderTask = this.MethodBuilder.Task;
                if (ValueTaskAwaiter.TryGetTask<TResult>(ref builderTask, out Task<TResult> innerTask))
                {
                    this.AssignStateMachineTask(innerTask);
                }
            }
        }

        /// <summary>
        /// Schedules the state machine to proceed to the next action when the specified awaiter completes.
        /// </summary>
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            this.MethodBuilder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
            if (this.Runtime != null && awaiter is IControllableAwaiter controllableAwaiter &&
                controllableAwaiter.IsControlled)
            {
                var builderTask = this.MethodBuilder.Task;
                if (ValueTaskAwaiter.TryGetTask<TResult>(ref builderTask, out Task<TResult> innerTask))
                {
                    this.AssignStateMachineTask(innerTask);
                }
            }
        }

        /// <summary>
        /// Assigns the state machine task with the runtime.
        /// </summary>
        private void AssignStateMachineTask(Task<TResult> builderTask)
        {
            IO.Debug.WriteLine("[coyote::debug] Assigned state machine value task '{0}' from thread '{1}'.",
                builderTask.Id, Thread.CurrentThread.ManagedThreadId);
            this.Runtime.RegisterKnownControlledTask(builderTask);
        }
    }
}
