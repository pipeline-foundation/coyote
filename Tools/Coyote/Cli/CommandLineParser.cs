﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.Coyote.IO;
using Microsoft.Coyote.Rewriting;

namespace Microsoft.Coyote.Cli
{
    internal sealed class CommandLineParser
    {
        /// <summary>
        /// Url with information on learning about coyote.
        /// </summary>
        private const string LearnAboutCoyoteUrl = "https://aka.ms/learn-coyote";

        /// <summary>
        /// Url with information about what is new with coyote.
        /// </summary>
        private const string LearnWhatIsNewUrl = "https://aka.ms/coyote-what-is-new";

        /// <summary>
        /// Url with information about the testing process.
        /// </summary>
        private const string LearnAboutTestUrl = "https://aka.ms/coyote-test";

        /// <summary>
        /// Url with information about the replaying process.
        /// </summary>
        private const string LearnAboutReplayUrl = "https://aka.ms/coyote-replay";

        /// <summary>
        /// Url with information about the rewriting process.
        /// </summary>
        private const string LearnAboutRewritingUrl = "https://aka.ms/coyote-rewrite";

        /// <summary>
        /// The Coyote runtime and testing configuration.
        /// </summary>
        private readonly Configuration Configuration;

        /// <summary>
        /// The Coyote rewriting options.
        /// </summary>
        private readonly RewritingOptions RewritingOptions;

        /// <summary>
        /// The test command.
        /// </summary>
        private readonly Command TestCommand;

        /// <summary>
        /// The replay command.
        /// </summary>
        private readonly Command ReplayCommand;

        /// <summary>
        /// The rewrite command.
        /// </summary>
        private readonly Command RewriteCommand;

        /// <summary>
        /// Mao from argument names to arguments.
        /// </summary>
        private readonly Dictionary<string, Argument> Arguments;

        /// <summary>
        /// Mao from option names to options.
        /// </summary>
        private readonly Dictionary<string, Option> Options;

        /// <summary>
        /// The parse results.
        /// </summary>
        private readonly ParseResult Results;

