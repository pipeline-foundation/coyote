﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Coyote.Logging;
using Microsoft.Coyote.Specifications;

namespace Microsoft.Coyote.Runtime
{
    /// <summary>
    /// Interface that exposes base runtime methods for Coyote.
    /// </summary>
    public interface ICoyoteRuntime : IDisposable
    {
        /// <summary>
        /// Gets or sets an <see cref="ILogger"/> for logging runtime messages.
        /// </summary>
        /// <remarks>
        /// See <see href="/coyote/concepts/actors/logging">Logging</see> for more information.
        /// </remarks>
        ILogger Logger { get; set; }

        /// <summary>
        /// Callback that is fired when an exception is thrown that includes failed assertions.
        /// </summary>
        event OnFailureHandler OnFailure;

        /// <summary>
        /// Registers a new specification monitor of the specified <see cref="Type"/>.
        /// </summary>
        /// <typeparam name="T">Type of the monitor.</typeparam>
        void RegisterMonitor<T>()
            where T : Monitor;

        /// <summary>
        /// Invokes the specified monitor with the specified <see cref="Monitor.Event"/>.
        /// </summary>
        /// <typeparam name="T">Type of the monitor.</typeparam>
        /// <param name="e">Event to send to the monitor.</param>
        void Monitor<T>(Monitor.Event e)
            where T : Monitor;

        /// <summary>
        /// Returns a nondeterministic boolean choice, that can be controlled during testing.
        /// </summary>
        /// <returns>The nondeterministic boolean choice.</returns>
        /// <remarks>
        /// See <see href="/coyote/concepts/non-determinism">Program non-determinism</see>
        /// for more information.
        /// </remarks>
        bool RandomBoolean();

        /// <summary>
        /// Returns a nondeterministic integer choice, that can be controlled during testing.
        /// The value is used to generate an integer in the range [0..maxValue).
        /// </summary>
        /// <param name="maxValue">The max value.</param>
        /// <returns>The nondeterministic integer choice.</returns>
        /// <remarks>
        /// See <see href="/coyote/concepts/non-determinism">Program non-determinism</see>
        /// for more information.
        /// </remarks>
        int RandomInteger(int maxValue);

        /// <summary>
        /// Checks if the assertion holds, and if not, throws an <see cref="AssertionFailureException"/> exception.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        void Assert(bool predicate);

        /// <summary>
        /// Checks if the assertion holds, and if not, throws an <see cref="AssertionFailureException"/> exception.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        /// <param name="s">The message to print if the assertion fails.</param>
        /// <param name="arg0">The first argument.</param>
        void Assert(bool predicate, string s, object arg0);

        /// <summary>
        /// Checks if the assertion holds, and if not, throws an <see cref="AssertionFailureException"/> exception.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        /// <param name="s">The message to print if the assertion fails.</param>
        /// <param name="arg0">The first argument.</param>
        /// <param name="arg1">The second argument.</param>
        void Assert(bool predicate, string s, object arg0, object arg1);

        /// <summary>
        /// Checks if the assertion holds, and if not, throws an <see cref="AssertionFailureException"/> exception.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        /// <param name="s">The message to print if the assertion fails.</param>
        /// <param name="arg0">The first argument.</param>
        /// <param name="arg1">The second argument.</param>
        /// <param name="arg2">The third argument.</param>
        void Assert(bool predicate, string s, object arg0, object arg1, object arg2);

        /// <summary>
        /// Checks if the assertion holds, and if not, throws an <see cref="AssertionFailureException"/> exception.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        /// <param name="s">The message to print if the assertion fails.</param>
        /// <param name="args">The message arguments.</param>
        void Assert(bool predicate, string s, params object[] args);

        /// <summary>
        /// Use this method to register an <see cref="IRuntimeLog"/>.
        /// </summary>
        /// <param name="log">The log writer to register.</param>
        void RegisterLog(IRuntimeLog log);

        /// <summary>
        /// Use this method to unregister a previously registered <see cref="IRuntimeLog"/>.
        /// </summary>
        /// <param name="log">The previously registered log writer to unregister.</param>
        void RemoveLog(IRuntimeLog log);

        /// <summary>
        /// Terminates the runtime, causing any executing operations to eventually complete.
        /// </summary>
        void Stop();
    }
}
