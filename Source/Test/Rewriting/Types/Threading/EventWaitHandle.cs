﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Coyote.Runtime;
using SystemEventResetMode = System.Threading.EventResetMode;
using SystemEventWaitHandle = System.Threading.EventWaitHandle;
using SystemWaitHandle = System.Threading.WaitHandle;

namespace Microsoft.Coyote.Rewriting.Types.Threading
{
    /// <summary>
    /// Represents a thread synchronization event.
    /// </summary>
    /// <remarks>This type is intended for compiler use rather than use directly in code.</remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static class EventWaitHandle
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventWaitHandle"/> class, specifying whether the wait
        /// handle is initially signaled, and whether it resets automatically or manually.
        /// </summary>
        public static SystemEventWaitHandle Create(bool initialState, SystemEventResetMode mode) =>
            Create(initialState, mode, null, out _);

        /// <summary>
        /// Initializes a new instance of the <see cref="EventWaitHandle"/> class, specifying whether the wait
        /// handle is initially signaled if created as a result of this call, whether it resets automatically
        /// or manually, and the name of a system synchronization event.
        /// </summary>
        public static SystemEventWaitHandle Create(bool initialState, SystemEventResetMode mode, string name) =>
            Create(initialState, mode, name, out _);

        /// <summary>
        /// Initializes a new instance of the <see cref="EventWaitHandle"/> class, specifying whether the wait
        /// handle is initially signaled if created as a result of this call, whether it resets automatically
        /// or manually, the name of a system synchronization event, and a variable whose value after the call
        /// indicates whether the named system event was created.
        /// </summary>
        public static SystemEventWaitHandle Create(bool initialState, SystemEventResetMode mode, string name, out bool createdNew)
        {
            var instance = new SystemEventWaitHandle(initialState, mode, name, out createdNew);
            var runtime = CoyoteRuntime.Current;
            if (runtime.SchedulingPolicy is SchedulingPolicy.Interleaving)
            {
                Resource resource = new Resource(runtime, instance, initialState, mode);
                Resource.Add(resource);
            }

            return instance;
        }

        /// <summary>
        /// Sets the state of the event to signaled, allowing one or more waiting threads to proceed.
        /// </summary>
        public static bool Set(SystemEventWaitHandle instance)
        {
            var runtime = CoyoteRuntime.Current;
            if (runtime.SchedulingPolicy is SchedulingPolicy.Interleaving &&
                Resource.TryFind(instance, out WaitHandle.Resource baseResource) &&
                baseResource is Resource resource)
            {
                return resource.Set(runtime);
            }

            return instance.Set();
        }

        /// <summary>
        /// Sets the state of the event to non-signaled, causing threads to block.
        /// </summary>
        public static bool Reset(SystemEventWaitHandle instance)
        {
            var runtime = CoyoteRuntime.Current;
            if (runtime.SchedulingPolicy is SchedulingPolicy.Interleaving &&
                Resource.TryFind(instance, out WaitHandle.Resource baseResource) &&
                baseResource is Resource resource)
            {
                return resource.Reset(runtime);
            }

            return instance.Reset();
        }

        /// <summary>
        /// Resource that is used to control an <see cref="EventWaitHandle"/> during testing.
        /// </summary>
        internal class Resource : WaitHandle.Resource
        {
            /// <summary>
            /// The initial state of the handle.
            /// </summary>
            private readonly bool InitialState;

            /// <summary>
            /// The mode of the handle.
            /// </summary>
            private readonly SystemEventResetMode Mode;

            /// <summary>
            /// Initializes a new instance of the <see cref="Resource"/> class.
            /// </summary>
            internal Resource(CoyoteRuntime runtime, SystemWaitHandle handle, bool initialState, SystemEventResetMode mode)
                : base(runtime, handle, GetReleaseMode(mode), initialState)
            {
                this.InitialState = initialState;
                this.Mode = mode;
            }

            /// <summary>
            /// Sets the state of this resource to signaled, allowing any paused operation to resume executing.
            /// </summary>
            internal bool Set(CoyoteRuntime runtime)
            {
                using (runtime.EnterSynchronizedSection())
                {
                    this.CheckRuntime(runtime);
                    if (!runtime.TryGetExecutingOperation(out ControlledOperation current))
                    {
                        runtime.NotifyUncontrolledSynchronizationInvocation("EventWaitHandle.Set");
                    }

                    this.IsSignaled = true;
                    if (this.Mode is SystemEventResetMode.AutoReset)
                    {
                        this.SignalNext();
                    }
                    else
                    {
                        this.SignalAll();
                    }

                    return true;
                }
            }

            /// <summary>
            /// Resets the state of this resource to non-signaled.
            /// </summary>
            internal bool Reset(CoyoteRuntime runtime)
            {
                using (runtime.EnterSynchronizedSection())
                {
                    this.CheckRuntime(runtime);
                    if (!runtime.TryGetExecutingOperation(out ControlledOperation current))
                    {
                        runtime.NotifyUncontrolledSynchronizationInvocation("EventWaitHandle.Reset");
                    }
                    else if (runtime.Configuration.IsLockAccessRaceCheckingEnabled)
                    {
                        runtime.ScheduleNextOperation(current, SchedulingPointType.Interleave);
                    }

                    this.IsSignaled = false;
                    return true;
                }
            }

            /// <summary>
            /// Get the signal mode of this resource based on the specified <see cref="SystemEventResetMode"/>.
            /// </summary>
            private static WaitHandle.Resource.SignalMode GetReleaseMode(SystemEventResetMode mode) =>
                mode is SystemEventResetMode.AutoReset ?
                    WaitHandle.Resource.SignalMode.AutoResetSignal :
                    WaitHandle.Resource.SignalMode.None;
        }
    }
}
