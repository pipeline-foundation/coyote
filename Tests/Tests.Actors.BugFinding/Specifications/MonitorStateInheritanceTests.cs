﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Coyote.Specifications;
using Microsoft.Coyote.Tests.Common.Events;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Coyote.Actors.BugFinding.Tests.Specifications
{
    public class MonitorStateInheritanceTests : BaseActorBugFindingTest
    {
        public MonitorStateInheritanceTests(ITestOutputHelper output)
            : base(output)
        {
        }

        private class M1 : Monitor
        {
            [Start]
            private class Init : BaseState
            {
            }

            [OnEventDoAction(typeof(MonitorUnitEvent), nameof(Check))]
            private abstract class BaseState : State
            {
            }

            private void Check()
            {
                this.Assert(false, "Error reached.");
            }
        }

        private class M2 : Monitor
        {
            [Start]
            private class Init : BaseState
            {
            }

            [Start]
            private class BaseState : State
            {
            }
        }

        private class M3 : Monitor
        {
            [Start]
            private class Init : BaseState
            {
            }

            [OnEntry(nameof(BaseOnEntry))]
            private class BaseState : State
            {
            }

            private void BaseOnEntry()
            {
                this.Assert(false, "Error reached.");
            }
        }

        private class M4 : Monitor
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            private class Init : BaseState
            {
            }

            [OnEntry(nameof(BaseOnEntry))]
            private class BaseState : State
            {
            }

#pragma warning disable CA1822 // Mark members as static
            private void InitOnEntry()
#pragma warning restore CA1822 // Mark members as static
            {
            }

            private void BaseOnEntry()
            {
                this.Assert(false, "Error reached.");
            }
        }

        private class M5 : Monitor
        {
            [Start]
            private class Init : BaseState
            {
            }

            [OnEventDoAction(typeof(MonitorUnitEvent), nameof(Check))]
            private class BaseState : State
            {
            }

            private void Check()
            {
                this.Assert(false, "Error reached.");
            }
        }

        private class M6 : Monitor
        {
            [Start]
            [OnEventDoAction(typeof(MonitorUnitEvent), nameof(Check))]
            private class Init : BaseState
            {
            }

            [OnEventDoAction(typeof(MonitorUnitEvent), nameof(BaseCheck))]
            private class BaseState : State
            {
            }

#pragma warning disable CA1822 // Mark members as static
            private void Check()
#pragma warning restore CA1822 // Mark members as static
            {
            }

            private void BaseCheck()
            {
                this.Assert(false, "Error reached.");
            }
        }

        private class M7 : Monitor
        {
            [Start]
            [OnEventDoAction(typeof(MonitorUnitEvent), nameof(Check))]
            private class Init : BaseState
            {
            }

            [OnEventDoAction(typeof(MonitorUnitEvent), nameof(BaseCheck))]
            private class BaseState : BaseBaseState
            {
            }

            [OnEventDoAction(typeof(MonitorUnitEvent), nameof(BaseBaseCheck))]
            private class BaseBaseState : State
            {
            }

#pragma warning disable CA1822 // Mark members as static
            private void Check()
#pragma warning restore CA1822 // Mark members as static
            {
            }

            private void BaseCheck()
            {
                this.Assert(false, "Error reached.");
            }

            private void BaseBaseCheck()
            {
                this.Assert(false, "Error reached.");
            }
        }

        private class M8 : Monitor
        {
            [Start]
            private class Init : BaseState
            {
            }

            [OnEventDoAction(typeof(MonitorUnitEvent), nameof(BaseCheck))]
            private class BaseState : BaseBaseState
            {
            }

            [OnEventDoAction(typeof(MonitorUnitEvent), nameof(BaseBaseCheck))]
            private class BaseBaseState : State
            {
            }

#pragma warning disable CA1822 // Mark members as static
            private void BaseCheck()
#pragma warning restore CA1822 // Mark members as static
            {
            }

            private void BaseBaseCheck()
            {
                this.Assert(false, "Error reached.");
            }
        }

        private class M9 : Monitor
        {
            [Start]
            private class Init : BaseState
            {
            }

            [OnEventGotoState(typeof(MonitorUnitEvent), typeof(Done))]
            private class BaseState : State
            {
            }

            [OnEntry(nameof(DoneOnEntry))]
            private class Done : State
            {
            }

            private void DoneOnEntry()
            {
                this.Assert(false, "Done reached.");
            }
        }

        private class M10 : Monitor
        {
            [Start]
            [OnEventGotoState(typeof(MonitorUnitEvent), typeof(Done))]
            private class Init : BaseState
            {
            }

            [OnEventGotoState(typeof(MonitorUnitEvent), typeof(Error))]
            private class BaseState : State
            {
            }

            [OnEntry(nameof(DoneOnEntry))]
            private class Done : State
            {
            }

            [OnEntry(nameof(ErrorOnEntry))]
            private class Error : State
            {
            }

            private void DoneOnEntry()
            {
                this.Assert(false, "Done reached.");
            }

            private void ErrorOnEntry()
            {
                this.Assert(false, "Error reached.");
            }
        }

        private class M11 : Monitor
        {
            [Start]
            [OnEventGotoState(typeof(MonitorUnitEvent), typeof(Done))]
            private class Init : BaseState
            {
            }

            [OnEventGotoState(typeof(MonitorUnitEvent), typeof(Error))]
            private class BaseState : BaseBaseState
            {
            }

            [OnEventGotoState(typeof(MonitorUnitEvent), typeof(Error))]
            private class BaseBaseState : State
            {
            }

            [OnEntry(nameof(DoneOnEntry))]
            private class Done : State
            {
            }

            [OnEntry(nameof(ErrorOnEntry))]
            private class Error : State
            {
            }

            private void DoneOnEntry()
            {
                this.Assert(false, "Done reached.");
            }

            private void ErrorOnEntry()
            {
                this.Assert(false, "Error reached.");
            }
        }

        private class M12 : Monitor
        {
            [Start]
            private class Init : BaseState
            {
            }

            [OnEventGotoState(typeof(MonitorUnitEvent), typeof(Done))]
            private class BaseState : BaseBaseState
            {
            }

            [OnEventGotoState(typeof(MonitorUnitEvent), typeof(Error))]
            private class BaseBaseState : State
            {
            }

            [OnEntry(nameof(DoneOnEntry))]
            private class Done : State
            {
            }

            [OnEntry(nameof(ErrorOnEntry))]
            private class Error : State
            {
            }

            private void DoneOnEntry()
            {
                this.Assert(false, "Done reached.");
            }

            private void ErrorOnEntry()
            {
                this.Assert(false, "Error reached.");
            }
        }

        [Fact(Timeout = 5000)]
        public void TestMonitorStateInheritingAbstractState()
        {
            this.TestWithError(r =>
            {
                r.RegisterMonitor<M1>();
                r.Monitor<M1>(MonitorUnitEvent.Instance);
            },
            expectedError: "Error reached.");
        }

        [Fact(Timeout = 5000)]
        public void TestMonitorStateInheritingStateDuplicateStart()
        {
            this.TestWithError(r =>
            {
                r.RegisterMonitor<M2>();
            },
            expectedError: "M2 can not declare more than one start states.");
        }

        [Fact(Timeout = 5000)]
        public void TestMonitorStateInheritingStateOnEntry()
        {
            this.TestWithError(r =>
            {
                r.RegisterMonitor<M3>();
            },
            expectedError: "Error reached.");
        }

        [Fact(Timeout = 5000)]
        public void TestMonitorStateOverridingStateOnEntry()
        {
            this.Test(r =>
            {
                r.RegisterMonitor<M4>();
            });
        }

        [Fact(Timeout = 5000)]
        public void TestMonitorStateInheritingStateOnEventDoAction()
        {
            this.TestWithError(r =>
            {
                r.RegisterMonitor<M5>();
                r.Monitor<M5>(MonitorUnitEvent.Instance);
            },
            expectedError: "Error reached.");
        }

        [Fact(Timeout = 5000)]
        public void TestMonitorStateOverridingStateOnEventDoAction()
        {
            this.Test(r =>
            {
                r.RegisterMonitor<M6>();
                r.Monitor<M6>(MonitorUnitEvent.Instance);
            });
        }

        [Fact(Timeout = 5000)]
        public void TestMonitorStateOverridingTwoStatesOnEventDoAction()
        {
            this.Test(r =>
            {
                r.RegisterMonitor<M7>();
                r.Monitor<M7>(MonitorUnitEvent.Instance);
            });
        }

        [Fact(Timeout = 5000)]
        public void TestMonitorStateOverridingDeepStateOnEventDoAction()
        {
            this.Test(r =>
            {
                r.RegisterMonitor<M8>();
                r.Monitor<M8>(MonitorUnitEvent.Instance);
            });
        }

        [Fact(Timeout = 5000)]
        public void TestMonitorStateInheritingStateOnEventGotoState()
        {
            this.TestWithError(r =>
            {
                r.RegisterMonitor<M9>();
                r.Monitor<M9>(MonitorUnitEvent.Instance);
            },
            expectedError: "Done reached.");
        }

        [Fact(Timeout = 5000)]
        public void TestMonitorStateOverridingStateOnEventGotoState()
        {
            this.TestWithError(r =>
            {
                r.RegisterMonitor<M10>();
                r.Monitor<M10>(MonitorUnitEvent.Instance);
            },
            expectedError: "Done reached.");
        }

        [Fact(Timeout = 5000)]
        public void TestMonitorStateOverridingTwoStatesOnEventGotoState()
        {
            this.TestWithError(r =>
            {
                r.RegisterMonitor<M11>();
                r.Monitor<M11>(MonitorUnitEvent.Instance);
            },
            expectedError: "Done reached.");
        }

        [Fact(Timeout = 5000)]
        public void TestMonitorStateOverridingDeepStateOnEventGotoState()
        {
            this.TestWithError(r =>
            {
                r.RegisterMonitor<M12>();
                r.Monitor<M12>(MonitorUnitEvent.Instance);
            },
            expectedError: "Done reached.");
        }
    }
}
