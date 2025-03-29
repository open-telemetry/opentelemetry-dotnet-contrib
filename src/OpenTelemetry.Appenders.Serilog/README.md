# Serilog for OpenTelemetry .NET

| Status        |           |
| ------------- |-----------|
| Stability     | [Alpha](../../README.md#alpha) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Appenders.Serilog.svg)](https://www.nuget.org/packages/OpenTelemetry.Appenders.Serilog)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Appenders.Serilog.svg)](https://www.nuget.org/packages/OpenTelemetry.Appenders.Serilog)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Appenders.Serilog)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Appenders.Serilog)

> [!IMPORTANT]
> This package is in the [Alpha](../../README.md#alpha) state.

This project contains a [Serilog](https://github.com/serilog/)
[sink](https://github.com/serilog/serilog/wiki/Configuration-Basics#sinks) for
writing log messages to OpenTelemetry and an
[enricher](https://github.com/serilog/serilog/wiki/Configuration-Basics#enrichers)
for adding OpenTelemetry trace details to log messages.

## Installation

```shell
dotnet add package OpenTelemetry.Appenders.Serilog  --prerelease
```

## Usage Examples

### Sink

Use the sink when you want to capture & translate Serilog log messages into
OpenTelemetry. This is known as a "log appender" in the OpenTelemetry
Specification. Log messages will flow through the OpenTelemetry pipeline to any
registered processors/exporters.

```csharp
// Step 1: Configure an OpenTelemetryLoggerProvider...
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

var serviceProvider = new ServiceCollection()
    .AddLogging(builder => builder
        .AddConsole()
        .AddOpenTelemetry(options => 
        {
            options.AddConsoleExporter();
            options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyService"));
        }))
    .BuildServiceProvider();

var loggerProvider = serviceProvider.GetRequiredService<OpenTelemetryLoggerProvider>();

// Step 2: Register OpenTelemetry sink with Serilog...
Log.Logger = new LoggerConfiguration()
    .WriteTo.OpenTelemetry(loggerProvider, disposeProvider: true) // <-- Register sink
    .CreateLogger();

// Step 3: When application is shutdown flush all log messages and dispose provider...
Log.CloseAndFlush();
```

### Enricher

Use the enricher when you want to add trace details to Serilog log messages.

Note: It is not necessary to use the enricher when using the sink above. The
enricher is provider for log messages that are NOT flowing through OpenTelemetry
for example when logging to JSON files. When using the sink trace details are
included automatically.

```csharp
// Step 1: Register OpenTelemetry enricher with Serilog...
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .Enrich.WithOpenTelemetry() // <-- Register enricher
    .CreateLogger();

// Step 2: Start a trace...
using var myActivity = myActivitySource.StartActivity(
    activityKind,
    startTime: DateTimeOffset.NowUtc,
    name: name);

// Step 3: Generate log messages through Serilog...
Log.Logger.Information("Starting application");
```

The example above will output this JSON:

```json
{
  "@t": "2024-03-23T13:33:57.5057358Z",
  "@m": "Starting application",
  "@i": "ab9cc38b",
  "TraceId": "a6d6d0bf7c1f87496cbe1e7f10880477",
  "SpanId": "b49eb1336be3152e",
  "ParentSpanId": "0000000000000000"
}
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
