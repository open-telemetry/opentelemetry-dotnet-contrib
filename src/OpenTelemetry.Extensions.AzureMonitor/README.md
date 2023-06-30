# Application Insights Sampler for OpenTelemetry .NET

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Extensions.AzureMonitor.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.AzureMonitor)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Extensions.AzureMonitor.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.AzureMonitor)

The ```Application Insights Sampler``` should be utilized when
compatibility with Application Insights SDKs is desired, as it
implements the same hash algorithm when deciding to sample telemetry.

## Installation

```shell
dotnet add package OpenTelemetry.Extensions.AzureMonitor
```

## Usage

You can configure the `ApplicationInsightsSampler` with the following example.
In this example the `samplingRatio` has been set to `0.4F`.
This means 40% of traces are sampled and the remaining 60% will be dropped.

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                            .SetSampler(new ApplicationInsightsSampler(0.4F))
                            .Build();
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
