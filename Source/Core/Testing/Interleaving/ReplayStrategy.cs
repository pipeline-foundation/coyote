﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Coyote.IO;
using Microsoft.Coyote.Runtime;

namespace Microsoft.Coyote.Testing.Interleaving
{
    /// <summary>
    /// A replaying scheduling strategy.
    /// </summary>
    internal sealed class ReplayStrategy : InterleavingStrategy
    {
        /// <summary>
        /// The Coyote program schedule trace.
        /// </summary>
        private readonly ScheduleTrace ScheduleTrace;

        /// <summary>
        /// Text describing a replay error.
        /// </summary>
        internal string ErrorText { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReplayStrategy"/> class.
        /// </summary>
        internal ReplayStrategy(Configuration configuration, IRandomValueGenerator generator,
            ScheduleTrace trace, bool isFair)
            : base(configuration, generator, isFair)
        {
            this.ScheduleTrace = trace;
            this.ErrorText = string.Empty;
        }

        /// <inheritdoc/>
        internal override bool InitializeNextIteration(uint iteration)
        {
            this.StepCount = 0;
            return iteration is 0;
        }

        /// <inheritdoc/>
        internal override bool GetNextOperation(IEnumerable<ControlledOperation> ops, ControlledOperation current,
            bool isYielding, out ControlledOperation next)
        {
            try
            {
                if (this.StepCount >= this.ScheduleTrace.Count)
                {
                    this.ErrorText = this.FormatError("execution is longer than trace");
                    throw new InvalidOperationException(this.ErrorText);
                }

                ScheduleStep nextStep = this.ScheduleTrace[this.StepCount];
                if (nextStep.Type != ScheduleStepType.SchedulingChoice)
                {
                    this.ErrorText = this.FormatError("next step is not a scheduling choice");
                    throw new InvalidOperationException(this.ErrorText);
                }

                next = ops.FirstOrDefault(op => op.Id == nextStep.ScheduledOperationId);
                if (next is null)
                {
                    this.ErrorText = this.FormatError($"cannot detect id '{nextStep.ScheduledOperationId}'");
                    throw new InvalidOperationException(this.ErrorText);
                }
            }
            catch (InvalidOperationException ex)
            {
                Error.Report(ex.Message);
                next = null;
                return false;
            }

            this.StepCount++;
            return true;
        }

        /// <inheritdoc/>
        internal override bool GetNextBooleanChoice(ControlledOperation current, out bool next)
        {
            ScheduleStep nextStep;

            try
            {
                if (this.StepCount >= this.ScheduleTrace.Count)
                {
                    this.ErrorText = this.FormatError("execution is longer than trace");
                    throw new InvalidOperationException(this.ErrorText);
                }

                nextStep = this.ScheduleTrace[this.StepCount];
                if (nextStep.Type != ScheduleStepType.NondeterministicChoice)
                {
                    this.ErrorText = this.FormatError("next step is not a nondeterministic choice");
                    throw new InvalidOperationException(this.ErrorText);
                }

                if (nextStep.BooleanChoice is null)
                {
                    this.ErrorText = this.FormatError("next step is not a nondeterministic boolean choice");
                    throw new InvalidOperationException(this.ErrorText);
                }
            }
            catch (InvalidOperationException ex)
            {
                Error.Report(ex.Message);
                next = false;
                return false;
            }

            next = nextStep.BooleanChoice.Value;
            this.StepCount++;
            return true;
        }

        /// <inheritdoc/>
        internal override bool GetNextIntegerChoice(ControlledOperation current, int maxValue, out int next)
        {
            ScheduleStep nextStep;

            try
            {
                if (this.StepCount >= this.ScheduleTrace.Count)
                {
                    this.ErrorText = this.FormatError("execution is longer than trace");
                    throw new InvalidOperationException(this.ErrorText);
                }

                nextStep = this.ScheduleTrace[this.StepCount];
                if (nextStep.Type != ScheduleStepType.NondeterministicChoice)
                {
                    this.ErrorText = this.FormatError("next step is not a nondeterministic choice");
                    throw new InvalidOperationException(this.ErrorText);
                }

                if (nextStep.IntegerChoice is null)
                {
                    this.ErrorText = this.FormatError("next step is not a nondeterministic integer choice");
                    throw new InvalidOperationException(this.ErrorText);
                }
            }
            catch (InvalidOperationException ex)
            {
                Error.Report(ex.Message);
                next = 0;
                return false;
            }

            next = nextStep.IntegerChoice.Value;
            this.StepCount++;
            return true;
        }

        /// <inheritdoc/>
        internal override int GetStepCount() => this.StepCount;

        /// <inheritdoc/>
        internal override bool IsMaxStepsReached() => false;

        /// <inheritdoc/>
        internal override string GetDescription() => "replay";

        /// <inheritdoc/>
        internal override void Reset()
        {
            this.StepCount = 0;
        }

        /// <summary>
        /// Formats the error message.
        /// </summary>
        private string FormatError(string reason) => this.Configuration.RandomGeneratorSeed.HasValue ?
            $"Trace from execution with random seed '{this.Configuration.RandomGeneratorSeed}' is not reproducible: {reason}." :
            $"Trace is not reproducible: {reason}.";
    }
}
