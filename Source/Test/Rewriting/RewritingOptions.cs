﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.Coyote.Rewriting
{
    /// <summary>
    /// Options for rewriting binaries.
    /// </summary>
    /// <remarks>
    /// See <see href="/coyote/get-started/rewriting">rewriting</see> for more information.
    /// </remarks>
    internal class RewritingOptions
    {
        /// <summary>
        /// The directory containing the assemblies to rewrite.
        /// </summary>
        internal string AssembliesDirectory { get; set; }

        /// <summary>
        /// The output directory where rewritten assemblies are placed.
        /// If this is the same as the <see cref="AssembliesDirectory"/> then
        /// the rewritten assemblies will replace the original assemblies.
        /// </summary>
        internal string OutputDirectory { get; set; }

        /// <summary>
        /// The file names of the assemblies to rewrite. If this list is empty then it will
        /// rewrite all assemblies in the <see cref="AssembliesDirectory"/>.
        /// </summary>
        internal HashSet<string> AssemblyPaths { get; set; }

        /// <summary>
        /// The paths to search for resolving dependencies.
        /// </summary>
        internal IList<string> DependencySearchPaths { get; set; }

        /// <summary>
        /// The regular expressions used to match against assembly names to determine which assemblies
        /// to ignore when rewriting dependencies or a whole directory.
        /// </summary>
        /// <remarks>
        /// The list automatically includes the following expressions:
        /// Microsoft\.Coyote.*
        /// Microsoft\.TestPlatform.*
        /// Microsoft\.VisualStudio\.TestPlatform.*
        /// Newtonsoft\.Json.*
        /// System\.Private\.CoreLib
        /// mscorlib.
        /// </remarks>
        private Regex IgnoredAssembliesPattern { get; set; }

        /// <summary>
        /// True if rewriting for concurrent collections is enabled, else false.
        /// </summary>
        internal bool IsRewritingConcurrentCollections { get; set; }

        /// <summary>
        /// True if rewriting for data race checking is enabled, else false.
        /// </summary>
        internal bool IsDataRaceCheckingEnabled { get; set; }

        /// <summary>
        /// True if rewriting dependent assemblies that are found in the same location is enabled, else false.
        /// </summary>
        internal bool IsRewritingDependencies { get; set; }

        /// <summary>
        /// True if rewriting of unit test methods is enabled, else false.
        /// </summary>
        internal bool IsRewritingUnitTests { get; set; }

        /// <summary>
        /// True if rewriting threads as controlled tasks.
        /// </summary>
        internal bool IsRewritingThreads { get; set; }

        /// <summary>
        /// True if the rewriter should log the IL before and after rewriting.
        /// </summary>
        internal bool IsLoggingAssemblyContents { get; set; }

        /// <summary>
        /// True if the rewriter should diff the IL before and after rewriting.
        /// </summary>
        internal bool IsDiffingAssemblyContents { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RewritingOptions"/> class.
        /// </summary>
        private RewritingOptions()
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="RewritingOptions"/> class with default values.
        /// </summary>
        internal static RewritingOptions Create() =>
            new RewritingOptions()
            {
                AssembliesDirectory = string.Empty,
                OutputDirectory = string.Empty,
                AssemblyPaths = new HashSet<string>(),
                DependencySearchPaths = null,
                IgnoredAssembliesPattern = GetDisallowedAssembliesRegex(new List<string>()),
                IsRewritingConcurrentCollections = true,
                IsDataRaceCheckingEnabled = false,
                IsRewritingDependencies = false,
                IsRewritingUnitTests = false,
                IsRewritingThreads = false,
                IsLoggingAssemblyContents = false,
                IsDiffingAssemblyContents = false,
            };

        /// <summary>
        /// Parses the <see cref="RewritingOptions"/> from the specified JSON configuration file.
        /// </summary>
        internal static RewritingOptions ParseFromJSON(string configurationPath) =>
            ParseFromJSON(new RewritingOptions(), configurationPath);

        /// <summary>
        /// Parses the JSON configuration file and merges the options into the specified
        /// <see cref="RewritingOptions"/> object.
        /// </summary>
        internal static RewritingOptions ParseFromJSON(RewritingOptions options, string configurationPath)
        {
            try
            {
                // TODO: replace with the new 'System.Text.Json'.
                using FileStream fs = new FileStream(configurationPath, FileMode.Open, FileAccess.Read);
                var serializer = new DataContractJsonSerializer(typeof(JsonConfiguration));
                JsonConfiguration configuration = (JsonConfiguration)serializer.ReadObject(fs);

                Uri baseUri = new Uri(Path.GetDirectoryName(Path.GetFullPath(configurationPath)) + Path.DirectorySeparatorChar);
                Uri resolvedUri = new Uri(baseUri, configuration.AssembliesPath);
                options.AssembliesDirectory = resolvedUri.LocalPath;
                if (string.IsNullOrEmpty(configuration.OutputPath))
                {
                    options.OutputDirectory = options.AssembliesDirectory;
                }
                else
                {
                    resolvedUri = new Uri(baseUri, configuration.OutputPath);
                    options.OutputDirectory = resolvedUri.LocalPath;
                }

                options.AssemblyPaths = new HashSet<string>();
                if (configuration.Assemblies != null)
                {
                    foreach (string assembly in configuration.Assemblies)
                    {
                        resolvedUri = new Uri(Path.Combine(options.AssembliesDirectory, assembly));
                        options.AssemblyPaths.Add(resolvedUri.LocalPath);
                    }
                }

                options.DependencySearchPaths = configuration.DependencySearchPaths;
                options.IgnoredAssembliesPattern = GetDisallowedAssembliesRegex(
                    configuration.IgnoredAssemblies ?? Array.Empty<string>());
                options.IsRewritingConcurrentCollections = configuration.IsRewritingConcurrentCollections;
                options.IsDataRaceCheckingEnabled = configuration.IsDataRaceCheckingEnabled;
                options.IsRewritingDependencies = configuration.IsRewritingDependencies;
                options.IsRewritingUnitTests = configuration.IsRewritingUnitTests;
                options.IsRewritingThreads = configuration.IsRewritingThreads;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Unexpected JSON format in the '{configurationPath}' configuration file.\n{ex.Message}");
            }

            return options;
        }

        /// <summary>
        /// Returns true if the assembly with the specified name must be ignored during rewriting, else false.
        /// </summary>
        internal bool IsAssemblyIgnored(string assemblyName) => this.IgnoredAssembliesPattern.IsMatch(assemblyName);

        /// <summary>
        /// Returns true if the input assemblies are being replaced by the rewritten ones, else false.
        /// </summary>
        internal bool IsReplacingAssemblies() => this.AssembliesDirectory == this.OutputDirectory;

        /// <summary>
        /// Returns a regex pattern with the disallowed assemblies.
        /// </summary>
        private static Regex GetDisallowedAssembliesRegex(IList<string> ignoredAssemblies)
        {
            // List of assemblies that must be ignored by default.
            string[] defaultIgnoreList = new string[]
            {
                @"Newtonsoft\.Json\.dll",
                @"Microsoft\.Coyote\.dll",
                @"Microsoft\.Coyote.Test\.dll",
                @"Microsoft\.VisualStudio\.TestPlatform.*",
                @"Microsoft\.TestPlatform.*",
                @"System\.Private\.CoreLib\.dll",
                @"mscorlib\.dll"
            };

            StringBuilder combined = new StringBuilder();
            foreach (var e in defaultIgnoreList.Concat(ignoredAssemblies))
            {
                combined.Append(combined.Length is 0 ? "(" : "|");
                combined.Append(e);
            }

            combined.Append(')');

            try
            {
                return new Regex(combined.ToString());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Unable to create a valid regular expression for ignored assemblies. {ex.Message}.");
            }
        }

        /// <summary>
        /// Sanitizes the rewriting options.
        /// </summary>
        internal RewritingOptions Sanitize(Assembly callingAssembly = null)
        {
            if (string.IsNullOrEmpty(this.AssembliesDirectory))
            {
                throw new InvalidOperationException("Please provide RewritingOptions.AssembliesDirectory");
            }
            else if (string.IsNullOrEmpty(this.OutputDirectory))
            {
                throw new InvalidOperationException("Please provide RewritingOptions.OutputDirectory");
            }
            else if (this.AssemblyPaths is null || this.AssemblyPaths.Count is 0)
            {
                throw new InvalidOperationException("Please provide RewritingOptions.AssemblyPaths");
            }

            // We try resolve assemblies in the following order: (a) the calling assembly only for our unit tests,
            // (b) the entry assembly which is typically the coyote CLI tool, and (c) the current assembly. If the
            // resolution fails,then we throw an error so that the user can be explicit.
            if (!TryResolveTargetFramework(callingAssembly, out string targetFramework) &&
                !TryResolveTargetFramework(Assembly.GetEntryAssembly(), out targetFramework) &&
                !TryResolveTargetFramework(Assembly.GetExecutingAssembly(), out targetFramework))
            {
                throw new InvalidOperationException("Unable to resolve '$(TargetFramework)', please set it explicitly.");
            }

            this.AssembliesDirectory = ResolvePath(this.AssembliesDirectory, targetFramework);
            this.OutputDirectory = ResolvePath(this.OutputDirectory, targetFramework);
            foreach (string path in this.AssemblyPaths.ToArray())
            {
                var newPath = ResolvePath(path, targetFramework);
                if (newPath != path)
                {
                    this.AssemblyPaths.Remove(path);
                    this.AssemblyPaths.Add(newPath);
                }
            }

            if (this.AssemblyPaths is null || this.AssemblyPaths.Count is 0)
            {
                // Expand folder to include all DLLs in the path.
                foreach (var file in Directory.GetFiles(this.AssembliesDirectory, "*.dll"))
                {
                    if (!this.IsAssemblyIgnored(Path.GetFileName(file)))
                    {
                        this.AssemblyPaths.Add(file);
                    }
                }
            }

            return this;
        }

        /// <summary>
        /// Resolves the specified path.
        /// </summary>
        private static string ResolvePath(string path, string targetFramework) =>
            path.Replace("$(TargetFramework)", targetFramework);

        /// <summary>
        /// Returns the resolved target framework of the specified or executing assembly.
        /// </summary>
        private static bool TryResolveTargetFramework(Assembly assembly, out string resolvedTargetFramework)
        {
            var targetFramework = assembly?.GetCustomAttributes(typeof(TargetFrameworkAttribute), false)
                .SingleOrDefault() as TargetFrameworkAttribute;
#if NET || NETCOREAPP3_1
            var tokens = targetFramework?.FrameworkName.Split(",Version=", StringSplitOptions.None);
#else
            var tokens = targetFramework?.FrameworkName.Split(new[] { ",Version=" }, StringSplitOptions.None);
#endif

            resolvedTargetFramework = string.Empty;
            if (tokens != null && tokens.Length is 2)
            {
                if (tokens[0] == ".NETCoreApp")
                {
                    resolvedTargetFramework = tokens[1] is "v7.0" ? "net7.0" :
                        tokens[1] is "v6.0" ? "net6.0" :
                        tokens[1] is "v3.1" ? "netcoreapp3.1" :
                        resolvedTargetFramework;
                }
                else if (tokens[0] == ".NETFramework")
                {
                    resolvedTargetFramework = tokens[1] is "v4.6.2" ? "net462" : resolvedTargetFramework;
                }
            }

            return !string.IsNullOrEmpty(resolvedTargetFramework);
        }

        /// <summary>
        /// Implements a JSON configuration object.
        /// </summary>
        /// <example>
        /// The JSON schema is:
        /// <code>
        /// {
        ///     // The directory with the assemblies to rewrite. This path is relative
        ///     // to this configuration file.
        ///     "AssembliesPath": "./bin/net7.0",
        ///     // The output directory where rewritten assemblies are placed. This path
        ///     // is relative to this configuration file.
        ///     "OutputPath": "./bin/net7.0/RewrittenBinaries",
        ///     // The assemblies to rewrite. The paths are relative to 'AssembliesPath'.
        ///     "Assemblies": [
        ///         "Example.exe"
        ///     ]
        /// }
        /// </code>
        /// </example>
        [DataContract]
        private class JsonConfiguration
        {
            [DataMember(Name = "AssembliesPath", IsRequired = true)]
            public string AssembliesPath { get; set; }

            [DataMember(Name = "OutputPath")]
            public string OutputPath { get; set; }

            [DataMember(Name = "Assemblies")]
            public IList<string> Assemblies { get; set; }

            [DataMember(Name = "IgnoredAssemblies")]
            public IList<string> IgnoredAssemblies { get; set; }

            [DataMember(Name = "DependencySearchPaths")]
            public IList<string> DependencySearchPaths { get; set; }

            [DataMember(Name = "IsDataRaceCheckingEnabled")]
            public bool IsDataRaceCheckingEnabled { get; set; }

            private bool? isRewritingConcurrentCollections;

            [DataMember(Name = "IsRewritingConcurrentCollections")]
            public bool IsRewritingConcurrentCollections
            {
                // This option defaults to true.
                get => this.isRewritingConcurrentCollections ?? true;
                set => this.isRewritingConcurrentCollections = value;
            }

            [DataMember(Name = "IsRewritingDependencies")]
            public bool IsRewritingDependencies { get; set; }

            [DataMember(Name = "IsRewritingUnitTests")]
            public bool IsRewritingUnitTests { get; set; }

            [DataMember(Name = "IsRewritingThreads")]
            public bool IsRewritingThreads { get; set; }
        }
    }
}
