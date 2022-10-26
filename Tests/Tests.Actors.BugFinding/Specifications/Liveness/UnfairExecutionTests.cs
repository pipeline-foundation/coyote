﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Coyote.Specifications;
using Microsoft.Coyote.Tests.Common.Events;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Coyote.Actors.BugFinding.Tests.Specifications
{
    public class UnfairExecutionTests : BaseActorBugFindingTest
    {
        public UnfairExecutionTests(ITestOutputHelper output)
            : base(output)
        {
        }

        private class E : Event
        {
            public ActorId A;

            public E(ActorId a)
            {
                this.A = a;
            }
        }

        private class M : StateMachine
        {
            private ActorId N;

            [Start]
            [OnEntry(nameof(SOnEntry))]
            [OnEventGotoState(typeof(UnitEvent), typeof(S2))]
            private class S : State
            {
            }

            private void SOnEntry()
            {
                this.N = this.CreateActor(typeof(N));
                this.SendEvent(this.N, new E(this.Id));
                this.RaiseEvent(UnitEvent.Instance);
            }

            [OnEntry(nameof(S2OnEntry))]
            [OnEventGotoState(typeof(UnitEvent), typeof(S2))]
            [OnEventGotoState(typeof(E), typeof(S3))]
            private class S2 : State
            {
            }

            private void S2OnEntry()
            {
                this.SendEvent(this.Id, UnitEvent.Instance);
            }

            [OnEntry(nameof(S3OnEntry))]
            private class S3 : State
            {
            }

            private void S3OnEntry()
            {
                this.Monitor<LivenessMonitor>(MonitorUnitEvent.Instance);
                this.RaiseHaltEvent();
            }
        }

        private class N : StateMachine
        {
            [Start]
            [OnEventDoAction(typeof(E), nameof(Process))]
            private class S : State
            {
            }

            private void Process(Event e)
            {
                this.SendEvent((e as E).A, new E(this.Id));
            }
        }

        private class LivenessMonitor : Monitor
        {
            [Start]
            [Hot]
            [OnEventGotoState(typeof(MonitorUnitEvent), typeof(S2))]
            private class S : State
            {
            }

            [Cold]
            private class S2 : State
            {
            }
        }

        [Fact(Timeout = 5000)]
        public void TestUnfairExecution()
        {
            this.Test(r =>
            {
                r.RegisterMonitor<LivenessMonitor>();
                r.CreateActor(typeof(M));
            },
            configuration: this.GetConfiguration()
                .WithMaxSchedulingSteps(300)
                .WithPrioritizationStrategy(false, 10)
                .WithLivenessTemperatureThreshold(150));
        }
    }
}
