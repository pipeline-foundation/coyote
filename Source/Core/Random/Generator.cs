﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Coyote.Runtime;

namespace Microsoft.Coyote.Random
{
    /// <summary>
    /// Represents a pseudo-random value generator, which is an algorithm that produces
    /// a sequence of values that meet certain statistical requirements for randomness.
    /// During systematic testing, the generation of random values is controlled, which
    /// allows the runtime to explore combinations of choices to find bugs.
    /// </summary>
    /// <remarks>
    /// See <see href="/coyote/concepts/non-determinism" >Program non-determinism</see>
    /// for more information.
    /// </remarks>
    public class Generator
    {
        /// <summary>
        /// The runtime associated with this random value generator.
        /// </summary>
        internal readonly CoyoteRuntime Runtime;

        /// <summary>
        /// Initializes a new instance of the <see cref="Generator"/> class.
        /// </summary>
        private Generator()
        {
            this.Runtime = CoyoteRuntime.Current;
        }

        /// <summary>
        /// Creates a new pseudo-random value generator.
        /// </summary>
        /// <returns>The pseudo-random value generator.</returns>
        public static Generator Create() => new Generator();

        /// <summary>
        /// Returns a random boolean, that can be controlled during testing.
        /// </summary>
        public bool NextBoolean() => this.Runtime.RandomBoolean();

        /// <summary>
        /// Returns a random integer, that can be controlled during testing.
        /// </summary>
        public int NextInteger(int maxValue) => this.Runtime.RandomInteger(maxValue);
    }
}
