﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Coyote.SystematicTesting
{
    /// <summary>
    /// A portfolio of systematic testing strategies.
    /// </summary>
    internal static class TestingPortfolio
    {
        /// <summary>
        /// Configures the systematic testing strategy for the current testing process.
        /// </summary>
        internal static void ConfigureStrategyForCurrentProcess(Configuration configuration)
        {
            if (configuration.TestingProcessId is 0)
            {
                configuration.SchedulingStrategy = "random";
            }
            else if (configuration.TestingProcessId % 2 is 0)
            {
                configuration.SchedulingStrategy = "probabilistic";
                configuration.StrategyBound = (int)(configuration.TestingProcessId / 2);
            }
            else if (configuration.TestingProcessId is 1)
            {
                configuration.SchedulingStrategy = "fair-prioritization";
                configuration.StrategyBound = 1;
            }
            else
            {
                configuration.SchedulingStrategy = "fair-prioritization";
                configuration.StrategyBound = 5 * (int)((configuration.TestingProcessId + 1) / 2);
            }
        }
    }
}
