﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.Serialization;
using System.Threading;
using Microsoft.Coyote.Logging;
using Microsoft.Coyote.Runtime;
using Microsoft.Coyote.Testing;

namespace Microsoft.Coyote
{
#pragma warning disable CA1724 // Type names should not match namespaces
    /// <summary>
    /// The Coyote runtime and testing configuration.
    /// </summary>
    [DataContract]
    [Serializable]
    public class Configuration
    {
        /// <summary>
        /// The output path.
        /// </summary>
        [DataMember]
        internal string OutputFilePath;

        /// <summary>
        /// The assembly to be analyzed for bugs.
        /// </summary>
        [DataMember]
        internal string AssemblyToBeAnalyzed;

        /// <summary>
        /// Test method to be used.
        /// </summary>
        [DataMember]
        internal string TestMethodName;

        /// <summary>
        /// Number of testing iterations.
        /// </summary>
        [DataMember]
        public uint TestingIterations { get; internal set; }

        /// <summary>
        /// Timeout in seconds after which no more testing iterations will run.
        /// </summary>
        /// <remarks>
        /// Setting this value overrides the <see cref="TestingIterations"/> value.
        /// </remarks>
        [DataMember]
        internal int TestingTimeout;

        /// <summary>
        /// Custom seed to be used by the random value generator. By default,
        /// this value is null indicating that no seed has been set.
        /// </summary>
        [DataMember]
        public uint? RandomGeneratorSeed { get; internal set; }

        /// <summary>
        /// The exploration strategy to use during testing.
        /// </summary>
        [DataMember]
        internal ExplorationStrategy ExplorationStrategy { get; set; }

        /// <summary>
        /// A strategy-specific bound.
        /// </summary>
        [DataMember]
        internal int StrategyBound;

        /// <summary>
        /// The exploration strategy portfolio mode that is enabled during testing.
        /// </summary>
        [DataMember]
        internal PortfolioMode PortfolioMode;

        /// <summary>
        /// If enabled and uncontrolled concurrency is detected, then the runtime will attempt to partially
        /// control the concurrency of the application, instead of immediately failing with an error.
        /// </summary>
        [DataMember]
        internal bool IsPartiallyControlledConcurrencyAllowed;

        /// <summary>
        /// If enabled and uncontrolled data non-determinism is detected, then the runtime will attempt
        /// to partially control the data non-determinism of the application, instead of immediately
        /// failing with an error.
        /// </summary>
        [DataMember]
        internal bool IsPartiallyControlledDataNondeterminismAllowed;

        /// <summary>
        /// If enabled, the systematic fuzzing policy is used during testing.
        /// </summary>
        [DataMember]
        internal bool IsSystematicFuzzingEnabled;

        /// <summary>
        /// If enabled and uncontrolled concurrency is detected, then the tester automatically switches
        /// to systematic fuzzing, instead of failing with an error.
        /// </summary>
        [DataMember]
        internal bool IsSystematicFuzzingFallbackEnabled;

        /// <summary>
        /// Value that controls the maximum time an operation might get delayed during systematic fuzzing.
        /// </summary>
        [DataMember]
        public uint MaxFuzzingDelay { get; internal set; }

        /// <summary>
        /// If enabled, liveness checking is enabled during systematic testing.
        /// </summary>
        [DataMember]
        internal bool IsLivenessCheckingEnabled;

        /// <summary>
        /// If enabled, checking races at collection accesses is enabled during systematic testing.
        /// </summary>
        [DataMember]
        internal bool IsCollectionAccessRaceCheckingEnabled;

        /// <summary>
        /// If enabled, checking races at lock accesses is enabled during systematic testing.
        /// </summary>
        [DataMember]
        internal bool IsLockAccessRaceCheckingEnabled;

        /// <summary>
        /// If enabled, checking races at atomic operations is enabled during systematic testing.
        /// </summary>
        [DataMember]
        internal bool IsAtomicOperationRaceCheckingEnabled;

        /// <summary>
        /// If enabled, shared state reduction is enabled during systematic testing.
        /// </summary>
        [DataMember]
        internal bool IsSharedStateReductionEnabled;

        /// <summary>
        /// If true, the tester runs all iterations up to a bound, even if a bug is found.
        /// </summary>
        [DataMember]
        internal bool RunTestIterationsToCompletion;

