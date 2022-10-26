﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Coyote.Specifications;
using Microsoft.Coyote.Tests.Common.Events;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Coyote.Actors.BugFinding.Tests.Specifications
{
    public class Liveness2LoopMachineTests : BaseActorBugFindingTest
    {
        public Liveness2LoopMachineTests(ITestOutputHelper output)
            : base(output)
        {
        }

        private class UserEvent : Event
        {
        }

        private class Done : Event
        {
        }

        private class EventHandler : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            [OnEventGotoState(typeof(UnitEvent), typeof(WaitForUser))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                this.CreateActor(typeof(Loop));
                this.RaiseEvent(UnitEvent.Instance);
            }

            [OnEntry(nameof(WaitForUserOnEntry))]
            [OnEventGotoState(typeof(UserEvent), typeof(HandleEvent))]
            private class WaitForUser : State
            {
            }

            private void WaitForUserOnEntry()
            {
                this.Monitor<LivenessMonitor>(new LivenessMonitor.Waiting());
                this.SendEvent(this.Id, new UserEvent());
            }

            [OnEntry(nameof(HandleEventOnEntry))]
            private class HandleEvent : State
            {
            }

            private void HandleEventOnEntry()
            {
                this.Monitor<LivenessMonitor>(new LivenessMonitor.Computing());
            }
        }

        private class Loop : StateMachine
        {
            [Start]
            [OnEntry(nameof(LoopingOnEntry))]
            [OnEventGotoState(typeof(Done), typeof(Looping))]
            private class Looping : State
            {
            }

            private void LoopingOnEntry()
            {
                this.SendEvent(this.Id, new Done());
            }
        }

        private class LivenessMonitor : Monitor
        {
            internal class Waiting : Event
            {
            }

            internal class Computing : Event
            {
            }

            [Start]
            [Cold]
            [OnEventGotoState(typeof(Waiting), typeof(CanGetUserInput))]
            [OnEventGotoState(typeof(Computing), typeof(CannotGetUserInput))]
            private class CanGetUserInput : State
            {
            }

            [Hot]
            [OnEventGotoState(typeof(Waiting), typeof(CanGetUserInput))]
            [OnEventGotoState(typeof(Computing), typeof(CannotGetUserInput))]
            private class CannotGetUserInput : State
            {
            }
        }

        [Fact(Timeout = 5000)]
        public void TestLiveness2LoopMachine()
        {
            var configuration = this.GetConfiguration();
            configuration.LivenessTemperatureThreshold = 200;
            configuration.TestingIterations = 1;

            this.TestWithError(r =>
            {
                r.RegisterMonitor<LivenessMonitor>();
                r.CreateActor(typeof(EventHandler));
            },
            configuration: configuration,
            expectedError: "LivenessMonitor detected potential liveness bug in hot state 'CannotGetUserInput'.",
            replay: true);
        }
    }
}
