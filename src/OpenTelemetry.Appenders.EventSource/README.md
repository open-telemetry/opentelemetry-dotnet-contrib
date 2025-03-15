# EventSource for OpenTelemetry .NET

| Status        |           |
| ------------- |-----------|
| Stability     | [Alpha](../../README.md#alpha) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Appenders.EventSource.svg)](https://www.nuget.org/packages/OpenTelemetry.Appenders.EventSource)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Appenders.EventSource.svg)](https://www.nuget.org/packages/OpenTelemetry.Appenders.EventSource)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Appenders.EventSource)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Appenders.EventSource)

> [!IMPORTANT]
> This package is in the [Alpha](../../README.md#alpha) state. The main goal
  of this package is to stabilize [OpenTelemetry Logs (Bridge) API](https://github.com/open-telemetry/opentelemetry-dotnet/issues/4433).
  There is no plan to go beyond Alpha until API reach stability.

This project contains an
[EventListener](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventlistener)
which can be used to translate events written to an
[EventSource](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventsource)
into OpenTelemetry logs.

## Installation

```shell
dotnet add package OpenTelemetry.Extensions.EventSource --prerelease
```

## Usage Example

### Configured using dependency injection

```csharp
IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(builder =>
    {
        builder.ClearProviders();

        // Step 1: Configure OpenTelemetry logging...
        builder.AddOpenTelemetry(options =>
        {
            options
                .ConfigureResource(builder => builder.AddService("MyService"))
                .AddConsoleExporter()
                // Step 2: Register OpenTelemetryEventSourceLogEmitter to listen to events...
                .AddEventSourceLogEmitter((name) => name == MyEventSource.Name ? EventLevel.Informational : null);
        });
    })
    .Build();

    host.Run();
```

### Configured manually

```csharp
// Step 1: Configure OpenTelemetryLoggerProvider...
var openTelemetryLoggerProvider = Sdk.CreateLoggerProviderBuilder()
    .ConfigureResource(builder => builder.AddService("MyService"))
    .AddConsoleExporter()
    .Build();

// Step 2: Create OpenTelemetryEventSourceLogEmitter to listen to events...
using var openTelemetryEventSourceLogEmitter = new OpenTelemetryEventSourceLogEmitter(
    openTelemetryLoggerProvider,
    (name) => name == MyEventSource.Name ? EventLevel.Informational : null,
    disposeProvider: true);
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