        /// <summary>
        /// The maximum scheduling steps to explore for unfair schedulers.
        /// By default this is set to 10,000 steps.
        /// </summary>
        [DataMember]
        public int MaxUnfairSchedulingSteps { get; internal set; }

        /// <summary>
        /// The maximum scheduling steps to explore for fair schedulers.
        /// By default this is set to 100,000 steps.
        /// </summary>
        [DataMember]
        public int MaxFairSchedulingSteps { get; internal set; }

        /// <summary>
        /// True if the user has explicitly set the fair scheduling steps.
        /// </summary>
        [DataMember]
        internal bool UserExplicitlySetMaxFairSchedulingSteps;

        /// <summary>
        /// If true, then the Coyote tester will consider an execution
        /// that hits the depth bound as buggy.
        /// </summary>
        [DataMember]
        internal bool ConsiderDepthBoundHitAsBug;

        /// <summary>
        /// Value that controls the probability of triggering a timeout during systematic testing.
        /// Decrease the value to increase the frequency of timeouts (e.g. a value of 1 corresponds
        /// to a 50% probability), or increase the value to decrease the frequency (e.g. a value of
        /// 10 corresponds to a 10% probability).
        /// </summary>
        [DataMember]
        public uint TimeoutDelay { get; internal set; }

        /// <summary>
        /// Value that controls how much time the deadlock monitor should wait during concurrency testing
        /// before reporting a potential deadlock. This value is in milliseconds.
        /// </summary>
        [DataMember]
        public uint DeadlockTimeout { get; internal set; }

        /// <summary>
        /// If enabled then report any potential deadlock as a bug, else skip to the next test iteration.
        /// </summary>
        [DataMember]
        internal bool ReportPotentialDeadlocksAsBugs;

        /// <summary>
        /// Value that controls how many times the runtime can check if each instance
        /// of uncontrolled concurrency has resolved during testing before timing out.
        /// </summary>
        [DataMember]
        internal uint UncontrolledConcurrencyResolutionAttempts;

        /// <summary>
        /// Value that controls how much time the runtime should wait between each attempt
        /// to resolve uncontrolled concurrency during testing before timing out.
        /// </summary>
        [DataMember]
        internal uint UncontrolledConcurrencyResolutionDelay;

        /// <summary>
        /// The liveness temperature threshold. If it is 0 then it is disabled. By default
        /// this value is assigned to <see cref="MaxFairSchedulingSteps"/> / 2.
        /// </summary>
        [DataMember]
        internal int LivenessTemperatureThreshold;

        /// <summary>
        /// True if the user has explicitly set the liveness temperature threshold.
        /// </summary>
        [DataMember]
        internal bool UserExplicitlySetLivenessTemperatureThreshold;

        /// <summary>
        /// If enabled, runtime and automatically inferred program state is used to contribute
        /// to the computed program state at each scheduling step during testing.
        /// </summary>
        [DataMember]
        internal bool IsImplicitProgramStateHashingEnabled;

        /// <summary>
        /// If enabled, safety monitors can run outside the scope of the testing engine.
        /// </summary>
        [DataMember]
        internal bool IsMonitoringEnabledOutsideTesting { get; private set; }

        /// <summary>
        /// If enabled, the runtime can check for actor quiescence outside the scope of the testing engine.
        /// </summary>
        [DataMember]
        internal bool IsActorQuiescenceCheckingEnabledOutsideTesting;

        /// <summary>
        /// If enabled, the runtime can bypass scheduling suppression to avoid deadlocking during testing.
        /// </summary>
        [DataMember]
        internal bool IsSchedulingSuppressionWeak;

        /// <summary>
        /// Attaches the debugger during trace replay.
        /// </summary>
        [DataMember]
        internal bool AttachDebugger;

        /// <summary>
        /// The trace to be replayed during testing.
        /// </summary>
        internal string ReproducibleTrace { get; private set; }

        /// <summary>
        /// The level of verbosity to use during logging.
        /// </summary>
        public VerbosityLevel VerbosityLevel { get; private set; }

        /// <summary>
        /// If true, then console logging is enabled, else false.
        /// </summary>
        internal bool IsConsoleLoggingEnabled { get; private set; }

