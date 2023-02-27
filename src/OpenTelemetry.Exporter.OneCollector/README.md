# OneCollector Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.OneCollector.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OneCollector)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.OneCollector.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OneCollector)

> **Warning**
> This is an early preview version breaking changes should be expected.

The OneCollectorExporter is designed for Microsoft products to send data to
public-facing end-points which route to Microsoft's internal data pipeline. It
is not meant to be used outside of Microsoft products and is open sourced to
demonstrate best practices and to be transparent about what is being collected.

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
        builder.AddOneCollectorExporter("InstrumentationKey=instrumentation-key-here");
    }));

var logger = logFactory.CreateLogger<MyService>();

using var scope = logger.BeginScope("{requestContext}", Guid.NewGuid());

logger.LogInformation("Request received {requestId}!", 1);
logger.LogWarning("Warning encountered {error_code}!", 0xBAADBEEF);
```
