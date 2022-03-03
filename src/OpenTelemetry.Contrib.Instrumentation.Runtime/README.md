# DotNet Runtime Instrumentation for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Contrib.Instrumentation.Runtime.svg)](https://www.nuget.org/packages/OpenTelemetry.Contrib.Instrumentation.Runtime)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Contrib.Instrumentation.Runtime.svg)](https://www.nuget.org/packages/OpenTelemetry.Contrib.Instrumentation.Runtime)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments [.NET Runtime](https://docs.microsoft.com/dotnet) and
collect telemetry about runtime behavior.

## Steps to enable OpenTelemetry.Contrib.Instrumentation.Runtime

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Contrib.Instrumentation.Runtime`](https://www.nuget.org/packages/OpenTelemetry.Contrib.Instrumentation.Runtime)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Contrib.Instrumentation.Runtime
```

### Step 2: Enable Runtime Instrumentation at application startup

Runtime instrumentation must be enabled at application startup. This is
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
        .AddRuntimeMetrics()
        .AddPrometheusExporter()
    );
}
```

Or configure directly:

```csharp
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddRuntimeMetrics()
    .Build();
```

## Advanced configuration

By default all available runtime metrics will be added. It's also possible to
specify only the required metrics:

```csharp
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddRuntimeMetrics(options => options
    {
        options.GcEnabled = true;
        options.ThreadingEnabled = true;
        options.MemoryEnabled = true;
     })
    .Build();
```

## Troubleshooting

This component uses an
[EventSource](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventsource)
with the name "OpenTelemetry-Instrumentation-Runtime" for its internal
logging. Please refer to [SDK
troubleshooting](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry#troubleshooting)
for instructions on seeing these internal logs.

## References

* [Introduction to ASP.NET
  Core](https://docs.microsoft.com/aspnet/core/introduction-to-aspnet-core)
* [OpenTelemetry Project](https://opentelemetry.io/)
