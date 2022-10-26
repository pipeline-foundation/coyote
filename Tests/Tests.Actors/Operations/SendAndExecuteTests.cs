﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Coyote.Specifications;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Coyote.Actors.Tests
{
    public class SendAndExecuteTests : BaseActorTest
    {
        public SendAndExecuteTests(ITestOutputHelper output)
            : base(output)
        {
        }

        private class Config1 : Event
        {
            public TaskCompletionSource<bool> Tcs;

            public Config1(TaskCompletionSource<bool> tcs)
            {
                this.Tcs = tcs;
            }
        }

        private class Config2 : Event
        {
            public bool HandleException;
            public TaskCompletionSource<bool> Tcs;

            public Config2(bool handleEx, TaskCompletionSource<bool> tcs)
            {
                this.HandleException = handleEx;
                this.Tcs = tcs;
            }
        }

        private class E1 : Event
        {
            public int Value;

            public E1()
            {
                this.Value = 0;
            }
        }

        private class E2 : Event
        {
            public ActorId Id;

            public E2(ActorId id)
            {
                this.Id = id;
            }
        }

        private class E3 : Event
        {
        }

        private class M1 : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            private class Init : State
            {
            }

            private async Task InitOnEntry(Event e)
            {
                var tcs = (e as Config1).Tcs;
                var e1 = new E1();
                var m = await this.Context.CreateActorAndExecuteAsync(typeof(N1));
                await this.Context.SendEventAndExecuteAsync(m, e1);
                this.Assert(e1.Value is 1);
                tcs.SetResult(true);
            }
        }

        private class N1 : StateMachine
        {
            private bool LEHandled = false;

            [Start]
            [OnEntry(nameof(InitOnEntry))]
            [OnEventDoAction(typeof(E1), nameof(HandleEventE))]
            [OnEventDoAction(typeof(E3), nameof(HandleEventLE))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                this.SendEvent(this.Id, new E3());
            }

            private void HandleEventLE()
            {
                this.LEHandled = true;
            }

            private void HandleEventE(Event e)
            {
                this.Assert(this.LEHandled);
                (e as E1).Value = 1;
            }
        }

        [Fact(Timeout = 5000)]
        public async Task TestSyncSendBlocks()
        {
            await this.RunAsync(async r =>
            {
                var failed = false;
                var tcs = new TaskCompletionSource<bool>();
                r.OnFailure += (ex) =>
                {
                    failed = true;
                    tcs.SetResult(true);
                };

                r.CreateActor(typeof(M1), new Config1(tcs));

                await this.WaitAsync(tcs.Task);
                Assert.False(failed);
            },
            handleFailures: false);
        }

        private class M2 : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            [IgnoreEvents(typeof(E3))]
            private class Init : State
            {
            }

            private async Task InitOnEntry(Event e)
            {
                var tcs = (e as Config1).Tcs;
                var m = await this.Context.CreateActorAndExecuteAsync(typeof(N2), new E2(this.Id));
                var handled = await this.Context.SendEventAndExecuteAsync(m, new E3());
                this.Assert(handled);
                tcs.SetResult(true);
            }
        }

        private class N2 : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            [IgnoreEvents(typeof(E3))]
            private class Init : State
            {
            }

            private async Task InitOnEntry(Event e)
            {
                var creator = (e as E2).Id;
#pragma warning disable CS0618 // Type or member is obsolete
                var handled = await this.Id.Runtime.SendEventAndExecuteAsync(creator, new E3());
#pragma warning restore CS0618 // Type or member is obsolete
                this.Assert(!handled);
            }
        }

        [Fact(Timeout = 5000)]
        public async Task TestSendCycleDoesNotDeadlock()
        {
            await this.RunAsync(async r =>
            {
                var failed = false;
                var tcs = new TaskCompletionSource<bool>();
                r.OnFailure += (ex) =>
                {
                    failed = true;
                    tcs.SetResult(false);
                };

                r.CreateActor(typeof(M2), new Config1(tcs));

                await this.WaitAsync(tcs.Task);
                Assert.False(failed);
            },
            handleFailures: false);
        }

        private class M3 : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            private class Init : State
            {
            }

            private async Task InitOnEntry(Event e)
            {
                var tcs = (e as Config1).Tcs;
                var m = await this.Context.CreateActorAndExecuteAsync(typeof(N3));
                var handled = await this.Context.SendEventAndExecuteAsync(m, new E3());
                this.Monitor<SafetyMonitor>(new SafetyMonitor.SEReturns());
                this.Assert(handled);
                tcs.TrySetResult(true);
            }
        }

        private class N3 : StateMachine
        {
            [Start]
            [OnEventDoAction(typeof(E3), nameof(HandleE))]
            private class Init : State
            {
            }

            private void HandleE() => this.RaiseHaltEvent();

            protected override Task OnHaltAsync(Event e)
            {
                this.Monitor<SafetyMonitor>(new SafetyMonitor.MHalts());
                return Task.CompletedTask;
            }
        }

        private class SafetyMonitor : Monitor
        {
            internal class SEReturns : Event
            {
            }

            internal class MHalts : Event
            {
            }

            private bool MHalted = false;
            private bool SEReturned = false;

            [Start]
            [Hot]
            [OnEventDoAction(typeof(MHalts), nameof(OnMHalts))]
            [OnEventDoAction(typeof(SEReturns), nameof(OnSEReturns))]
            private class Init : State
            {
            }

            [Cold]
            private class Done : State
            {
            }

            private void OnMHalts()
            {
                this.Assert(this.SEReturned is false);
                this.MHalted = true;
            }

            private void OnSEReturns()
            {
                this.Assert(this.MHalted);
                this.SEReturned = true;
                this.RaiseGotoStateEvent<Done>();
            }
        }

        [Fact(Timeout = 5000)]
        public async Task TestMachineHaltsOnSendExec()
        {
            var config = this.GetConfiguration();
            await this.RunAsync(async r =>
            {
                var failed = false;
                var tcs = new TaskCompletionSource<bool>();
                r.OnFailure += (ex) =>
                {
                    failed = true;
                    tcs.SetResult(false);
                };

                r.RegisterMonitor<SafetyMonitor>();
                r.CreateActor(typeof(M3), new Config1(tcs));

                await this.WaitAsync(tcs.Task);
                Assert.False(failed);
            }, config, handleFailures: false);
        }

        private class M4 : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            private class Init : State
            {
            }

            private async Task InitOnEntry(Event e)
            {
                var tcs = (e as Config2).Tcs;
                var m = await this.Context.CreateActorAndExecuteAsync(typeof(N4), e);
                var handled = await this.Context.SendEventAndExecuteAsync(m, new E3());
                this.Assert(handled);
                tcs.TrySetResult(true);
            }

            protected override OnExceptionOutcome OnException(Exception ex, string methodName, Event e)
            {
                this.Assert(false);
                return OnExceptionOutcome.ThrowException;
            }
        }

        private class N4 : StateMachine
        {
            private bool HandleException = false;

            [Start]
            [OnEntry(nameof(InitOnEntry))]
            [OnEventDoAction(typeof(E3), nameof(HandleE))]
            private class Init : State
            {
            }

            private void InitOnEntry(Event e)
            {
                this.HandleException = (e as Config2).HandleException;
            }

#pragma warning disable CA1822 // Mark members as static
            private void HandleE() => throw new Exception();
#pragma warning restore CA1822 // Mark members as static

            protected override OnExceptionOutcome OnException(Exception ex, string methodName, Event e)
            {
                if (this.HandleException)
                {
                    return OnExceptionOutcome.HandledException;
                }

                return OnExceptionOutcome.ThrowException;
            }
        }

        [Fact(Timeout = 5000)]
        public async Task TestHandledExceptionOnSendExec()
        {
            await this.RunAsync(async r =>
            {
                var failed = false;
                var tcs = new TaskCompletionSource<bool>();
                r.OnFailure += (ex) =>
                {
                    failed = true;
                    tcs.SetResult(false);
                };

                r.CreateActor(typeof(M4), new Config2(true, tcs));

                await this.WaitAsync(tcs.Task);
                Assert.False(failed);
            },
            handleFailures: false);
        }

        [Fact(Timeout = 5000)]
        public async Task TestUnHandledExceptionOnSendExec()
        {
            await this.RunAsync(async r =>
            {
                var failed = false;
                var tcs = new TaskCompletionSource<bool>();
                var message = string.Empty;

                r.OnFailure += (ex) =>
                {
                    if (!failed)
                    {
                        message = (ex is ActionExceptionFilterException) ? ex.InnerException.Message : ex.Message;
                        failed = true;
                        tcs.TrySetResult(false);
                    }
                };

                r.CreateActor(typeof(M4), new Config2(false, tcs));

                await this.WaitAsync(tcs.Task);
                Assert.True(failed);
                Assert.StartsWith("Exception of type 'System.Exception' was thrown", message);
            },
            handleFailures: false);
        }

        private class M5 : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            private class Init : State
            {
            }

            private async Task InitOnEntry(Event e)
            {
                var tcs = (e as Config1).Tcs;
                var m = await this.Context.CreateActorAndExecuteAsync(typeof(N5));
                var handled = await this.Context.SendEventAndExecuteAsync(m, new E3());
                this.Assert(handled);
                tcs.TrySetResult(true);
            }
        }

        private class N5 : StateMachine
        {
            [Start]
            private class Init : State
            {
            }
        }

        [Fact(Timeout = 5000)]
        public async Task TestUnhandledEventOnSendExec()
        {
            await this.RunAsync(async r =>
            {
                var failed = false;
                var tcs = new TaskCompletionSource<bool>();
                var message = string.Empty;

                r.OnFailure += (ex) =>
                {
                    if (!failed)
                    {
                        message = (ex is ActionExceptionFilterException) ? ex.InnerException.Message : ex.Message;
                        failed = true;
                        tcs.TrySetResult(false);
                    }
                };

                r.CreateActor(typeof(M5), new Config1(tcs));

                await this.WaitAsync(tcs.Task);
                Assert.True(failed);

                var className = this.GetType().FullName;
                var expected = string.Format("{0}+N5(1) received event '{0}+E3' that cannot be handled.", className);
                Assert.Equal(expected, message);
            },
            handleFailures: false);
        }
    }
}