        /// <summary>
        /// Enables logging of stack traces when uncontrolled invocations are detected during testing.
        /// </summary>
        internal bool IsUncontrolledInvocationStackTraceLoggingEnabled { get; private set; }

        /// <summary>
        /// Enables activity coverage reporting during testing.
        /// </summary>
        [DataMember]
        internal bool IsActivityCoverageReported;

        /// <summary>
        /// Enables schedule coverage reporting during testing.
        /// </summary>
        [DataMember]
        internal bool IsScheduleCoverageReported;

        /// <summary>
        /// Serialize the reported coverage information.
        /// </summary>
        [DataMember]
        internal bool IsCoverageInfoSerialized;

        /// <summary>
        /// If true, requests a DGML graph of the iteration that contains a bug, if a bug is found.
        /// This is different from a coverage activity graph, as it will also show actor instances.
        /// </summary>
        [DataMember]
        internal bool IsTraceVisualizationEnabled;

        /// <summary>
        /// Produce an XML formatted runtime log file.
        /// </summary>
        [DataMember]
        internal bool IsXmlLogEnabled;

        /// <summary>
        /// If true, then anonymized telemetry is enabled, else false.
        /// </summary>
        internal bool IsTelemetryEnabled;

        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration"/> class.
        /// </summary>
        protected Configuration()
        {
            this.OutputFilePath = string.Empty;
            this.AssemblyToBeAnalyzed = string.Empty;
            this.TestMethodName = string.Empty;
            this.ReproducibleTrace = string.Empty;

            this.VerbosityLevel = VerbosityLevel.Error;
            this.IsConsoleLoggingEnabled = false;

            this.ExplorationStrategy = ExplorationStrategy.Random;
            this.TestingIterations = 1;
            this.TestingTimeout = 0;
            this.RandomGeneratorSeed = null;
            this.PortfolioMode = PortfolioMode.Fair;
            this.IsPartiallyControlledConcurrencyAllowed = true;
            this.IsPartiallyControlledDataNondeterminismAllowed = true;
            this.IsSystematicFuzzingEnabled = false;
            this.IsSystematicFuzzingFallbackEnabled = true;
            this.MaxFuzzingDelay = 1000;
            this.IsLivenessCheckingEnabled = true;
            this.IsCollectionAccessRaceCheckingEnabled = true;
            this.IsLockAccessRaceCheckingEnabled = true;
            this.IsAtomicOperationRaceCheckingEnabled = true;
            this.IsSharedStateReductionEnabled = false;
            this.RunTestIterationsToCompletion = false;
            this.MaxUnfairSchedulingSteps = 10000;
            this.MaxFairSchedulingSteps = 100000; // 10 times the unfair steps.
            this.UserExplicitlySetMaxFairSchedulingSteps = false;
            this.ConsiderDepthBoundHitAsBug = false;
            this.StrategyBound = 0;
            this.TimeoutDelay = 10;
            this.DeadlockTimeout = 1000;
            this.ReportPotentialDeadlocksAsBugs = true;
            this.UncontrolledConcurrencyResolutionAttempts = 10;
            this.UncontrolledConcurrencyResolutionDelay = 1000;
            this.LivenessTemperatureThreshold = 50000;
            this.UserExplicitlySetLivenessTemperatureThreshold = false;
            this.IsImplicitProgramStateHashingEnabled = false;
            this.IsMonitoringEnabledOutsideTesting = false;
            this.IsActorQuiescenceCheckingEnabledOutsideTesting = false;
            this.IsSchedulingSuppressionWeak = false;
            this.AttachDebugger = false;

            this.IsUncontrolledInvocationStackTraceLoggingEnabled = false;
            this.IsActivityCoverageReported = false;
            this.IsScheduleCoverageReported = false;
            this.IsCoverageInfoSerialized = false;
            this.IsTraceVisualizationEnabled = false;
            this.IsXmlLogEnabled = false;

            string optout = Environment.GetEnvironmentVariable("COYOTE_CLI_TELEMETRY_OPTOUT");
            this.IsTelemetryEnabled = optout != "1" && optout != "true";
        }

        /// <summary>
        /// Creates a new configuration with default values.
        /// </summary>
        public static Configuration Create()
        {
            return new Configuration();
        }

