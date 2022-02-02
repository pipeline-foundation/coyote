﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using RuntimeCompiler = Microsoft.Coyote.Runtime.CompilerServices;
using SystemCompiler = System.Runtime.CompilerServices;
using SystemConcurrentCollections = System.Collections.Concurrent;
using SystemGenericCollections = System.Collections.Generic;
using SystemTasks = System.Threading.Tasks;
using SystemThreading = System.Threading;

namespace Microsoft.Coyote.Rewriting.Types
{
    /// <summary>
    /// Cache of known types that can be rewritten.
    /// </summary>
    internal static class NameCache
    {
        internal static string RuntimeCompilerNamespace { get; } = typeof(RuntimeCompiler.AsyncTaskMethodBuilder).Namespace;
        internal static string SystemCompilerNamespace { get; } = typeof(SystemCompiler.AsyncTaskMethodBuilder).Namespace;
        internal static string SystemTasksNamespace { get; } = typeof(SystemTasks.Task).Namespace;

        internal static string TaskName { get; } = typeof(SystemTasks.Task).Name;
        internal static string Task { get; } = typeof(SystemTasks.Task).FullName;
        internal static string GenericTask { get; } = typeof(SystemTasks.Task<>).FullName;
#if NET
        internal static string TaskCompletionSource { get; } = typeof(SystemTasks.TaskCompletionSource).FullName;
#endif
        internal static string GenericTaskCompletionSource { get; } = typeof(SystemTasks.TaskCompletionSource<>).FullName;

        internal static string AsyncTaskMethodBuilderName { get; } = typeof(SystemCompiler.AsyncTaskMethodBuilder).Name;
        internal static string AsyncTaskMethodBuilder { get; } = typeof(SystemCompiler.AsyncTaskMethodBuilder).FullName;
        internal static string GenericAsyncTaskMethodBuilder { get; } = typeof(SystemCompiler.AsyncTaskMethodBuilder<>).FullName;

        internal static string TaskAwaiterName { get; } = typeof(SystemCompiler.TaskAwaiter).Name;
        internal static string TaskAwaiter { get; } = typeof(SystemCompiler.TaskAwaiter).FullName;
        internal static string GenericTaskAwaiter { get; } = typeof(SystemCompiler.TaskAwaiter<>).FullName;
        internal static string ConfiguredTaskAwaitable { get; } = typeof(SystemCompiler.ConfiguredTaskAwaitable).FullName;
        internal static string GenericConfiguredTaskAwaitable { get; } = typeof(SystemCompiler.ConfiguredTaskAwaitable<>).FullName;
        internal static string ConfiguredTaskAwaiter { get; } =
            typeof(SystemCompiler.ConfiguredTaskAwaitable).FullName + "/ConfiguredTaskAwaiter";
        internal static string GenericConfiguredTaskAwaiter { get; } =
            typeof(SystemCompiler.ConfiguredTaskAwaitable<>).FullName + "/ConfiguredTaskAwaiter";

        internal static string TaskExtensions { get; } = typeof(SystemTasks.TaskExtensions).FullName;
        internal static string TaskFactory { get; } = typeof(SystemTasks.TaskFactory).FullName;
        internal static string GenericTaskFactory { get; } = typeof(SystemTasks.TaskFactory<>).FullName;
        internal static string TaskParallel { get; } = typeof(SystemTasks.Parallel).FullName;

        internal static string Monitor { get; } = typeof(SystemThreading.Monitor).FullName;

        internal static string GenericList { get; } = typeof(SystemGenericCollections.List<>).FullName;
        internal static string GenericDictionary { get; } = typeof(SystemGenericCollections.Dictionary<,>).FullName;
        internal static string GenericHashSet { get; } = typeof(SystemGenericCollections.HashSet<>).FullName;

        internal static string ConcurrentBag { get; } = typeof(SystemConcurrentCollections.ConcurrentBag<>).FullName;
        internal static string ConcurrentDictionary { get; } = typeof(SystemConcurrentCollections.ConcurrentDictionary<,>).FullName;
        internal static string ConcurrentQueue { get; } = typeof(SystemConcurrentCollections.ConcurrentQueue<>).FullName;
        internal static string ConcurrentStack { get; } = typeof(SystemConcurrentCollections.ConcurrentStack<>).FullName;
    }
}
