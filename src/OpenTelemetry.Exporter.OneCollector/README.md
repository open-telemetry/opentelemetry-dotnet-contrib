# OneCollector Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.OneCollector.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OneCollector)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.OneCollector.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OneCollector)

The OneCollector Exporter exports telemetry to the Microsoft OneCollector
backend.

> **Warning**
> This is an early preview version breaking changes should be expected.

## Installation

```shell
dotnet add package --prerelease OpenTelemetry.Exporter.OneCollector
```

## Basic usage

```csharp
using var logFactory = LoggerFactory.Create(builder => builder
    .AddOpenTelemetry(builder =>
    {
        builder.ParseStateValues = true;
        builder.IncludeScopes = true;
        builder.AddOneCollectorExporter("instrumentation-key-here");
    }));

var logger = logFactory.CreateLogger<MyService>();

using var scope = logger.BeginScope("{requestContext}", Guid.NewGuid());

logger.LogInformation("Request received {requestId}!", 1);
logger.LogWarning("Warning encountered {error_code}!", 0xBAADBEEF);
```