        /// <summary>
        /// Updates the configuration to use the specified verbosity level, or <see cref="VerbosityLevel.Info"/>,
        /// if no level is specified. The default verbosity level is <see cref="VerbosityLevel.Error"/>.
        /// </summary>
        /// <param name="level">The level of verbosity used during logging.</param>
        public Configuration WithVerbosityEnabled(VerbosityLevel level = VerbosityLevel.Info)
        {
            this.VerbosityLevel = level;
            return this;
        }

        /// <summary>
        /// Updates the configuration to log all runtime messages to the console, unless overridden by a custom <see cref="ILogger"/>.
        /// </summary>
        /// <param name="isEnabled">If true, then logs all runtime messages to the console.</param>
        public Configuration WithConsoleLoggingEnabled(bool isEnabled = true)
        {
            this.IsConsoleLoggingEnabled = isEnabled;
            return this;
        }

        /// <summary>
        /// Updates the configuration with the specified number of iterations to run during systematic testing.
        /// </summary>
        /// <param name="iterations">The number of iterations to run.</param>
        public Configuration WithTestingIterations(uint iterations)
        {
            this.TestingIterations = iterations;
            return this;
        }

        /// <summary>
        /// Updates the configuration with the specified systematic testing timeout in seconds.
        /// </summary>
        /// <param name="timeout">The timeout value in seconds.</param>
        /// <remarks>
        /// Setting this value overrides the <see cref="TestingIterations"/> value.
        /// </remarks>
        public Configuration WithTestingTimeout(int timeout)
        {
            this.TestingTimeout = timeout;
            return this;
        }

        /// <summary>
        /// Updates the configuration to try reproduce the specified trace during systematic testing.
        /// </summary>
        /// <param name="trace">The trace to be reproduced.</param>
        public Configuration WithReproducibleTrace(string trace)
        {
            this.ReproducibleTrace = trace;
            return this;
        }

        /// <summary>
        /// Updates the configuration to use the random exploration strategy during systematic testing.
        /// </summary>
        /// <remarks>
        /// Note that explicitly setting this strategy disables the default exploration mode
        /// that uses a tuned portfolio of strategies.
        /// </remarks>
        public Configuration WithRandomStrategy()
        {
            this.ExplorationStrategy = ExplorationStrategy.Random;
            this.PortfolioMode = PortfolioMode.None;
            return this;
        }

        /// <summary>
        /// Updates the configuration to use the probabilistic exploration strategy during systematic testing.
        /// You can specify a value controlling the probability of each scheduling decision. This value is
        /// specified as the integer N in the equation 0.5 to the power of N. So for N=1, the probability is
        /// 0.5, for N=2 the probability is 0.25, N=3 you get 0.125, etc. By default, this value is 3.
        /// </summary>
        /// <param name="probabilityLevel">The probability level.</param>
        /// <remarks>
        /// Note that explicitly setting this strategy disables the default exploration mode
        /// that uses a tuned portfolio of strategies.
        /// </remarks>
        public Configuration WithProbabilisticStrategy(uint probabilityLevel = 3)
        {
            this.ExplorationStrategy = ExplorationStrategy.Probabilistic;
            this.StrategyBound = (int)probabilityLevel;
            this.PortfolioMode = PortfolioMode.None;
            return this;
        }

        /// <summary>
        /// Updates the configuration to use the priority-based exploration strategy during systematic testing.
        /// You can specify if you want to enable liveness checking, which is disabled by default, and an upper
        /// bound of possible priority changes, which by default can be up to 10.
        /// </summary>
        /// <param name="isFair">If true, enable liveness checking by using fair scheduling.</param>
        /// <param name="priorityChangeBound">Upper bound of possible priority changes per test iteration.</param>
        /// <remarks>
        /// Note that explicitly setting this strategy disables the default exploration mode
        /// that uses a tuned portfolio of strategies.
        /// </remarks>
        public Configuration WithPrioritizationStrategy(bool isFair = false, uint priorityChangeBound = 10)
        {
            this.ExplorationStrategy = isFair ? ExplorationStrategy.FairPrioritization : ExplorationStrategy.Prioritization;
            this.StrategyBound = (int)priorityChangeBound;
            this.PortfolioMode = PortfolioMode.None;
            return this;
        }

