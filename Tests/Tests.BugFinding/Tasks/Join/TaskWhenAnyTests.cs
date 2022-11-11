﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Coyote.Specifications;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Coyote.BugFinding.Tests
{
    public class TaskWhenAnyTests : BaseBugFindingTest
    {
        public TaskWhenAnyTests(ITestOutputHelper output)
            : base(output)
        {
        }

        private static async Task WriteAsync(SharedEntry entry, int value)
        {
            await Task.CompletedTask;
            entry.Value = value;
        }

        private static async Task WriteWithDelayAsync(SharedEntry entry, int value)
        {
            await Task.Delay(1);
            entry.Value = value;
        }

        [Fact(Timeout = 5000)]
        public void TestWhenAnyWithTwoSynchronousTasks()
        {
            this.TestWithError(async () =>
            {
                SharedEntry entry = new SharedEntry();
                Task task1 = WriteAsync(entry, 5);
                Task task2 = WriteAsync(entry, 3);
                Task result = await Task.WhenAny(task1, task2);
                Specification.Assert(result.IsCompleted, "No task has completed.");
                AssertSharedEntryValue(entry, 5);
            },
            configuration: this.GetConfiguration().WithTestingIterations(200),
            expectedError: "Value is 3 instead of 5.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestWhenAnyWithTwoAsynchronousTasks()
        {
            this.TestWithError(async () =>
            {
                SharedEntry entry = new SharedEntry();
                Task task1 = WriteWithDelayAsync(entry, 3);
                Task task2 = WriteWithDelayAsync(entry, 5);
                Task result = await Task.WhenAny(task1, task2);
                Specification.Assert(result.IsCompleted, "No task has completed.");
                AssertSharedEntryValue(entry, 5);
            },
            configuration: this.GetConfiguration().WithTestingIterations(200),
            expectedError: "Value is 3 instead of 5.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestWhenAnyWithTwoParallelTasks()
        {
            this.TestWithError(async () =>
            {
                SharedEntry entry = new SharedEntry();

                Task task1 = Task.Run(async () =>
                {
                    await WriteAsync(entry, 3);
                });

                Task task2 = Task.Run(async () =>
                {
                    await WriteAsync(entry, 5);
                });

                Task result = await Task.WhenAny(task1, task2);

                Specification.Assert(result.IsCompleted, "No task has completed.");
                AssertSharedEntryValue(entry, 5);
            },
            configuration: this.GetConfiguration().WithTestingIterations(200),
            expectedError: "Value is 3 instead of 5.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestWhenAnyWithTwoSynchronousTaskWithResults()
        {
            this.TestWithError(async () =>
            {
                SharedEntry entry = new SharedEntry();
                Task<int> task1 = entry.GetWriteResultAsync(5);
                Task<int> task2 = entry.GetWriteResultAsync(3);
                Task<int> result = await Task.WhenAny(task1, task2);
                Specification.Assert(result.IsCompleted, "One task has not completed.");
                Specification.Assert(
                    (result.Id == task1.Id && result.Result == 5) ||
                    (result.Id == task2.Id && result.Result is 3),
                    "Found unexpected value.");
                AssertSharedEntryValue(entry, 5);
            },
            configuration: this.GetConfiguration().WithTestingIterations(200),
            expectedError: "Value is 3 instead of 5.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestWhenAnyWithTwoAsynchronousTaskWithResults()
        {
            this.TestWithError(async () =>
            {
                SharedEntry entry = new SharedEntry();
                Task<int> task1 = entry.GetWriteResultWithDelayAsync(5);
                Task<int> task2 = entry.GetWriteResultWithDelayAsync(3);
                Task<int> result = await Task.WhenAny(task1, task2);
                Specification.Assert(result.IsCompleted, "One task has not completed.");
                Specification.Assert((result.Id == task1.Id && result.Result == 5) ||
                    (result.Id == task2.Id && result.Result is 3), "Found unexpected value.");
            },
            configuration: this.GetConfiguration().WithTestingIterations(200),
            expectedError: "Found unexpected value.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestWhenAnyWithTwoParallelSynchronousTaskWithResults()
        {
            this.TestWithError(async () =>
            {
                SharedEntry entry = new SharedEntry();

                Task<int> task1 = Task.Run(async () =>
                {
                    return await entry.GetWriteResultAsync(5);
                });

                Task<int> task2 = Task.Run(async () =>
                {
                    return await entry.GetWriteResultAsync(3);
                });

                Task<int> result = await Task.WhenAny(task1, task2);

                Specification.Assert(result.IsCompleted, "One task has not completed.");
                Specification.Assert((result.Id == task1.Id && result.Result == 5) ||
                    (result.Id == task2.Id && result.Result is 3), "Found unexpected value.");
                AssertSharedEntryValue(entry, 5);
            },
            configuration: this.GetConfiguration().WithTestingIterations(200),
            expectedError: "Value is 3 instead of 5.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestWhenAnyWithTwoParallelAsynchronousTaskWithResults()
        {
            this.TestWithError(async () =>
            {
                SharedEntry entry = new SharedEntry();

                Task<int> task1 = Task.Run(async () =>
                {
                    return await entry.GetWriteResultWithDelayAsync(5);
                });

                Task<int> task2 = Task.Run(async () =>
                {
                    return await entry.GetWriteResultWithDelayAsync(3);
                });

                Task<int> result = await Task.WhenAny(task1, task2);

                Specification.Assert(result.IsCompleted, "One task has not completed.");
                Specification.Assert((result.Id == task1.Id && result.Result == 5) ||
                    (result.Id == task2.Id && result.Result is 3), "Found unexpected value.");
            },
            configuration: this.GetConfiguration().WithTestingIterations(200),
            expectedError: "Found unexpected value.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestWhenAnyWithAsyncCaller()
        {
            this.TestWithError(async () =>
            {
                SharedEntry entry = new SharedEntry();
                Func<Task> whenAny = async () =>
                {
                    List<Task> tasks = new List<Task>();
                    for (int i = 0; i < 2; i++)
                    {
                        tasks.Add(Task.Delay(1));
                    }

                    entry.Value = 3;
                    await await Task.WhenAny(tasks);
                    entry.Value = 1;
                };

                var task = whenAny();
                AssertSharedEntryValue(entry, 1);
                await task;
            },
            configuration: this.GetConfiguration().WithTestingIterations(200),
            expectedError: "Value is 3 instead of 1.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestWhenAnyWithResultAndAsyncCaller()
        {
            this.TestWithError(async () =>
            {
                SharedEntry entry = new SharedEntry();
                Func<Task> whenAll = async () =>
                {
                    List<Task<int>> tasks = new List<Task<int>>();
                    for (int i = 0; i < 2; i++)
                    {
                        tasks.Add(Task.Run(() => 1));
                    }

                    entry.Value = 3;
                    await await Task.WhenAny(tasks);
                    entry.Value = 1;
                };

                var task = whenAll();
                AssertSharedEntryValue(entry, 1);
                await task;
            },
            configuration: this.GetConfiguration().WithTestingIterations(200),
            expectedError: "Value is 3 instead of 1.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestWhenAnyWithBlockingWait()
        {
            this.Test(() =>
            {
                SharedEntry entry = new SharedEntry();
                Task task1 = WriteWithDelayAsync(entry, 5);
                Task task2 = WriteWithDelayAsync(entry, 3);
                var task = Task.WhenAny(task1, task2);
                task.Wait();
            },
            configuration: this.GetConfiguration().WithTestingIterations(100));
        }

        [Fact(Timeout = 5000)]
        public void TestWhenAnyWithException()
        {
            this.TestWithError(async () =>
            {
                Task task1 = Task.Run(async () =>
                {
                    await Task.CompletedTask;
                    ThrowException<InvalidOperationException>();
                });

                Task task2 = Task.Run(async () =>
                {
                    await Task.CompletedTask;
                    ThrowException<NotSupportedException>();
                });

                Task result = await Task.WhenAny(task1, task2);

                Specification.Assert(result.IsFaulted, "No task has faulted.");
                Specification.Assert(
                        (task1.IsFaulted && task1.Exception.InnerException.GetType() == typeof(InvalidOperationException)) ||
                        (task2.IsFaulted && task2.Exception.InnerException.GetType() == typeof(NotSupportedException)),
                        "The exception is not of the expected type.");
                Specification.Assert(false, "Reached test assertion.");
            },
            configuration: this.GetConfiguration().WithTestingIterations(200),
            expectedError: "Reached test assertion.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestWhenAnyWithResultsAndException()
        {
            this.TestWithError(async () =>
            {
                Task<int> task1 = Task.Run(async () =>
                {
                    await Task.CompletedTask;
                    ThrowException<InvalidOperationException>();
                    return 1;
                });

                Task<int> task2 = Task.Run(async () =>
                {
                    await Task.CompletedTask;
                    ThrowException<NotSupportedException>();
                    return 3;
                });

                Task result = await Task.WhenAny(task1, task2);

                Specification.Assert(result.IsFaulted, "No task has faulted.");
                Specification.Assert(
                    (task1.IsFaulted && task1.Exception.InnerException.GetType() == typeof(InvalidOperationException)) ||
                    (task2.IsFaulted && task2.Exception.InnerException.GetType() == typeof(NotSupportedException)),
                    "The exception is not of the expected type.");
                Specification.Assert(false, "Reached test assertion.");
            },
            configuration: this.GetConfiguration().WithTestingIterations(200),
            expectedError: "Reached test assertion.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestWhenAnyWithIncompleteTask()
        {
            this.Test(async () =>
            {
                // Test that `WhenAny` can complete even if one of the tasks cannot complete until later.
                var tcs = new TaskCompletionSource<bool>();
                await await Task.WhenAny(tcs.Task, Task.Delay(1));
                tcs.SetResult(true);
                await tcs.Task;
            },
            configuration: this.GetConfiguration().WithTestingIterations(200));
        }

        [Fact(Timeout = 5000)]
        public void TestWhenAnyWithIncompleteGenericTask()
        {
            this.Test(async () =>
            {
                // Test that `WhenAny` can complete even if one of the tasks cannot complete until later.
                var tcs = new TaskCompletionSource<bool>();
                await await Task.WhenAny(tcs.Task, Task.FromResult(true));
                tcs.SetResult(true);
                await tcs.Task;
            },
            configuration: this.GetConfiguration().WithTestingIterations(200));
        }
    }
}
