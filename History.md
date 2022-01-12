## v1.4.2
- Added support for the `net6.0` target.
- The `TestingEngine` is now giving a warning if the DLL being tested has not been rewritten.
- The number of controlled operations are now reported as part of test statistics.
- Improvements, optimizations and bug-fixes in binary rewriting.
- Support for dumping the rewritten IL diff to a file through `--dump-il-diff`.

## v1.4.1
- Enabled automated fallback to concurrency fuzzing upon detecting uncontrolled concurrency during
  testing to increase usability. This feature is enabled by default and can be disabled via the
  `disable-fuzzing-fallback` command line option (or
  `Configuration.WithConcurrencyFuzzingFallbackEnabled`).
- Added a new JSON test report that lists any detected invocations of uncontrolled methods.
- The `TestingEngine.TryEmitTraces` method has been renamed to `TestingEngine.TryEmitReports` to
  reflect that the reports do not include only traces.
- The `IActorRuntimeLog.OnStrategyDescription` method has been removed.

## v1.4.0
- Redesigned the systematic testing runtime to significantly improve its performance and simplicity.
- An `ActorId` of a halted actor can now be reused.
- The `coyote` tool can now resolve `aspnet`.

## v1.3.1
- Added rewriting support for testing race conditions with several `System.Collections.Concurrent`
  data structures.
- Added rewriting support for testing `System.Collections.Generic.HashSet<T>` data races.
- Added the `SchedulingPoint.Suppress` and `SchedulingPoint.Resume` methods for suppressing and
  resuming interleavings of enabled operations, accordingly.
- Fixed a memory leak in the testing engine.

## v1.3.0
- Improved the binary rewriting engine and fixed various rewriting bugs.
- Removed the deprecated `Microsoft.Coyote.Tasks` namespace. Testing task-based code should now only
  be done via binary rewriting, instead of using a custom task type.
- Removed the `net48` target, can instead just use the `net462` target for legacy .NET Framework
  projects.

## v1.2.8
- Improved the strategies used for concurrency fuzzing.
- Fixed a rewriting bug related to the `TaskAwaiter` type.

## v1.2.7
- Added the `--no-repro` command line option (enabled also via `Configuration.WithNoBugTraceRepro`),
  which disables the ability to reproduce buggy traces to allow skipping errors due to uncontrolled
  concurrency, for example when the program is only partially rewritten, or there is external
  concurrency that is not mocked, or when the program uses an API that is not yet supported.
- The uncontrolled concurrency errors have been updated to be more informative and point to the
  documentation for further reading.

## v1.2.6
- Added an experimental rewriting pass that adds assertion checks to find data races in uses of the
  `System.Collections.Generic.List<T>` and `System.Collections.Generic.Dictionary<TKey, TValue>`
  collections.
- Added support for the `net462` target.

## v1.2.5
- Added the `SchedulingPoint` static class that exposes methods for adding manual scheduling points
  during systematic testing.
- Added an experimental systematic testing strategy that uses reinforcement learning. This is
  enabled using the `--sch-rl` command line option or the `Configuration.WithRLStrategy` method.
- Added an experimental concurrency fuzzing testing mode that uses delay injection instead of
  systematic testing to find bugs. This can be enabled using the `--concurrency-fuzzing` command
  line option or the `Configuration.WithConcurrencyFuzzingEnabled` method.
- Added the `IActorRuntimeLog.OnEventHandlerTerminated` actor log callback that is called when an
  event handler terminates.
- Fixed a bug where the `IActorRuntimeLog.OnHandleRaisedEvent` actor log callback was not invoked in
  production.

## v1.2.4
- Improved how `coyote test` resolves ambiguous test method names.
- Fixed a bug where awaiting a task from a previous test iteration that was canceled due to
  `ExecutionCanceledException` would hang the tester.

## v1.2.3
- Exposed the `TextWriterLogger` type.
- Fixed a configuration bug where the `fairpct` strategy would be picked instead of `probabilistic`.

## v1.2.2
- Added the `Specification.IsEventuallyCompletedSuccessfully` API for checking if a task eventually
  completes successfully.
- Added the `Configuration.WithTestingTimeout` API for specifying a systematic testing timeout
  instead of iterations.
- Optimized state space exploration in programs using `Task.Delay`.
- Added support for the `net5.0` target.
- Removed the `net47` target.

## v1.2.1
- Added the `OnEventIgnored` and `OnEventDeferred` callbacks in the `Actor` type.

## v1.2.0
- Added support for systematically testing actors and tasks together using rewriting.
- Hardened the systematic testing runtime.

## v1.1.5
- Improved detection of uncontrolled tasks during systematic testing.
- Added detection of invoking unsupported APIs during systematic testing.