        /// <summary>
        /// Updates the configuration to use the delay-bounding exploration strategy during systematic testing.
        /// You can specify if you want to enable liveness checking, which is disabled by default, and an upper
        /// bound of possible delays, which by default can be up to 10.
        /// </summary>
        /// <param name="isFair">If true, enable liveness checking by using fair scheduling.</param>
        /// <param name="delayBound">Upper bound of possible priority delays per test iteration.</param>
        /// <remarks>
        /// Note that explicitly setting this strategy disables the default exploration mode
        /// that uses a tuned portfolio of strategies.
        /// </remarks>
        public Configuration WithDelayBoundingStrategy(bool isFair = false, uint delayBound = 10)
        {
            this.ExplorationStrategy = isFair ? ExplorationStrategy.FairDelayBounding : ExplorationStrategy.DelayBounding;
            this.StrategyBound = (int)delayBound;
            this.PortfolioMode = PortfolioMode.None;
            return this;
        }

        /// <summary>
        /// Updates the configuration to use the Q-learning exploration strategy during systematic testing.
        /// </summary>
        /// <remarks>
        /// Note that explicitly setting this strategy disables the default exploration mode
        /// that uses a tuned portfolio of strategies.
        /// </remarks>
        public Configuration WithQLearningStrategy()
        {
            this.ExplorationStrategy = ExplorationStrategy.QLearning;
            this.IsImplicitProgramStateHashingEnabled = true;
            this.PortfolioMode = PortfolioMode.None;
            return this;
        }

        /// <summary>
        /// Updates the configuration to use the dfs exploration strategy during systematic testing.
        /// </summary>
        /// <remarks>
        /// Note that explicitly setting this strategy disables the default exploration mode
        /// that uses a tuned portfolio of strategies.
        /// </remarks>
        internal Configuration WithDFSStrategy()
        {
            this.ExplorationStrategy = ExplorationStrategy.DFS;
            this.PortfolioMode = PortfolioMode.None;
            return this;
        }

        /// <summary>
        /// Updates the configuration to use fair or unfair portfolio mode during testing. Portfolio
        /// mode uses a tuned portfolio of strategies, instead of the default or user-specified strategy.
        /// If fair mode is enabled, then the portfolio will upgrade any unfair strategies to fair,
        /// by adding a fair execution suffix after the the max fair scheduling steps bound has been
        /// reached. By default, fair portfolio mode is enabled.
        /// </summary>
        /// <param name="isFair">
        /// If true, which is the default value, then the portfolio mode is fair, else it is unfair.
        /// </param>
        internal Configuration WithPortfolioMode(bool isFair = true)
        {
            this.PortfolioMode = isFair ? PortfolioMode.Fair : PortfolioMode.Unfair;
            return this;
        }

        /// <summary>
        /// Updates the configuration with partially controlled concurrency allowed or disallowed.
        /// </summary>
        /// <param name="isAllowed">If true, then partially controlled concurrency is allowed.</param>
        public Configuration WithPartiallyControlledConcurrencyAllowed(bool isAllowed = true)
        {
            this.IsPartiallyControlledConcurrencyAllowed = isAllowed;
            return this;
        }

        /// <summary>
        /// Updates the configuration with partially controlled data non-determinism allowed or disallowed.
        /// </summary>
        /// <param name="isAllowed">If true, then partially controlled data non-determinism is allowed.</param>
        public Configuration WithPartiallyControlledDataNondeterminismAllowed(bool isAllowed = true)
        {
            this.IsPartiallyControlledDataNondeterminismAllowed = isAllowed;
            return this;
        }

        /// <summary>
        /// Updates the configuration with systematic fuzzing enabled or disabled.
        /// </summary>
        /// <param name="isEnabled">If true, then systematic fuzzing is enabled.</param>
        public Configuration WithSystematicFuzzingEnabled(bool isEnabled = true)
        {
            this.IsSystematicFuzzingEnabled = isEnabled;
            return this;
        }

        /// <summary>
        /// Updates the configuration with systematic fuzzing fallback enabled or disabled.
        /// </summary>
        /// <param name="isEnabled">If true, then systematic fuzzing fallback is enabled.</param>
        public Configuration WithSystematicFuzzingFallbackEnabled(bool isEnabled = true)
        {
            this.IsSystematicFuzzingFallbackEnabled = isEnabled;
            return this;
        }

