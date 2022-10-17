# Process Instrumentation for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.Process.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Process)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.Process.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Process)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments [.NET](https://docs.microsoft.com/dotnet) and
collect telemetry about process behavior.

## Steps to enable OpenTelemetry.Instrumentation.Process

### Step 1: Install package

Add a reference to
[`OpenTelemetry.Instrumentation.Process`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Process)
package.

```shell
dotnet add package OpenTelemetry.Instrumentation.Process --version 0.1.0-alpha.1
```

Add a reference to
[`OpenTelemetry.Exporter.Prometheus.HttpListener`](https://www.nuget.org/packages/OpenTelemetry.Exporter.Prometheus.HttpListener)
package.

```shell
dotnet add package --prerelease OpenTelemetry.Exporter.Prometheus.HttpListener
```

### Step 2: Enable Process instrumentation

Process instrumentation should be enabled at application startup using the
`AddProcessInstrumentation` extension on `MeterProviderBuilder`:

```csharp
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddProcessInstrumentation()
    .AddPrometheusHttpListener()
    .Build();
```

Refer to [Program.cs](../../examples/runtime-instrumentation/Program.cs) for a
complete demo.

## Metrics

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
