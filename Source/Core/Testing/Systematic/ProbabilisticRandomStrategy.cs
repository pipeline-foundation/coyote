﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Coyote.Runtime;

namespace Microsoft.Coyote.Testing.Systematic
{
    /// <summary>
    /// A randomized scheduling strategy with increased probability
    /// to remain in the same scheduling choice.
    /// </summary>
    internal sealed class ProbabilisticRandomStrategy : RandomStrategy
    {
        /// <summary>
        /// Number of coin flips.
        /// </summary>
        private readonly int NumberOfCoinFlips;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProbabilisticRandomStrategy"/> class.
        /// </summary>
        internal ProbabilisticRandomStrategy(int maxSteps, int numberOfCoinFlips, IRandomValueGenerator generator)
            : base(maxSteps, generator)
        {
            this.NumberOfCoinFlips = numberOfCoinFlips;
        }

        /// <inheritdoc/>
        internal override bool GetNextOperation(IEnumerable<ControlledOperation> ops, ControlledOperation current,
            bool isYielding, out ControlledOperation next)
        {
            var enabledOps = ops.Where(op => op.Status is OperationStatus.Enabled).ToList();
            if (enabledOps.Count is 0)
            {
                next = null;
                return false;
            }

            this.StepCount++;

            if (enabledOps.Count > 1)
            {
                if (!this.ShouldCurrentMachineChange() && current.Status is OperationStatus.Enabled)
                {
                    next = current;
                    return true;
                }
            }

            int idx = this.RandomValueGenerator.Next(enabledOps.Count);
            next = enabledOps[idx];

            return true;
        }

        /// <inheritdoc/>
        internal override string GetDescription() =>
            $"probabilistic[seed '{this.RandomValueGenerator.Seed}', coin flips '{this.NumberOfCoinFlips}']";

        /// <summary>
        /// Flip the coin a specified number of times.
        /// </summary>
        private bool ShouldCurrentMachineChange()
        {
            for (int idx = 0; idx < this.NumberOfCoinFlips; idx++)
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