        /// <summary>
        /// Updates the value that controls the maximum time an operation might get delayed
        /// during systematic fuzzing.
        /// </summary>
        /// <param name="delay">The maximum delay during systematic fuzzing, which by default is 1000.</param>
        public Configuration WithMaxFuzzingDelay(uint delay)
        {
            this.MaxFuzzingDelay = delay;
            return this;
        }

        /// <summary>
        /// Updates the configuration with race checking for collection accesses enabled or disabled.
        /// If this race checking strategy is enabled, then the runtime will explore interleavings
        /// when concurrent operations try to access collections.
        /// </summary>
        /// <param name="isEnabled">If true, then checking races at collection accesses is enabled.</param>
        public Configuration WithCollectionAccessRaceCheckingEnabled(bool isEnabled = true)
        {
            this.IsCollectionAccessRaceCheckingEnabled = isEnabled;
            return this;
        }

        /// <summary>
        /// Updates the configuration with race checking for lock accesses enabled or disabled.
        /// If this race checking strategy is enabled, then the runtime will explore interleavings
        /// when concurrent operations try to access lock-based synchronization primitives.
        /// </summary>
        /// <param name="isEnabled">If true, then checking races at lock accesses is enabled.</param>
        public Configuration WithLockAccessRaceCheckingEnabled(bool isEnabled = true)
        {
            this.IsLockAccessRaceCheckingEnabled = isEnabled;
            return this;
        }

        /// <summary>
        /// Updates the configuration with race checking for atomic operations enabled or disabled.
        /// If this race checking strategy is enabled, then the runtime will explore interleavings
        /// when invoking atomic operations, such as <see cref="Interlocked"/> methods.
        /// </summary>
        /// <param name="isEnabled">If true, then checking races at atomic operations is enabled.</param>
        public Configuration WithAtomicOperationRaceCheckingEnabled(bool isEnabled = true)
        {
            this.IsAtomicOperationRaceCheckingEnabled = isEnabled;
            return this;
        }

        /// <summary>
        /// Updates the configuration with shared state reduction enabled or disabled. If this
        /// reduction strategy is enabled, then the runtime will attempt to reduce the schedule
        /// space by taking into account any 'READ' and 'WRITE' operations declared by invoking
        /// <see cref="SchedulingPoint.Read"/> and <see cref="SchedulingPoint.Write"/>.
        /// </summary>
        /// <param name="isEnabled">If true, then shared state reduction is enabled.</param>
        public Configuration WithSharedStateReductionEnabled(bool isEnabled = true)
        {
            this.IsSharedStateReductionEnabled = isEnabled;
            return this;
        }

        /// <summary>
        /// Updates the configuration with the ability to reproduce bug traces enabled or disabled.
        /// Disabling reproducibility allows skipping errors due to uncontrolled concurrency, for
        /// example when the program is only partially rewritten, or there is external concurrency
        /// that is not mocked, or when the program uses an API that is not yet supported.
        /// </summary>
        /// <param name="isDisabled">If true, then reproducing bug traces is disabled.</param>
        public Configuration WithNoBugTraceRepro(bool isDisabled = true)
        {
            this.IsSystematicFuzzingEnabled = isDisabled;
            return this;
        }

        /// <summary>
        /// Updates the configuration with the specified number of maximum scheduling steps to explore per
        /// iteration during systematic testing. The <see cref="MaxUnfairSchedulingSteps"/> is assigned the
        /// <paramref name="maxSteps"/> value, whereas the <see cref="MaxFairSchedulingSteps"/> is assigned
        /// a value using the default heuristic, which is 10 * <paramref name="maxSteps"/>.
        /// </summary>
        /// <param name="maxSteps">The maximum scheduling steps to explore per iteration.</param>
        public Configuration WithMaxSchedulingSteps(uint maxSteps)
        {
            this.MaxUnfairSchedulingSteps = (int)maxSteps;
            this.MaxFairSchedulingSteps = 10 * (int)maxSteps;
            return this;
        }

