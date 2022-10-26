﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Coyote.Specifications;
using Xunit;
using Xunit.Abstractions;
using MonitorEvent = Microsoft.Coyote.Specifications.Monitor.Event;

namespace Microsoft.Coyote.Actors.BugFinding.Tests.Specifications
{
    public class MachineMonitorIntegrationTests : BaseActorBugFindingTest
    {
        public MachineMonitorIntegrationTests(ITestOutputHelper output)
            : base(output)
        {
        }

        private class CheckE : MonitorEvent
        {
            public bool Value;

            public CheckE(bool v)
            {
                this.Value = v;
            }
        }

        private class M1<T> : StateMachine
        {
            private readonly bool Test = false;

            [Start]
            [OnEntry(nameof(InitOnEntry))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                this.Monitor<T>(new CheckE(this.Test));
            }
        }

        private class M2 : StateMachine
        {
            private readonly bool Test = false;

            [Start]
            [OnEntry(nameof(InitOnEntry))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                this.Monitor<Spec2>(new CheckE(true));
                this.Monitor<Spec2>(new CheckE(this.Test));
            }
        }

        private class Spec1 : Monitor
        {
            [Start]
            [OnEventDoAction(typeof(CheckE), nameof(Check))]
            private class Checking : State
            {
            }

            private void Check(Event e)
            {
                this.Assert((e as CheckE).Value is true);
            }
        }

        private class Spec2 : Monitor
        {
            [Start]
            [OnEventDoAction(typeof(CheckE), nameof(Check))]
            private class Checking : State
            {
            }

#pragma warning disable CA1822 // Mark members as static
            private void Check()
#pragma warning restore CA1822 // Mark members as static
            {
            }
        }

        private class Spec3 : Monitor
        {
            [Start]
            [OnEventDoAction(typeof(CheckE), nameof(Check))]
            private class Checking : State
            {
            }

            private void Check(Event e)
            {
                this.Assert((e as CheckE).Value is false);
            }
        }

        [Fact(Timeout = 5000)]
        public void TestMachineMonitorIntegration1()
        {
            this.TestWithError(r =>
            {
                r.RegisterMonitor<Spec1>();
                r.CreateActor(typeof(M1<Spec1>));
            },
            configuration: this.GetConfiguration().WithDFSStrategy(),
            expectedError: "Detected an assertion failure.",
            replay: true);
        }

        [Fact(Timeout = 5000)]
        public void TestMachineMonitorIntegration2()
        {
            this.Test(r =>
            {
                r.RegisterMonitor<Spec2>();
                r.CreateActor(typeof(M2));
            },
            configuration: this.GetConfiguration().WithDFSStrategy());
        }

        [Fact(Timeout = 5000)]
        public void TestMachineMonitorIntegration3()
        {
            this.Test(r =>
            {
                r.RegisterMonitor<Spec3>();
                r.CreateActor(typeof(M1<Spec3>));
            },
            configuration: this.GetConfiguration().WithDFSStrategy());
        }
    }
}
