# TestingEngine class

Testing engine that can run a controlled concurrency test using a specified configuration.

```csharp
public sealed class TestingEngine
```

## Public Members

| name | description |
| --- | --- |
| static [Create](TestingEngine/Create.md)(…) | Creates a new systematic testing engine. (7 methods) |
| [Logger](TestingEngine/Logger.md) { get; set; } | Get or set the ILogger used to log messages during testing. |
| [ReadableTrace](TestingEngine/ReadableTrace.md) { get; } | The readable trace, if any. |
| [ReproducibleTrace](TestingEngine/ReproducibleTrace.md) { get; } | The reproducable trace, if any. |
| [TestReport](TestingEngine/TestReport.md) { get; set; } | Data structure containing information gathered during testing. |
| [GetReport](TestingEngine/GetReport.md)() | Returns a report with the testing results. |
| [IsTestRewritten](TestingEngine/IsTestRewritten.md)() | Checks if the test executed by the testing engine has been rewritten with the current version. |
| [RegisterPerIterationCallBack](TestingEngine/RegisterPerIterationCallBack.md)(…) | Registers a callback to invoke at the end of each iteration. The callback takes as a parameter an integer representing the current iteration. |
| [Run](TestingEngine/Run.md)() | Runs the testing engine. |
| [Stop](TestingEngine/Stop.md)() | Stops the testing engine. |
| [ThrowIfBugFound](TestingEngine/ThrowIfBugFound.md)() | Throws either an AssertionFailureException, if a bug was found, or an unhandled Exception, if one was thrown. |
| [TryEmitCoverageReports](TestingEngine/TryEmitCoverageReports.md)(…) | Tries to emit the available coverage reports to the specified directory with the given file name, and returns the paths of all emitted coverage reports. |
| [TryEmitReports](TestingEngine/TryEmitReports.md)(…) | Tries to emit the available reports to the specified directory with the given file name, and returns the paths of all emitted reports. |

## See Also

* namespace [Microsoft.Coyote.SystematicTesting](../Microsoft.Coyote.SystematicTestingNamespace.md)
* assembly [Microsoft.Coyote.Test](../Microsoft.Coyote.Test.md)

<!-- DO NOT EDIT: generated by xmldocmd for Microsoft.Coyote.Test.dll -->