        /// <summary>
        /// Updates the configuration with the specified number of maximum unfair and fair scheduling
        /// steps to explore per iteration during systematic testing. It is recommended to use
        /// <see cref="WithMaxSchedulingSteps(uint)"/> instead of this overloaded method.
        /// </summary>
        /// <param name="maxUnfairSteps">The unfair scheduling steps to explore per iteration.</param>
        /// <param name="maxFairSteps">The fair scheduling steps to explore per iteration.</param>
        public Configuration WithMaxSchedulingSteps(uint maxUnfairSteps, uint maxFairSteps)
        {
            if (maxFairSteps < maxUnfairSteps)
            {
                throw new ArgumentException("The max fair steps cannot not be less than the max unfair steps.", nameof(maxFairSteps));
            }

            this.MaxUnfairSchedulingSteps = (int)maxUnfairSteps;
            this.MaxFairSchedulingSteps = (int)maxFairSteps;
            this.UserExplicitlySetMaxFairSchedulingSteps = true;
            return this;
        }

        /// <summary>
        /// Updates the configuration with the specified liveness temperature threshold during
        /// systematic testing. If this value is 0 it disables liveness checking. It is not
        /// recommended to explicitly set this value, instead use the default value which is
        /// assigned to <see cref="MaxFairSchedulingSteps"/> / 2.
        /// </summary>
        /// <param name="threshold">The liveness temperature threshold.</param>
        public Configuration WithLivenessTemperatureThreshold(uint threshold)
        {
            this.LivenessTemperatureThreshold = (int)threshold;
            this.UserExplicitlySetLivenessTemperatureThreshold = true;
            return this;
        }

        /// <summary>
        /// Updates the value that controls the probability of triggering a timeout during systematic testing.
        /// </summary>
        /// <param name="delay">The timeout delay during testing, which by default is 10.</param>
        /// <remarks>
        /// Increase the value to decrease the probability. This value is not a unit of time.
        /// </remarks>
        public Configuration WithTimeoutDelay(uint delay)
        {
            this.TimeoutDelay = delay;
            return this;
        }

        /// <summary>
        /// Updates the value that controls how much time the background deadlock monitor should
        /// wait during concurrency testing before reporting a potential deadlock.
        /// </summary>
        /// <param name="timeout">The timeout value in milliseconds, which by default is 1000.</param>
        /// <remarks>
        /// Increase the value to give more time to the test to resolve a potential deadlock.
        /// </remarks>
        public Configuration WithDeadlockTimeout(uint timeout)
        {
            this.DeadlockTimeout = timeout;
            return this;
        }

        /// <summary>
        /// Updates the value that controls if potential deadlocks should be reported as bugs.
        /// </summary>
        /// <param name="reportedAsBugs">If true, then potential deadlocks are reported as bugs.</param>
        /// <remarks>
        /// A deadlock is considered to be potential if the runtime cannot fully determine if the
        /// deadlock is genuine or occurred because of partially-controlled concurrency.
        /// </remarks>
        public Configuration WithPotentialDeadlocksReportedAsBugs(bool reportedAsBugs = true)
        {
            this.ReportPotentialDeadlocksAsBugs = reportedAsBugs;
            return this;
        }

        /// <summary>
        /// Updates the values that control how much time the runtime should wait for each
        /// instance of uncontrolled concurrency to resolve before continuing exploration.
        /// The <paramref name="attempts"/> parameter controls how many times to check if
        /// uncontrolled concurrency has resolved, whereas the <paramref name="delay"/>
        /// parameter controls how long the runtime waits between each retry.
        /// </summary>
        /// <param name="attempts">The number of attempts, which by default is 10.</param>
        /// <param name="delay">The delay value is the number of busy loops to perform, which by default is 1000.</param>
        /// <remarks>
        /// Increasing each of the values allows more time to try resolve uncontrolled
        /// concurrency at the cost of slower testing.
        /// </remarks>
        public Configuration WithUncontrolledConcurrencyResolutionTimeout(uint attempts, uint delay)
        {
            this.UncontrolledConcurrencyResolutionAttempts = attempts;
            this.UncontrolledConcurrencyResolutionDelay = delay;
            return this;
        }

        /// <summary>
        /// Updates the seed used by the random value generator during systematic testing.
        /// </summary>
        /// <param name="seed">The seed used by the random value generator.</param>
        public Configuration WithRandomGeneratorSeed(uint seed)
        {
            this.RandomGeneratorSeed = seed;
            return this;
        }

