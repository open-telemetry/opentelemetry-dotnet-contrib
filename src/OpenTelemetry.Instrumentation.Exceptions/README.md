# Exceptions Instrumentation for OpenTelemetry .NET

| Status | |
| ------ | --- |
| Stability | [Development](../../README.md#development) |
| Code Owners | [@iblancasa](https://github.com/iblancasa) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.Exceptions)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Exceptions)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.Exceptions)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Exceptions)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Instrumentation.Exceptions)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Instrumentation.Exceptions)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which captures selected runtime-level unhandled exceptions and emits them as
OpenTelemetry logs.

Captured exceptions are emitted using the OpenTelemetry exception log semantic
conventions. When the .NET logging pipeline supports it, the exception instance
is attached directly to the emitted <code>LogRecord</code> instead of manually
duplicating exception fields.

## Steps to enable OpenTelemetry.Instrumentation.Exceptions

### Step 1: Install package

Add a reference to
[`OpenTelemetry.Instrumentation.Exceptions`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Exceptions)
package.

```shell
dotnet add package --prerelease OpenTelemetry.Instrumentation.Exceptions
```

### Step 2: Enable exception instrumentation

Enable exception instrumentation when configuring OpenTelemetry logging:

```csharp
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(logging =>
    {
        logging.AddExceptionsInstrumentation();
        logging.AddConsoleExporter();
    });
});
```

You can also use the builder returned by `services.AddOpenTelemetry()`:

```csharp
services.AddOpenTelemetry().WithLogging(logging =>
{
    logging.AddExceptionsInstrumentation();
    logging.AddOtlpExporter();
});
```

### Optional configuration

```csharp
logging.AddExceptionsInstrumentation(options =>
{
    options.CaptureUnhandledExceptions = true;
    options.CaptureUnobservedTaskExceptions = true;
});
```

## What is captured

This package currently subscribes to:

* `AppDomain.CurrentDomain.UnhandledException`
* `TaskScheduler.UnobservedTaskException`

Each emitted log uses event name `exception`.

Severity follows the exception log semantic convention guidance:

* `AppDomain.UnhandledException` uses `Critical` when
  `UnhandledExceptionEventArgs.IsTerminating` is `true`, otherwise `Error`.
* `TaskScheduler.UnobservedTaskException` uses `Error`.

## Semantic-convention behavior

For exceptions delivered as a real <code>Exception</code> instance, this package
passes that instance through the .NET logging API so the resulting
<code>LogRecord.Exception</code> is populated.

This aligns with the OpenTelemetry exception log semantic convention guidance
for language SDKs that can attach exception objects directly. Exporters or SDK
layers may derive `exception.type`, `exception.message`, and
`exception.stacktrace` from that exception object.

The exception event body is a short fixed message:

* `Unhandled exception.`
* `Unobserved task exception.`

Be aware that `exception.message` can contain sensitive data because it is taken
from the original exception object.

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
* This package does not suppress or mark exceptions as observed. Application
  behavior is unchanged.
* If the runtime supplies a non-<code>Exception</code> object for
  `AppDomain.UnhandledException`, only limited exception fields can be emitted.

## References

* [OpenTelemetry exception log semantic conventions](https://opentelemetry.io/docs/specs/semconv/exceptions/exceptions-logs/)
* [System.AppDomain.UnhandledException event](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-appdomain-unhandledexception)
* [TaskScheduler.UnobservedTaskException event](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler.unobservedtaskexception?view=net-10.0)