## v1.1.4
- Added missing `coyote rewrite` dependencies in the `Microsoft.Coyote.Test` package.

## v1.1.3
- Optimizations and fixes in binary rewriting.

## v1.1.2
- Added basic support for the `System.Threading.Tasks.Parallel` type during rewriting.
- Fixed a bug in `coyote rewrite` that was incorrectly copying dependencies after rewriting.

## v1.1.1
- Renamed `TestingEngine.ReproducibleTrace` to fix typo in the API name.
- Fixed some bugs in `coyote rewrite`.

## v1.1.0
- Added experimental support for testing unmodified task-based programs using binary rewriting.
- Added support for log severity in the logger and converted to an `ILogger` interface.
- Optimized various internals of the task testing runtime.

## v1.0.17
- Fixed a bug in the `Actor` logic related to event handlers.
- Fixed a bug in `Microsoft.Coyote.Task.WhenAny`.

## v1.0.16
- Added support for cancellations in `Task.Run` APIs.
- Optimized various internals of the task testing runtime.

## v1.0.15
- Fixed the `Task.WhenAny` and `Task.WhenAll` APIs so that they execute asynchronously during
  systematic testing.
- Fixed the `Task.WhenAny` and `Task.WhenAll` APIs so that they throw the proper argument exceptions
  during systematic testing.

## v1.0.14
- Added missing `Task<TResult>.UncontrolledTask` API.
- Fixed a bug in the testing runtime for controlled tasks.

## v1.0.13
- Fixed a bug in the testing runtime for controlled tasks that could lead to a stack overflow.
- Optimized various internals of the testing runtime.

## v1.0.12
- Introduced a new `EventGroup` API for actors, which replaces operation groups, that allows
  improved tracing and awaiting of long running actor operations.
- The `Task.Yield` API can now be used to de-prioritize the executing operation during testing.
- Added missing APIs in the `Microsoft.Coyote.Tasks.Semaphore` type.
- Fixed two bugs in the systematic testing scheduler.

## v1.0.11
- Fixed an issue that did not allow systematic and non-systematic unit tests to run on the same
  process.
- Fixed a bug in the `TestingEngine` logger.

## v1.0.10
- Fixed the NuGet symbol packages.

## v1.0.9
- Introduced a new `Microsoft.Coyote.Test` package that contains the `Test` attribute and the
  `TestingEngine` type for writing unit tests.
- The core `Microsoft.Coyote` does not contain anymore `Test` and `TestingEngine`, which were moved
  to the `Microsoft.Coyote.Test` package.
- Added support for optional anonymized telemetry in the `TestingEngine`.
- Optimized various internals of the systematic testing scheduler.
- Fixed some issues in the scripts.

## v1.0.8
- The core `Microsoft.Coyote` project is now targeting only .NET Standard, allowing it to be
  consumed by any project that supports `netstandard2.0` and above.
- Removed the `net46` target.
- Fixed bug in using the global dotnet tool.

## v1.0.7
- Added support for building Coyote on Linux and macOS.
- Building Coyote locally now ignores .NET targets that are not installed.
- Added optional anonymized telemetry in the `coyote` tool.
- Fixed a bug in the `SynchronizedBlock` type.

## v1.0.6
- Added a `SynchronizedBlock` type to model the semantics of the C# `lock` statement.
- Simplified the `Configuration` APIs for setting max-steps and liveness related heuristics.
- Fixed code coverage and added support for code coverage on `netcoreapp3.1`.

## v1.0.5
- Added a --version argument to the `coyote` command line tool.
- Added a dotnet tool package called `Microsoft.Coyote.CLI` to install the `coyote` command line
  tool and running it without an explicit path.
- Exposed the `ReadableTrace` and `ReproducibleTrace` members of
  `Microsoft.Coyote.SystematicTesting.TestingEngine` as public.
- Fixed a bug in activity coverage reporting for `netcoreapp3.1`.
- Fixed some bugs in parallel testing.

## v1.0.4
- Added new `Microsoft.Coyote.Configuration.WithReplayStrategy` method for programmatically
  assigning a trace to replay.
- Added support for the `netstandard2.1`, `netcoreapp3.1` and `net48` targets.
- Removed support for the `netcoreapp2.2` target, which reached end of life.
- Fixed various bugs in the documentation.

## v1.0.3
- Fixed an issue when invoking `Microsoft.Coyote.Tasks.Task.ExploreContextSwitch` during a
  production run.

## v1.0.2
- Made ActorRuntimeLogGraphBuilder public.
- Added CreateStateMachine to IActorRuntimeLog.

## v1.0.1
- Fixed an issue in the runtime (there should always be a default task runtime instance).

## v1.0.0
- The initial release of the Coyote set of libraries and test tools.