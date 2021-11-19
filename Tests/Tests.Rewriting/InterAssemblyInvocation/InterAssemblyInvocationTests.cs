﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Coyote.Rewriting.Tests
{
    public class InterAssemblyInvocationTests : BaseRewritingTest
    {
        public InterAssemblyInvocationTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact(Timeout = 5000)]
        public void TestInterAssemblyInvocationWithAwaiter()
        {
            this.Test(async () =>
            {
                await new Helpers.TaskAwaiter();
            },
            configuration: this.GetConfiguration().WithTestingIterations(10));
        }

        [Fact(Timeout = 5000)]
        public void TestInterAssemblyInvocationWithGenericAwaiter()
        {
            this.Test(async () =>
            {
                await new Helpers.GenericTaskAwaiter();
            },
            configuration: this.GetConfiguration().WithTestingIterations(10));
        }

        [Fact(Timeout = 5000)]
        public void TestInterAssemblyInvocationWithAwaiterWithGenericArgument()
        {
            this.Test(async () =>
            {
                await new Helpers.TaskAwaiter<int>();
            },
            configuration: this.GetConfiguration().WithTestingIterations(10));
        }
    }
}
