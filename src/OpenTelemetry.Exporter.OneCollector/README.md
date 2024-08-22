# OneCollector Exporter for OpenTelemetry .NET

| Status        |           |
| ------------- |-----------|
| Stability     |  [Stable](../../README.md#stable)|
| Code Owners   |  [@codeblanch](https://github.com/codeblanch), [@reyang](https://github.com/reyang)|

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.OneCollector)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OneCollector)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.OneCollector)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OneCollector)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Exporter.OneCollector)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Exporter.OneCollector)

The OneCollectorExporter is designed for Microsoft products to send data to
public-facing end-points which route to Microsoft's internal data pipeline. It
is not meant to be used outside of Microsoft products and is open sourced to
demonstrate best practices and to be transparent about what is being collected.

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.OneCollector
```

## Basic usage

```csharp
using var logFactory = LoggerFactory.Create(builder => builder
    .AddOpenTelemetry(builder =>
    {
        builder.IncludeScopes = true;
        builder.AddOneCollectorExporter("InstrumentationKey=instrumentation-key-here");
    }));

var logger = logFactory.CreateLogger<MyService>();

using var scope = logger.BeginScope("{requestContext}", Guid.NewGuid());

logger.LogInformation("Request received {requestId}!", 1);
logger.LogWarning("Warning encountered {error_code}!", 0xBAADBEEF);
```
