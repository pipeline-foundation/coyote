﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Coyote.Runtime;

namespace Microsoft.Coyote.Testing.Interleaving
{
    /// <summary>
    /// A randomized exploration strategy with increased probability
    /// to remain in the same scheduling choice.
    /// </summary>
    internal sealed class ProbabilisticRandomStrategy : RandomStrategy
    {
        /// <summary>
        /// Number of coin flips.
        /// </summary>
        private readonly int Bound;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProbabilisticRandomStrategy"/> class.
        /// </summary>
        internal ProbabilisticRandomStrategy(Configuration configuration, int bound)
            : base(configuration)
        {
            this.Bound = bound;
        }

        /// <inheritdoc/>
        internal override bool NextOperation(IEnumerable<ControlledOperation> ops, ControlledOperation current,
            bool isYielding, out ControlledOperation next)
        {
            int count = ops.Count();
            if (count > 1)
            {
                if (!this.ShouldCurrentOperationChange() && current.Status is OperationStatus.Enabled)
                {
                    next = current;
                    return true;
                }
            }

            int idx = this.RandomValueGenerator.Next(count);
            next = ops.ElementAt(idx);
            return true;
        }

        /// <inheritdoc/>
        internal override string GetName() => ExplorationStrategy.Probabilistic.GetName();

        /// <inheritdoc/>
        internal override string GetDescription() =>
            $"{this.GetName()}[bound:{this.Bound},seed:{this.RandomValueGenerator.Seed}']";

        /// <summary>
        /// Flip the coin a specified number of times.
        /// </summary>
        private bool ShouldCurrentOperationChange()
        {
            for (int idx = 0; idx < this.Bound; idx++)
            {
                if (this.RandomValueGenerator.Next(2) is 1)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
