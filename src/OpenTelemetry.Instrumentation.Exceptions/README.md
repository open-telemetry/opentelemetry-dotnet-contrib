# Exceptions Instrumentation for OpenTelemetry .NET

| Status | |
| ------ | --- |
| Stability | [Development](../../README.md#development) |
| Code Owners | [@iblancasa](https://github.com/iblancasa) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.Exceptions)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Exceptions)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.Exceptions)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Exceptions)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Instrumentation.Exceptions)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Instrumentation.Exceptions)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library)
intended to capture selected runtime-level unhandled exceptions and emit them as
OpenTelemetry logs.

This package is currently scaffold-only. Runtime exception capture APIs and
behavior will be added in follow-up pull requests.

## Steps to enable OpenTelemetry.Instrumentation.Exceptions

### Step 1: Install package

Add a reference to
[`OpenTelemetry.Instrumentation.Exceptions`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Exceptions)
package.

```shell
dotnet add package --prerelease OpenTelemetry.Instrumentation.Exceptions
```

## What is captured

Nothing yet. Runtime hooks and emitted log details are intentionally deferred.

## Semantic-convention behavior

Follow-up implementation work should align emitted logs with the OpenTelemetry
exception log semantic conventions.

## Supported runtimes and limitations

Supported target frameworks for this package are:

* `net10.0`
* `net8.0`
* `netstandard2.0`
* `net462`

Limitations:

* Delivery is best effort. For terminating failures, the runtime may stop the
  process before exporters flush all pending log records.
* `AppDomain.UnhandledException` is runtime-level and not hosting-model
  complete. Some frameworks expose earlier or alternate unhandled-exception
  hooks and may prevent this event from firing for certain exceptions.
* `TaskScheduler.UnobservedTaskException` only fires for faulted tasks whose
  exceptions were never observed. It is not raised for ordinary handled task
  failures.

## References

* [OpenTelemetry exception log semantic conventions](https://opentelemetry.io/docs/specs/semconv/exceptions/exceptions-logs/)
* [System.AppDomain.UnhandledException event](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-appdomain-unhandledexception)
* [TaskScheduler.UnobservedTaskException event](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler.unobservedtaskexception?view=net-10.0)
