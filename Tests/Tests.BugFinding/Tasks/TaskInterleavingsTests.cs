﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Coyote.Logging;
using Microsoft.Coyote.Specifications;
using Microsoft.Coyote.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Coyote.BugFinding.Tests
{
    public class TaskInterleavingsTests : BaseBugFindingTest
    {
        public TaskInterleavingsTests(ITestOutputHelper output)
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
        public void TestInterleavingsWithOneSynchronousTask()
        {
            this.Test(async () =>
            {
                SharedEntry entry = new SharedEntry();
                Task task = WriteAsync(entry, 3);
                entry.Value = 5;
                await task;
                AssertSharedEntryValue(entry, 5);
            },
            configuration: this.GetConfiguration().WithTestingIterations(200));
        }

        [Fact(Timeout = 5000)]
        public void TestInterleavingsWithOneAsynchronousTask()
        {
            this.TestWithError(async () =>
            {
                SharedEntry entry = new SharedEntry();

                Task task = WriteWithDelayAsync(entry, 3);
                entry.Value = 5;
                await task;

                AssertSharedEntryValue(entry, 5);
            },
            configuration: this.GetConfiguration().WithTestingIterations(200),
            expectedError: "Value is 3 instead of 5.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestInterleavingsWithOneParallelTask()
        {
            this.TestWithError(async () =>
            {
                SharedEntry entry = new SharedEntry();

                Task task = Task.Run(async () =>
                {
                    await WriteAsync(entry, 3);
                });

                await WriteAsync(entry, 5);
                await task;
                AssertSharedEntryValue(entry, 5);
            },
            configuration: this.GetConfiguration().WithTestingIterations(200),
            expectedError: "Value is 3 instead of 5.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestInterleavingsWithTwoSynchronousTasks()
        {
            this.Test(async () =>
            {
                SharedEntry entry = new SharedEntry();

                Task task1 = WriteAsync(entry, 3);
                Task task2 = WriteAsync(entry, 5);

                await task1;
                await task2;
                AssertSharedEntryValue(entry, 5);
            },
            configuration: this.GetConfiguration().WithTestingIterations(200));
        }

        [Fact(Timeout = 5000)]
        public void TestInterleavingsWithTwoAsynchronousTasks()
        {
            this.TestWithError(async () =>
            {
                SharedEntry entry = new SharedEntry();

                Task task1 = WriteWithDelayAsync(entry, 3);
                Task task2 = WriteWithDelayAsync(entry, 5);

                await task1;
                await task2;
                AssertSharedEntryValue(entry, 5);
            },
            configuration: this.GetConfiguration().WithTestingIterations(200),
            expectedError: "Value is 3 instead of 5.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestInterleavingsWithTwoParallelTasks()
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

                await task1;
                await task2;
                AssertSharedEntryValue(entry, 5);
            },
            configuration: this.GetConfiguration().WithTestingIterations(200),
            expectedError: "Value is 3 instead of 5.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestInterleavingsWithNestedParallelTasks()
        {
            // When this test is running in systematic fuzzing mode, it is entirely dependent on real
            // task scheduling, which even with 1000 iterations is never guaranteed to hit the assert.
            // So in order to stop this from being a flakey test we introduce some random delays to
            // "help" the two tasks discover the interleaving we need for the test to pass.
            this.TestWithError(async (runtime) =>
            {
                SharedEntry entry = new SharedEntry();

                Task task1 = Task.Run(async () =>
                {
                    Task task2 = Task.Run(async () =>
                    {
                        await Task.Delay(runtime.RandomInteger(2));
                        await WriteAsync(entry, 5);
                    });

                    await Task.Delay(runtime.RandomInteger(2));
                    await WriteAsync(entry, 3);
                    await task2;
                });

                await task1;
                AssertSharedEntryValue(entry, 5);
            },
            configuration: this.GetConfiguration().WithTestingIterations(1000),
            expectedError: "Value is 3 instead of 5.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestExploreAllInterleavings()
        {
            SortedSet<string> results = new SortedSet<string>();
            string success = "Explored interleavings.";

            var config = this.GetConfiguration().WithTestingIterations(100);
            this.TestWithError(async (runtime) =>
            {
                var logger = new MemoryLogger(VerbosityLevel.Info);
                Task task1 = Task.Run(async () =>
                {
                    logger.WriteLine("1");
                    await Task.Delay(runtime.RandomInteger(10));
                    logger.WriteLine("2");
                });

                Task task2 = Task.Run(() =>
                {
                    logger.WriteLine("3");
                });

                await Task.WhenAll(task1, task2);

                results.Add(logger.ToString());
                Specification.Assert(results.Count < 3, success);
            },
            configuration: config,
            expectedError: success);

            string expected = @"1
2
3

1
3
2

3
1
2
";
            expected = expected.NormalizeNewLines();

            string actual = string.Join("\n", results).NormalizeNewLines();
            Assert.Equal(expected, actual);
        }
    }
}