        /// <summary>
        /// Updates the configuration so that the tester continues running test iterations
        /// up to a bound, even if a bug is already found.
        /// </summary>
        /// <param name="runToCompletion">
        /// If true, the tester runs all iterations up to a bound, even if a bug is found.
        /// </param>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public Configuration WithTestIterationsRunToCompletion(bool runToCompletion = true)
        {
            this.RunTestIterationsToCompletion = runToCompletion;
            return this;
        }

        /// <summary>
        /// Updates the configuration with stack trace logging for uncontrolled invocations enabled or disabled.
        /// </summary>
        /// <param name="isEnabled">If true, then stack trace logging for uncontrolled invocations is enabled.</param>
        public Configuration WithUncontrolledInvocationStackTraceLoggingEnabled(bool isEnabled = true)
        {
            this.IsUncontrolledInvocationStackTraceLoggingEnabled = isEnabled;
            return this;
        }

        /// <summary>
        /// Updates the configuration to enable or disable reporting activity coverage.
        /// </summary>
        /// <param name="isEnabled">If true, then enables activity coverage.</param>
        public Configuration WithActivityCoverageReported(bool isEnabled = true)
        {
            this.IsActivityCoverageReported = isEnabled;
            return this;
        }

        /// <summary>
        /// Updates the configuration to enable or disable reporting schedule coverage.
        /// </summary>
        /// <param name="isEnabled">If true, then enables schedule coverage reporting.</param>
        public Configuration WithScheduleCoverageReported(bool isEnabled = true)
        {
            this.IsScheduleCoverageReported = isEnabled;
            return this;
        }

        /// <summary>
        /// Updates the configuration to enable or disable serializing the coverage information.
        /// </summary>
        /// <param name="isEnabled">If true, then enables serializing the coverage information.</param>
        public Configuration WithCoverageInfoSerialized(bool isEnabled = true)
        {
            this.IsCoverageInfoSerialized = isEnabled;
            return this;
        }

        /// <summary>
        /// Updates the configuration with trace visualization enabled or disabled.
        /// If enabled, the testing engine can produce a DGML graph representing
        /// an execution leading up to a bug.
        /// </summary>
        /// <param name="isEnabled">If true, then enables trace visualization.</param>
        public Configuration WithTraceVisualizationEnabled(bool isEnabled = true)
        {
            this.IsTraceVisualizationEnabled = isEnabled;
            return this;
        }

        /// <summary>
        /// Updates the configuration with XML log generation enabled or disabled.
        /// </summary>
        /// <param name="isEnabled">If true, then enables XML log generation.</param>
        public Configuration WithXmlLogEnabled(bool isEnabled = true)
        {
            this.IsXmlLogEnabled = isEnabled;
            return this;
        }

        /// <summary>
        /// Updates the configuration with telemetry enabled or disabled.
        /// </summary>
        /// <param name="isEnabled">If true, then enables telemetry.</param>
        public Configuration WithTelemetryEnabled(bool isEnabled = true)
        {
            this.IsTelemetryEnabled = isEnabled;
            return this;
        }

        /// <summary>
        /// Updates the configuration to allow safety monitors to run outside
        /// the scope of the testing engine.
        /// </summary>
        internal Configuration WithMonitoringEnabledOutsideTesting(bool isEnabled = true)
        {
            this.IsMonitoringEnabledOutsideTesting = isEnabled;
            return this;
        }

        /// <summary>
        /// Updates the configuration to enable weak scheduling suppression during testing.
        /// </summary>
        public Configuration WithWeakSchedulingSuppressionEnabled(bool isEnabled = true)
        {
            this.IsSchedulingSuppressionWeak = isEnabled;
            return this;
        }

        /// <summary>
        /// Updates the configuration to allow the runtime to check for actor quiescence
        /// outside the scope of the testing engine.
        /// </summary>
        internal Configuration WithActorQuiescenceCheckingEnabledOutsideTesting(bool isEnabled = true)
        {
            this.IsActorQuiescenceCheckingEnabledOutsideTesting = isEnabled;
            return this;
        }
    }
#pragma warning restore CA1724 // Type names should not match namespaces
}
