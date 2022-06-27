# DotNet Runtime Instrumentation for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.Runtime.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Runtime)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.Runtime.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Runtime)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments [.NET Runtime](https://docs.microsoft.com/dotnet) and
collect telemetry about runtime behavior.

## Steps to enable OpenTelemetry.Instrumentation.Runtime

### Step 1: Install package

Add a reference to the
[`OpenTelemetry.Instrumentation.Runtime`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Runtime)
package.

```shell
dotnet add package OpenTelemetry.Instrumentation.Runtime
```

### Step 2: Enable runtime instrumentation

Runtime instrumentation should be enabled at application startup. This is
typically done in the `ConfigureServices` of your `Startup` class. The example
below enables this instrumentation by using an extension method on
`IServiceCollection`. This extension method requires adding the package
[`OpenTelemetry.Extensions.Hosting`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Extensions.Hosting/README.md)
to the application. This ensures the instrumentation is disposed when the host
is shutdown.

Additionally, this examples sets up the OpenTelemetry Prometheus exporter, which
requires adding the package
[`OpenTelemetry.Exporter.Prometheus`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Prometheus/README.md)
to the application.

```csharp
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

public void ConfigureServices(IServiceCollection services)
{
    services.AddOpenTelemetryMetrics((builder) => builder
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter()
    );
}
```

Or configure directly:

```csharp
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddRuntimeInstrumentation()
    .AddPrometheusExporter()
    .Build();
```

Refer to [Program.cs](../../examples/runtime-instrumentation/Program.cs) for a
complete demo.

## Troubleshooting

This component uses an
[EventSource](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventsource)
with the name "OpenTelemetry-Instrumentation-Runtime" for its internal
logging. Please refer to [SDK
troubleshooting](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry#troubleshooting)
for instructions on seeing these internal logs.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
