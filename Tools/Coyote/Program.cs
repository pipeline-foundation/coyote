﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Coyote.Cli;
using Microsoft.Coyote.IO;
using Microsoft.Coyote.Rewriting;
using Microsoft.Coyote.SystematicTesting;

namespace Microsoft.Coyote
{
    /// <summary>
    /// The entry point to the Coyote tool.
    /// </summary>
    internal class Program
    {
        private static TextWriter StdOut;
        private static TextWriter StdError;

        private static readonly object ConsoleLock = new object();

        private static int Main(string[] args)
        {
            // Save these so we can force output to happen even if they have been re-routed.
            StdOut = Console.Out;
            StdError = Console.Error;

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            var parser = new CommandLineParser(args);
            if (!parser.IsSuccessful)
            {
                return (int)ExitCode.Error;
            }

            return (int)parser.InvokeSelectedCommand(RunTest, ReplayTest, RewriteAssemblies);
        }

        /// <summary>
        /// Runs the test specified in the configuration.
        /// </summary>
        private static ExitCode RunTest(Configuration configuration)
        {
            try
            {
                Console.WriteLine($". Testing {configuration.AssemblyToBeAnalyzed}.");
                using TestingEngine engine = TestingEngine.Create(configuration);
                engine.Run();

                string directory = OutputFileManager.CreateOutputDirectory(configuration);
                string fileName = OutputFileManager.GetResolvedFileName(configuration.AssemblyToBeAnalyzed, directory);

                // Emit the test reports.
                Console.WriteLine($"... Emitting trace-related reports:");
                if (engine.TryEmitReports(directory, fileName, out IEnumerable<string> reportPaths))
                {
                    foreach (var path in reportPaths)
                    {
                        Console.WriteLine($"..... Writing {path}");
                    }
                }
                else
                {
                    Console.WriteLine($"..... No test reports available.");
                }

                // Emit the coverage reports.
                Console.WriteLine($"... Emitting coverage reports:");
                if (engine.TryEmitCoverageReports(directory, fileName, out reportPaths))
                {
                    foreach (var path in reportPaths)
                    {
                        Console.WriteLine($"..... Writing {path}");
                    }
                }
                else
                {
                    Console.WriteLine($"..... No coverage reports available.");
                }

                Console.WriteLine(engine.TestReport.GetText(configuration, "..."));
                Console.WriteLine($"... Elapsed {engine.Profiler.Results()} sec.");
                return GetExitCodeFromTestReport(engine.TestReport);
            }
            catch (Exception ex)
            {
                IO.Debug.WriteLine(ex.Message);
                IO.Debug.WriteLine(ex.StackTrace);
                return ExitCode.Error;
            }
        }

        /// <summary>
        /// Replays an execution that is specified in the configuration.
        /// </summary>
        private static ExitCode ReplayTest(Configuration configuration)
        {
            try
            {
                // Load the configuration of the assembly to be replayed.
                LoadAssemblyConfiguration(configuration.AssemblyToBeAnalyzed);

                Console.WriteLine($". Reproducing trace in {configuration.AssemblyToBeAnalyzed}.");
                using TestingEngine engine = TestingEngine.Create(configuration);
                engine.Run();

                // Emit the report.
                if (engine.TestReport.NumOfFoundBugs > 0)
                {
                    Console.WriteLine(engine.GetReport());
                }

                Console.WriteLine($"... Elapsed {engine.Profiler.Results()} sec.");
                return GetExitCodeFromTestReport(engine.TestReport);
            }
            catch (Exception ex)
            {
                IO.Debug.WriteLine(ex.Message);
                IO.Debug.WriteLine(ex.StackTrace);
                return ExitCode.Error;
            }
        }

        /// <summary>
        /// Rewrites the assemblies specified in the configuration.
        /// </summary>
        private static ExitCode RewriteAssemblies(Configuration configuration, RewritingOptions options)
        {
            try
            {
                if (options.AssemblyPaths.Count is 1)
                {
                    Console.WriteLine($". Rewriting {options.AssemblyPaths.First()}.");
                }
                else
                {
                    Console.WriteLine($". Rewriting the assemblies specified in {options.AssembliesDirectory}.");
                }

                var profiler = new Profiler();
                RewritingEngine.Run(options, configuration, profiler);
                Console.WriteLine($"... Elapsed {profiler.Results()} sec.");
            }
            catch (Exception ex)
            {
                if (ex is AggregateException aex)
                {
                    ex = aex.Flatten().InnerException;
                }

                Error.Report(configuration.IsDebugVerbosityEnabled ? ex.ToString() : ex.Message);
                return ExitCode.Error;
            }

            return ExitCode.Success;
        }

        /// <summary>
        /// Loads the configuration of the specified assembly.
        /// </summary>
        private static void LoadAssemblyConfiguration(string assemblyFile)
        {
            // Load config file and absorb its settings.
            try
            {
                var configFile = System.Configuration.ConfigurationManager.OpenExeConfiguration(assemblyFile);
                var settings = configFile.AppSettings.Settings;
                foreach (var key in settings.AllKeys)
                {
                    if (System.Configuration.ConfigurationManager.AppSettings.Get(key) is null)
                    {
                        System.Configuration.ConfigurationManager.AppSettings.Set(key, settings[key].Value);
                    }
                    else
                    {
                        System.Configuration.ConfigurationManager.AppSettings.Add(key, settings[key].Value);
                    }
                }
            }
            catch (System.Configuration.ConfigurationErrorsException ex)
            {
                Error.Report(ex.Message);
            }
        }

        /// <summary>
        /// Callback invoked when an unhandled exception occurs.
        /// </summary>
        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            ReportUnhandledException((Exception)args.ExceptionObject);
            Environment.Exit((int)ExitCode.InternalError);
        }

        private static void ReportUnhandledException(Exception ex)
        {
            Console.SetOut(StdOut);
            Console.SetError(StdError);

            PrintException(ex);
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
            {
                PrintException(inner);
            }
        }

        private static ExitCode GetExitCodeFromTestReport(TestReport report) =>
            report.InternalErrors.Count > 0 ? ExitCode.InternalError :
            report.NumOfFoundBugs > 0 ? ExitCode.BugFound :
            ExitCode.Success;

        private static void PrintException(Exception ex)
        {
            lock (ConsoleLock)
            {
                Error.Report($"[CoyoteTester] unhandled exception: {ex}");
                StdOut.WriteLine(ex.StackTrace);
            }
        }
    }
}
