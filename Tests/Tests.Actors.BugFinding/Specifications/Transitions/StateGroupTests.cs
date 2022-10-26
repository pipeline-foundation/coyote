﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Coyote.Specifications;
using Microsoft.Coyote.Tests.Common.Events;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Coyote.Actors.BugFinding.Tests.Specifications
{
    public class StateGroupTests : BaseActorBugFindingTest
    {
        public StateGroupTests(ITestOutputHelper output)
            : base(output)
        {
        }

        private class Safety : Monitor
        {
            private class States1 : StateGroup
            {
                [Start]
                [OnEventGotoState(typeof(MonitorUnitEvent), typeof(S2))]
                public class S1 : State
                {
                }

                [OnEntry(nameof(States1S2OnEntry))]
                [OnEventGotoState(typeof(MonitorUnitEvent), typeof(States2.S1))]
                public class S2 : State
                {
                }
            }

            private class States2 : StateGroup
            {
                [OnEntry(nameof(States2S1OnEntry))]
                [OnEventGotoState(typeof(MonitorUnitEvent), typeof(S2))]
                public class S1 : State
                {
                }

                [OnEntry(nameof(States2S2OnEntry))]
                public class S2 : State
                {
                }
            }

            private void States1S2OnEntry() => this.RaiseEvent(MonitorUnitEvent.Instance);

            private void States2S1OnEntry() => this.RaiseEvent(MonitorUnitEvent.Instance);

            private void States2S2OnEntry()
            {
                this.Assert(false, "Reached test assertion.");
            }
        }

        [Fact(Timeout = 5000)]
        public void TestStateGroup()
        {
            this.TestWithError(r =>
            {
                r.RegisterMonitor<Safety>();
                r.Monitor<Safety>(MonitorUnitEvent.Instance);
            },
            expectedError: "Reached test assertion.",
            replay: true);
        }
    }
}