        /// <summary>
        /// True if parsing was successful, else false.
        /// </summary>
        internal bool IsSuccessful { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandLineParser"/> class.
        /// </summary>
        internal CommandLineParser(string[] args)
        {
            this.Configuration = Configuration.Create();
            this.RewritingOptions = RewritingOptions.Create();
            this.Arguments = new Dictionary<string, Argument>();
            this.Options = new Dictionary<string, Option>();

            var allowedVerbosityLevels = new HashSet<string>
            {
                "quiet",
                "minimal",
                "normal",
                "detailed"
            };

            var verbosityOption = new Option<string>(
                aliases: new[] { "-v", "--verbosity" },
                getDefaultValue: () => "quiet",
                description: "Enable verbosity with an optional verbosity level. " +
                    $"Allowed values are {string.Join(", ", allowedVerbosityLevels)}. " +
                    "Skipping the argument sets the verbosity level to 'detailed'.")
            {
                ArgumentHelpName = "LEVEL",
                Arity = ArgumentArity.ZeroOrOne
            };

            var debugOption = new Option<bool>(aliases: new[] { "-d", "--debug" })
            {
                Arity = ArgumentArity.Zero
            };

            // Add validators.
            verbosityOption.AddValidator(result => ValidateOptionValueIsAllowed(result, allowedVerbosityLevels));

            // Create the commands.
            this.TestCommand = this.CreateTestCommand(this.Configuration);
            this.ReplayCommand = this.CreateReplayCommand();
            this.RewriteCommand = this.CreateRewriteCommand();

            // Create the root command.
            var rootCommand = new RootCommand("The Coyote systematic testing tool.\n\n" +
                $"Learn how to use Coyote at {LearnAboutCoyoteUrl}.\nLearn what is new at {LearnWhatIsNewUrl}.");
            this.AddGlobalOption(rootCommand, verbosityOption);
            this.AddGlobalOption(rootCommand, debugOption);
            rootCommand.AddCommand(this.TestCommand);
            rootCommand.AddCommand(this.ReplayCommand);
            rootCommand.AddCommand(this.RewriteCommand);
            rootCommand.TreatUnmatchedTokensAsErrors = true;

            var commandLineBuilder = new CommandLineBuilder(rootCommand);
            commandLineBuilder.UseDefaults();

            var parser = commandLineBuilder.Build();
            this.Results = parser.Parse(args);
            if (this.Results.Errors.Any() || IsHelpRequested(this.Results))
            {
                // There are parsing errors, so invoke the result to print the errors and help message.
                this.Results.Invoke();
                this.IsSuccessful = false;
            }
            else
            {
                // There were no errors, so use the parsed results to update the default configurations.
                this.UpdateConfigurations(this.Results);
                this.IsSuccessful = true;
            }
        }

        /// <summary>
        /// Invoke the handler of the command that was selected by the user.
        /// </summary>
        internal ExitCode InvokeSelectedCommand(
            Func<Configuration, ExitCode> testHandler,
            Func<Configuration, ExitCode> replayHandler,
            Func<Configuration, RewritingOptions, ExitCode> rewriteHandler)
        {
            PrintDetailedCoyoteVersion();

            this.TestCommand.SetHandler((InvocationContext context) => context.ExitCode = (int)testHandler(this.Configuration));
            this.ReplayCommand.SetHandler((InvocationContext context) => context.ExitCode = (int)replayHandler(this.Configuration));
            this.RewriteCommand.SetHandler((InvocationContext context) => context.ExitCode = (int)rewriteHandler(
                this.Configuration, this.RewritingOptions));
            return (ExitCode)this.Results.Invoke();
        }

        /// <summary>
        /// Creates the test command.
        /// </summary>
        private Command CreateTestCommand(Configuration configuration)
        {
            var pathArg = new Argument("path", $"Path to the assembly (*.dll, *.exe) to test.")
            {
                HelpName = "PATH"
            };

            var methodOption = new Option<string>(
                aliases: new[] { "-m", "--method" },
                description: "Suffix of the test method to execute.")
            {
                ArgumentHelpName = "METHOD"
            };

            var iterationsOption = new Option<int>(
                aliases: new[] { "-i", "--iterations" },
                getDefaultValue: () => (int)configuration.TestingIterations,
                description: "Number of testing iterations to run.")
            {
                ArgumentHelpName = "ITERATIONS"
            };

            var timeoutOption = new Option<int>(
                aliases: new[] { "-t", "--timeout" },
                getDefaultValue: () => configuration.TestingTimeout,
                description: "Timeout in seconds after which no more testing iterations will run (disabled by default).")
            {
                ArgumentHelpName = "TIMEOUT"
            };

            var allowedStrategies = new HashSet<string>
            {
                "random",
                "prioritization",
                "fair-prioritization",
                "probabilistic",
                "rl",
                "portfolio"
            };

            var strategyOption = new Option<string>(
                aliases: new[] { "-s", "--strategy" },
                getDefaultValue: () => configuration.SchedulingStrategy,
                description: "Set exploration strategy to use during testing. The exploration strategy " +
                    "controls all scheduling decisions and nondeterministic choices. " +
                    $"Allowed values are {string.Join(", ", allowedStrategies)}.")
            {
                ArgumentHelpName = "STRATEGY"
            };

            var strategyValueOption = new Option<int>(
                aliases: new[] { "-sv", "--strategy-value" },
                description: "Set exploration strategy specific value. Supported strategies (and values): " +
                    "(fair-)prioritization (maximum number of priority change points per iteration), " +
                    "probabilistic (probability of deviating from a scheduled operation).")
            {
                ArgumentHelpName = "VALUE"
            };

            var maxStepsOption = new Option<int>(
                aliases: new[] { "-ms", "--max-steps" },
                description: "Max scheduling steps (i.e. decisions) to be explored during testing. " +
                    "Choosing value 'STEPS' sets 'STEPS' unfair max-steps and 'STEPS*10' fair steps.")
            {
                ArgumentHelpName = "STEPS"
            };

            var maxFairStepsOption = new Option<int>(
                name: "--max-fair-steps",
                getDefaultValue: () => configuration.MaxFairSchedulingSteps,
                description: "Max fair scheduling steps (i.e. decisions) to be explored during testing. " +
                    "Used by exploration strategies that perform fair scheduling.")
            {
                ArgumentHelpName = "STEPS"
            };

            var maxUnfairStepsOption = new Option<int>(
                name: "--max-unfair-steps",
                getDefaultValue: () => configuration.MaxUnfairSchedulingSteps,
                description: "Max unfair scheduling steps (i.e. decisions) to be explored during testing. " +
                    "Used by exploration strategies that perform unfair scheduling.")
            {
                ArgumentHelpName = "STEPS"
            };

            var fuzzOption = new Option<bool>(
                name: "--fuzz",
                description: "Use systematic fuzzing instead of controlled testing.")
            {
                Arity = ArgumentArity.Zero
            };

            var coverageOption = new Option<bool>(
                aliases: new[] { "-c", "--coverage" },
                description: "Generate coverage reports if supported for the programming model used by the test.")
            {
                Arity = ArgumentArity.Zero
            };

            var graphOption = new Option<bool>(
                name: "--graph",
                description: "Output a DGML graph that visualizes the failing execution path if a bug is found.")
            {
                Arity = ArgumentArity.Zero
            };

            var xmlLogOption = new Option<bool>(
                name: "--xml-trace",
                description: "Output an XML formatted runtime log file.")
            {
                Arity = ArgumentArity.Zero
            };

            var reduceSharedStateOption = new Option<bool>(
                name: "--reduce-shared-state",
                description: "Enables shared state reduction based on 'READ' and 'WRITE' scheduling points.")
            {
                Arity = ArgumentArity.Zero
            };

            var seedOption = new Option<int>(
                name: "--seed",
                description: "Specify the random value generator seed.")
            {
                ArgumentHelpName = "VALUE"
            };

            var livenessTemperatureThresholdOption = new Option<int>(
                name: "--liveness-temperature-threshold",
                getDefaultValue: () => configuration.LivenessTemperatureThreshold,
                description: "Specify the threshold (in number of steps) that triggers a liveness bug.")
            {
                ArgumentHelpName = "THRESHOLD"
            };

            var timeoutDelayOption = new Option<int>(
                name: "--timeout-delay",
                getDefaultValue: () => (int)configuration.TimeoutDelay,
                description: "Controls the frequency of timeouts by built-in timers (not a unit of time).")
            {
                ArgumentHelpName = "DELAY"
            };

            var deadlockTimeoutOption = new Option<int>(
                name: "--deadlock-timeout",
                getDefaultValue: () => (int)configuration.DeadlockTimeout,
                description: "Controls how much time (in ms) to wait before reporting a potential deadlock.")
            {
                ArgumentHelpName = "TIMEOUT"
            };

            var uncontrolledConcurrencyTimeoutOption = new Option<int>(
                name: "--uncontrolled-concurrency-timeout",
                getDefaultValue: () => (int)configuration.UncontrolledConcurrencyResolutionTimeout,
                description: "Controls how much time (in ms) to try resolve uncontrolled concurrency.")
            {
                ArgumentHelpName = "TIMEOUT"
            };

            var skipPotentialDeadlocksOption = new Option<bool>(
                name: "--skip-potential-deadlocks",
                description: "Only report a deadlock when the runtime can fully determine that it is genuine " +
                    "and not due to partially-controlled concurrency.")
            {
                Arity = ArgumentArity.Zero
            };

            var failOnMaxStepsOption = new Option<bool>(
                name: "--fail-on-maxsteps",
                description: "Reaching the specified max-steps is considered a bug.")
            {
                Arity = ArgumentArity.Zero
            };

            var noFuzzingFallbackOption = new Option<bool>(
                name: "--no-fuzzing-fallback",
                description: "Disable automatic fallback to systematic fuzzing upon detecting uncontrolled concurrency.")
            {
                Arity = ArgumentArity.Zero
            };

            var noPartialControlOption = new Option<bool>(
                name: "--no-partial-control",
                description: "Disallow partially controlled concurrency during controlled testing.")
            {
                Arity = ArgumentArity.Zero
            };

            var noReproOption = new Option<bool>(
                name: "--no-repro",
                description: "Disable bug trace repro to ignore uncontrolled concurrency errors.")
            {
                Arity = ArgumentArity.Zero
            };

            var exploreOption = new Option<bool>(
                name: "--explore",
                description: "Keep testing until the bound (e.g. iteration or time) is reached.")
            {
                Arity = ArgumentArity.Zero,
                IsHidden = true
            };

            var breakOption = new Option<bool>(
                aliases: new[] { "-b", "--break" },
                description: "Attaches the debugger and adds a breakpoint when an assertion fails.")
            {
                Arity = ArgumentArity.Zero
            };

            var outputDirectoryOption = new Option<string>(
                aliases: new[] { "-o", "--outdir" },
                description: "Output directory for emitting reports. This can be an absolute path or relative to current directory.")
            {
                ArgumentHelpName = "PATH"
            };

            // Add validators.
            pathArg.AddValidator(result => ValidateArgumentValueIsExpectedFile(result, ".dll", ".exe"));
            iterationsOption.AddValidator(result => ValidateOptionValueIsUnsignedInteger(result));
            timeoutOption.AddValidator(result => ValidateOptionValueIsUnsignedInteger(result));
            strategyOption.AddValidator(result => ValidateOptionValueIsAllowed(result, allowedStrategies));
            strategyValueOption.AddValidator(result => ValidatePrerequisiteOptionValueIsAvailable(result, strategyOption));
            maxStepsOption.AddValidator(result => ValidateOptionValueIsUnsignedInteger(result));
            maxStepsOption.AddValidator(result => ValidateExclusiveOptionValueIsAvailable(result, maxFairStepsOption));
            maxStepsOption.AddValidator(result => ValidateExclusiveOptionValueIsAvailable(result, maxUnfairStepsOption));
            maxFairStepsOption.AddValidator(result => ValidateOptionValueIsUnsignedInteger(result));
            maxFairStepsOption.AddValidator(result => ValidateExclusiveOptionValueIsAvailable(result, maxStepsOption));
            maxUnfairStepsOption.AddValidator(result => ValidateOptionValueIsUnsignedInteger(result));
            maxUnfairStepsOption.AddValidator(result => ValidateExclusiveOptionValueIsAvailable(result, maxStepsOption));
            seedOption.AddValidator(result => ValidateOptionValueIsUnsignedInteger(result));
            livenessTemperatureThresholdOption.AddValidator(result => ValidateOptionValueIsUnsignedInteger(result));
            timeoutDelayOption.AddValidator(result => ValidateOptionValueIsUnsignedInteger(result));
            deadlockTimeoutOption.AddValidator(result => ValidateOptionValueIsUnsignedInteger(result));
            uncontrolledConcurrencyTimeoutOption.AddValidator(result => ValidateOptionValueIsUnsignedInteger(result));

            // Build command.
            var command = new Command("test", "Run tests using the Coyote systematic testing engine.\n" +
                $"Learn more at {LearnAboutTestUrl}.");
            this.AddArgument(command, pathArg);
            this.AddOption(command, methodOption);
            this.AddOption(command, iterationsOption);
            this.AddOption(command, timeoutOption);
            this.AddOption(command, strategyOption);
            this.AddOption(command, strategyValueOption);
            this.AddOption(command, maxStepsOption);
            this.AddOption(command, maxFairStepsOption);
            this.AddOption(command, maxUnfairStepsOption);
            this.AddOption(command, fuzzOption);
            this.AddOption(command, coverageOption);
            this.AddOption(command, graphOption);
            this.AddOption(command, xmlLogOption);
            this.AddOption(command, reduceSharedStateOption);
            this.AddOption(command, seedOption);
            this.AddOption(command, livenessTemperatureThresholdOption);
            this.AddOption(command, timeoutDelayOption);
            this.AddOption(command, deadlockTimeoutOption);
            this.AddOption(command, uncontrolledConcurrencyTimeoutOption);
            this.AddOption(command, skipPotentialDeadlocksOption);
            this.AddOption(command, failOnMaxStepsOption);
            this.AddOption(command, noFuzzingFallbackOption);
            this.AddOption(command, noPartialControlOption);
            this.AddOption(command, noReproOption);
            this.AddOption(command, exploreOption);
            this.AddOption(command, breakOption);
            this.AddOption(command, outputDirectoryOption);
            command.TreatUnmatchedTokensAsErrors = true;
            return command;
        }

        /// <summary>
        /// Creates the replay command.
        /// </summary>
        private Command CreateReplayCommand()
        {
            var pathArg = new Argument("path", $"Path to the assembly (*.dll, *.exe) to replay.")
            {
                HelpName = "PATH"
            };

            var scheduleFileArg = new Argument("schedule", $"*.schedule file containing the execution to replay.")
            {
                HelpName = "SCHEDULE_FILE"
            };

            var methodOption = new Option<string>(
                aliases: new[] { "-m", "--method" },
                description: "Suffix of the test method to execute.")
            {
                ArgumentHelpName = "METHOD"
            };

            var breakOption = new Option<bool>(
                aliases: new[] { "-b", "--break" },
                description: "Attaches the debugger and adds a breakpoint when an assertion fails.")
            {
                Arity = ArgumentArity.Zero
            };

            var outputDirectoryOption = new Option<string>(
                aliases: new[] { "-o", "--outdir" },
                description: "Output directory for emitting reports. This can be an absolute path or relative to current directory.")
            {
                ArgumentHelpName = "PATH"
            };

            // Add validators.
            pathArg.AddValidator(result => ValidateArgumentValueIsExpectedFile(result, ".dll", ".exe"));
            scheduleFileArg.AddValidator(result => ValidateArgumentValueIsExpectedFile(result, ".schedule"));

            // Build command.
            var command = new Command("replay", "Replay bugs that Coyote discovered during systematic testing.\n" +
                $"Learn more at {LearnAboutReplayUrl}.");
            this.AddArgument(command, pathArg);
            this.AddArgument(command, scheduleFileArg);
            this.AddOption(command, methodOption);
            this.AddOption(command, breakOption);
            this.AddOption(command, outputDirectoryOption);
            command.TreatUnmatchedTokensAsErrors = true;
            return command;
        }

        /// <summary>
        /// Creates the rewrite command.
        /// </summary>
        private Command CreateRewriteCommand()
        {
            var pathArg = new Argument("path", "Path to the assembly (*.dll, *.exe) to rewrite or to a JSON rewriting configuration file.")
            {
                HelpName = "PATH"
            };

            var assertDataRacesOption = new Option<bool>(
                name: "--assert-data-races",
                getDefaultValue: () => false,
                description: "Add assertions for read/write data races.")
            {
                Arity = ArgumentArity.Zero,
                IsHidden = true
            };

            var rewriteDependenciesOption = new Option<bool>(
                name: "--rewrite-dependencies",
                getDefaultValue: () => false,
                description: "Rewrite all dependent assemblies that are found in the same location as the given path.")
            {
                Arity = ArgumentArity.Zero,
                IsHidden = true
            };

            var rewriteUnitTestsOption = new Option<bool>(
                name: "--rewrite-unit-tests",
                getDefaultValue: () => false,
                description: "Rewrite unit tests to automatically inject the Coyote testing engine.")
            {
                Arity = ArgumentArity.Zero,
                IsHidden = true
            };

            var rewriteThreadsOption = new Option<bool>(
                name: "--rewrite-threads",
                getDefaultValue: () => false,
                description: "Rewrite low-level threading APIs.")
            {
                Arity = ArgumentArity.Zero,
                IsHidden = true
            };

            var dumpILOption = new Option<bool>(
                name: "--dump-il",
                getDefaultValue: () => false,
                description: "Dumps the original and rewritten IL in JSON for debugging purposes.")
            {
                Arity = ArgumentArity.Zero
            };

            var dumpILDiffOption = new Option<bool>(
                name: "--dump-il-diff",
                getDefaultValue: () => false,
                description: "Dumps the IL diff in JSON for debugging purposes.")
            {
                Arity = ArgumentArity.Zero
            };

            // Add validators.
            pathArg.AddValidator(result => ValidateArgumentValueIsExpectedFile(result, ".dll", ".exe", ".json"));

            // Build command.
            var command = new Command("rewrite", "Rewrite your assemblies to inject logic that allows " +
                "Coyote to take control of the schedule during systematic testing.\n" +
                $"Learn more at {LearnAboutRewritingUrl}.");
            this.AddArgument(command, pathArg);
            this.AddOption(command, assertDataRacesOption);
            this.AddOption(command, rewriteDependenciesOption);
            this.AddOption(command, rewriteUnitTestsOption);
            this.AddOption(command, rewriteThreadsOption);
            this.AddOption(command, dumpILOption);
            this.AddOption(command, dumpILDiffOption);
            command.TreatUnmatchedTokensAsErrors = true;
            return command;
        }

        /// <summary>
        /// Adds an argument to the specified command.
        /// </summary>
        private void AddArgument(Command command, Argument argument)
        {
            command.AddArgument(argument);
            if (!this.Arguments.ContainsKey(argument.Name))
            {
                this.Arguments.Add(argument.Name, argument);
            }
        }

        /// <summary>
        /// Adds a global option to the specified command.
        /// </summary>
        private void AddGlobalOption(Command command, Option option)
        {
            command.AddGlobalOption(option);
            if (!this.Options.ContainsKey(option.Name))
            {
                this.Options.Add(option.Name, option);
            }
        }

        /// <summary>
        /// Adds an option to the specified command.
        /// </summary>
        private void AddOption(Command command, Option option)
        {
            command.AddOption(option);
            if (!this.Options.ContainsKey(option.Name))
            {
                this.Options.Add(option.Name, option);
            }
        }

        /// <summary>
        /// Validates that the specified argument result is found and has an expected file extension.
        /// </summary>
        private static void ValidateArgumentValueIsExpectedFile(ArgumentResult result, params string[] extensions)
        {
            string fileName = result.GetValueOrDefault<string>();
            string foundExtension = Path.GetExtension(fileName);
            if (!extensions.Any(extension => extension == foundExtension))
            {
                if (extensions.Length is 1)
                {
                    result.ErrorMessage = $"File '{fileName}' does not have the expected '{extensions[0]}' extension.";
                }
                else
                {
                    result.ErrorMessage = $"File '{fileName}' does not have one of the expected extensions: " +
                        $"{string.Join(", ", extensions)}.";
                }
            }
            else if (!File.Exists(fileName))
            {
                result.ErrorMessage = $"File '{fileName}' does not exist.";
            }
        }

        /// <summary>
        /// Validates that the specified option result is an unsigned integer.
        /// </summary>
        private static void ValidateOptionValueIsUnsignedInteger(OptionResult result)
        {
            if (result.Tokens.Select(token => token.Value).Where(v => !uint.TryParse(v, out _)).Any())
            {
                result.ErrorMessage = $"Please give a positive integer to option '{result.Option.Name}'.";
            }
        }

        /// <summary>
        /// Validates that the specified option result has an allowed value.
        /// </summary>
        private static void ValidateOptionValueIsAllowed(OptionResult result, IEnumerable<string> allowedValues)
        {
            if (result.Tokens.Select(token => token.Value).Where(v => !allowedValues.Contains(v)).Any())
            {
                result.ErrorMessage = $"Please give an allowed value to option '{result.Option.Name}': " +
                    $"{string.Join(", ", allowedValues)}.";
            }
        }

        /// <summary>
        /// Validates that the specified prerequisite option is available.
        /// </summary>
        private static void ValidatePrerequisiteOptionValueIsAvailable(OptionResult result, Option prerequisite)
        {
            OptionResult prerequisiteResult = result.FindResultFor(prerequisite);
            if (!result.IsImplicit && (prerequisiteResult is null || prerequisiteResult.IsImplicit))
            {
                result.ErrorMessage = $"Setting option '{result.Option.Name}' requires option '{prerequisite.Name}'.";
            }
        }

        /// <summary>
        /// Validates that the specified exclusive option is available.
        /// </summary>
        private static void ValidateExclusiveOptionValueIsAvailable(OptionResult result, Option exclusive)
        {
            OptionResult exclusiveResult = result.FindResultFor(exclusive);
            if (!result.IsImplicit && exclusiveResult != null && !exclusiveResult.IsImplicit)
            {
                result.ErrorMessage = $"Setting options '{result.Option.Name}' and '{exclusive.Name}' at the same time is not allowed.";
            }
        }

        /// <summary>
        /// Populates the configurations from the specified parse result.
        /// </summary>
        private void UpdateConfigurations(ParseResult result)
        {
            CommandResult commandResult = result.CommandResult;
            Command command = commandResult.Command;
            foreach (var symbolResult in commandResult.Children)
            {
                if (symbolResult is ArgumentResult argument)
                {
                    this.UpdateConfigurationsWithParsedArgument(command, argument);
                }
                else if (symbolResult is OptionResult option)
                {
                    this.UpdateConfigurationsWithParsedOption(option);
                }
            }
        }

        /// <summary>
        /// Updates the configuration with the specified parsed argument.
        /// </summary>
        private void UpdateConfigurationsWithParsedArgument(Command command, ArgumentResult result)
        {
            switch (result.Argument.Name)
            {
                case "path":
                    if (command.Name is "test" || command.Name is "replay")
                    {
                        // In the case of 'coyote test' or 'replay', the path is the assembly to be tested.
                        string path = Path.GetFullPath(result.GetValueOrDefault<string>());
                        this.Configuration.AssemblyToBeAnalyzed = path;
                    }
                    else if (command.Name is "rewrite")
                    {
                        // In the case of 'coyote rewrite', the path is the JSON this.Configuration file
                        // with the binary rewriting options.
                        string filename = result.GetValueOrDefault<string>();
                        if (Directory.Exists(filename))
                        {
                            // Then we want to rewrite a whole folder full of assemblies.
                            var assembliesDir = Path.GetFullPath(filename);
                            this.RewritingOptions.AssembliesDirectory = assembliesDir;
                            this.RewritingOptions.OutputDirectory = assembliesDir;
                        }
                        else
                        {
                            string extension = Path.GetExtension(filename);
                            if (string.Compare(extension, ".json", StringComparison.OrdinalIgnoreCase) is 0)
                            {
                                // Parse the rewriting options from the JSON file.
                                RewritingOptions.ParseFromJSON(this.RewritingOptions, filename);
                            }
                            else if (string.Compare(extension, ".dll", StringComparison.OrdinalIgnoreCase) is 0 ||
                                string.Compare(extension, ".exe", StringComparison.OrdinalIgnoreCase) is 0)
                            {
                                this.Configuration.AssemblyToBeAnalyzed = filename;
                                var fullPath = Path.GetFullPath(filename);
                                var assembliesDir = Path.GetDirectoryName(fullPath);
                                this.RewritingOptions.AssembliesDirectory = assembliesDir;
                                this.RewritingOptions.OutputDirectory = assembliesDir;
                                this.RewritingOptions.AssemblyPaths.Add(fullPath);
                            }
                        }
                    }

                    break;
                case "schedule":
                    if (command.Name is "replay")
                    {
                        this.Configuration.ScheduleFile = result.GetValueOrDefault<string>();
                    }

                    break;
                default:
                    throw new Exception(string.Format("Unhandled parsed argument '{0}'.", result.Argument.Name));
            }
        }

        /// <summary>
        /// Updates the configuration with the specified parsed option.
        /// </summary>
        private void UpdateConfigurationsWithParsedOption(OptionResult result)
        {
            if (!result.IsImplicit)
            {
                switch (result.Option.Name)
                {
                    case "method":
                        this.Configuration.TestMethodName = result.GetValueOrDefault<string>();
                        break;
                    case "iterations":
                        this.Configuration.TestingIterations = (uint)result.GetValueOrDefault<int>();
                        break;
                    case "timeout":
                        this.Configuration.TestingTimeout = result.GetValueOrDefault<int>();
                        break;
                    case "strategy":
                        var strategyBound = result.FindResultFor(this.Options["strategy-value"]);
                        string strategy = result.GetValueOrDefault<string>();
                        switch (strategy)
                        {
                            case "prioritization":
                            case "fair-prioritization":
                                if (strategyBound is null)
                                {
                                    this.Configuration.StrategyBound = 10;
                                }

                                break;
                            case "probabilistic":
                                if (strategyBound is null)
                                {
                                    this.Configuration.StrategyBound = 3;
                                }

                                break;
                            case "rl":
                                this.Configuration.IsProgramStateHashingEnabled = true;
                                break;
                            case "portfolio":
                                strategy = "random";
                                break;
                            default:
                                break;
                        }

                        this.Configuration.SchedulingStrategy = strategy;
                        break;
                    case "strategy-value":
                        this.Configuration.StrategyBound = result.GetValueOrDefault<int>();
                        break;
                    case "max-steps":
                        this.Configuration.WithMaxSchedulingSteps((uint)result.GetValueOrDefault<int>());
                        break;
                    case "max-fair-steps":
                        var maxUnfairSteps = result.FindResultFor(this.Options["max-unfair-steps"]);
                        this.Configuration.WithMaxSchedulingSteps(
                            (uint)(maxUnfairSteps?.GetValueOrDefault<int>() ?? this.Configuration.MaxUnfairSchedulingSteps),
                            (uint)result.GetValueOrDefault<int>());
                        break;
                    case "max-unfair-steps":
                        var maxFairSteps = result.FindResultFor(this.Options["max-fair-steps"]);
                        this.Configuration.WithMaxSchedulingSteps(
                            (uint)result.GetValueOrDefault<int>(),
                            (uint)(maxFairSteps?.GetValueOrDefault<int>() ?? this.Configuration.MaxFairSchedulingSteps));
                        break;
                    case "fuzz":
                    case "no-repro":
                        this.Configuration.IsSystematicFuzzingEnabled = true;
                        break;
                    case "coverage":
                        this.Configuration.IsActivityCoverageReported = true;
                        break;
                    case "graph":
                        this.Configuration.IsTraceVisualizationEnabled = true;
                        break;
                    case "xml-trace":
                        this.Configuration.IsXmlLogEnabled = true;
                        break;
                    case "reduce-shared-state":
                        this.Configuration.IsSharedStateReductionEnabled = true;
                        break;
                    case "seed":
                        this.Configuration.RandomGeneratorSeed = (uint)result.GetValueOrDefault<int>();
                        break;
                    case "liveness-temperature-threshold":
                        this.Configuration.LivenessTemperatureThreshold = result.GetValueOrDefault<int>();
                        this.Configuration.UserExplicitlySetLivenessTemperatureThreshold = true;
                        break;
                    case "timeout-delay":
                        this.Configuration.TimeoutDelay = (uint)result.GetValueOrDefault<int>();
                        break;
                    case "deadlock-timeout":
                        this.Configuration.DeadlockTimeout = (uint)result.GetValueOrDefault<int>();
                        break;
                    case "uncontrolled-concurrency-timeout":
                        this.Configuration.UncontrolledConcurrencyResolutionTimeout = (uint)result.GetValueOrDefault<int>();
                        break;
                    case "skip-potential-deadlocks":
                        this.Configuration.ReportPotentialDeadlocksAsBugs = false;
                        break;
                    case "fail-on-maxsteps":
                        this.Configuration.ConsiderDepthBoundHitAsBug = true;
                        break;
                    case "no-fuzzing-fallback":
                        this.Configuration.IsSystematicFuzzingFallbackEnabled = false;
                        break;
                    case "no-partial-control":
                        this.Configuration.IsPartiallyControlledConcurrencyAllowed = false;
                        break;
                    case "explore":
                        this.Configuration.RunTestIterationsToCompletion = true;
                        break;
                    case "break":
                        this.Configuration.AttachDebugger = true;
                        break;
                    case "outdir":
                        this.Configuration.OutputFilePath = result.GetValueOrDefault<string>();
                        break;
                    case "debug":
                        this.Configuration.IsDebugVerbosityEnabled = true;
                        Debug.IsEnabled = true;
                        break;
                    case "assert-data-races":
                        this.RewritingOptions.IsDataRaceCheckingEnabled = true;
                        break;
                    case "rewrite-dependencies":
                        this.RewritingOptions.IsRewritingDependencies = true;
                        break;
                    case "rewrite-unit-tests":
                        this.RewritingOptions.IsRewritingUnitTests = true;
                        break;
                    case "rewrite-threads":
                        this.RewritingOptions.IsRewritingThreads = true;
                        break;
                    case "dump-il":
                        this.RewritingOptions.IsLoggingAssemblyContents = true;
                        break;
                    case "dump-il-diff":
                        this.RewritingOptions.IsDiffingAssemblyContents = true;
                        break;
                    case "verbosity":
                        switch (result.GetValueOrDefault<string>())
                        {
                            case "quiet":
                                this.Configuration.IsVerbose = false;
                                break;
                            case "minimal":
                                this.Configuration.LogLevel = LogSeverity.Error;
                                this.Configuration.IsVerbose = true;
                                break;
                            case "normal":
                                this.Configuration.LogLevel = LogSeverity.Warning;
                                this.Configuration.IsVerbose = true;
                                break;
                            case "detailed":
                            default:
                                this.Configuration.LogLevel = LogSeverity.Informational;
                                this.Configuration.IsVerbose = true;
                                break;
                        }

                        break;
                    case "help":
                        break;
                    default:
                        throw new Exception(string.Format("Unhandled parsed option '{0}.", result.Option.Name));
                }
            }
        }

        /// <summary>
        /// Returns true if the user is asking for help.
        /// </summary>
        private static bool IsHelpRequested(ParseResult result) => result.CommandResult.Children
            .OfType<OptionResult>()
            .Any(result => result.Option.Name is "help" && !result.IsImplicit);

        /// <summary>
        /// Prints the detailed Coyote version.
        /// </summary>
        private static void PrintDetailedCoyoteVersion()
        {
            Console.WriteLine("Microsoft (R) Coyote version {0} for .NET{1}",
                typeof(CommandLineParser).Assembly.GetName().Version, GetDotNetVersion());
            Console.WriteLine("Copyright (C) Microsoft Corporation. All rights reserved.\n");
        }

        /// <summary>
        /// Returns the current .NET version.
        /// </summary>
        private static string GetDotNetVersion()
        {
            var path = typeof(string).Assembly.Location;
            string result = string.Empty;

            string[] parts = path.Replace("\\", "/").Split('/');
            if (parts.Length > 2)
            {
                var version = parts[parts.Length - 2];
                if (char.IsDigit(version[0]))
                {
                    result += " " + version;
                }
            }

            return result;
        }
    }
}
