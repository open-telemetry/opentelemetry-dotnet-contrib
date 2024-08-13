# Hangfire Instrumentation for OpenTelemetry .NET

| Status        |           |
| ------------- |-----------|
| Stability     |  [Beta](../../README.md#beta)|
| Code Owners   |  [@fred2u](https://github.com/fred2u)|

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.Hangfire)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Hangfire)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.Hangfire)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Hangfire)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Instrumentation.Hangfire)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Instrumentation.Hangfire)

This is an
[Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments
[Hangfire](https://www.nuget.org/packages/Hangfire/)
and collects telemetry about BackgroundJob.

## Steps to enable OpenTelemetry.Instrumentation.Hangfire

### Step 1: Install and configure Hangfire

[Getting Started](https://docs.hangfire.io/en/latest/getting-started/index.html)

### Step 2: Install Hangfire instrumentation Package

Add a reference to the
[`OpenTelemetry.Instrumentation.Hangfire`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Hangfire)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Instrumentation.Hangfire --prerelease
```

### Step 3: Enable Hangfire Instrumentation at application startup

Hangfire instrumentation must be enabled at application startup.

The following example demonstrates adding Hangfire instrumentation to a
console application. This example also sets up the OpenTelemetry Console
exporter, which requires adding the package
[`OpenTelemetry.Exporter.Console`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Console/README.md)
to the application.

```csharp
using OpenTelemetry.Trace;

public class Program
{
    public static void Main(string[] args)
    {
        using var tracerProvider = Sdk
            .CreateTracerProviderBuilder()
            .AddHangfireInstrumentation()
            .AddConsoleExporter()
            .Build();
    }
}
```

For an ASP.NET Core application, adding instrumentation is typically done in
the `ConfigureServices` of your `Startup` class. Refer to [example](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/examples/AspNetCore/Program.cs).

For an ASP.NET application, adding instrumentation is typically done in the
`Global.asax.cs`. Refer to [example](../../examples/AspNet/Global.asax.cs).

## Advanced configuration

This instrumentation can be configured to change the default behavior by using
`HangfireInstrumentationOptions`.

```csharp
using var tracerProvider = Sdk
    .CreateTracerProviderBuilder()
    .AddHangfireInstrumentation(options =>
    {
        options.DisplayNameFunc = job => $"JOB {job.Id}";
        options.Filter = job => job.Id == "Filter this job";
        options.RecordException = true;
    })
    .AddConsoleExporter()
    .Build();
```

When used with
[`OpenTelemetry.Extensions.Hosting`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Extensions.Hosting/README.md),
all configurations to `HangfireInstrumentationOptions`
can be done in the `ConfigureServices` method of your applications `Startup`
class as shown below.

```csharp
// Configure
services.Configure<HangfireInstrumentationOptions>(options =>
{
    options.DisplayNameFunc = job => $"JOB {job.Id}";
    options.Filter = job => job.Id == "Filter this job";
    options.RecordException = true;
});

services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddHangfireInstrumentation()
        .AddConsoleExporter());
```

### RecordException

Configures a value indicating whether the exception will be recorded as
ActivityEvent or not. See
[Semantic Conventions for Exceptions on Spans](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/exceptions/exceptions-spans.md)

```csharp
using var tracerProvider = Sdk
    .CreateTracerProviderBuilder()
    .AddHangfireInstrumentation(options =>
    {
        options.RecordException = true;
    })
    .AddConsoleExporter()
    .Build();
```

### DisplayNameFunc

This option allows changing activity display name.

```C#
using var tracerProvider = Sdk
    .CreateTracerProviderBuilder()
    .AddHangfireInstrumentation(options =>
    {
        options.DisplayNameFunc = job => $"JOB {job.Id}";
    })
    .AddConsoleExporter()
    .Build();
```

If not configured the default is

```C#
$"JOB {BackgroundJob.Job.Type.Name}.{BackgroundJob.Job.Method.Name}"
```

### Filter

This option can be used to filter out activities based on the `BackgroundJob`
being executed. The `Filter` function should return `true` if the telemetry is
to be collected, and `false` if it should not.

The following code snippet shows how to use `Filter` to filter out traces for
job with a specified job id.

```csharp
using var tracerProvider = Sdk
    .CreateTracerProviderBuilder()
    .AddHangfireInstrumentation(options =>
    {
        options.Filter = job => job.Id == "Filter this job";
    })
    .AddConsoleExporter()
    .Build();
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Hangfire Project](https://www.hangfire.io/)
