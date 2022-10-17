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
dotnet add package --prerelease OpenTelemetry.Instrumentation.Process
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

### process.memory.usage

The amount of physical memory allocated for this process.

| Units | Instrument Type   | Value Type |
|-------|-------------------|------------|
|  `By` |  ObservableGauge  | `Double`   |

### process.memory.virtual

The amount of virtual memory allocated for this process
that cannot be shared with other processes.

| Units | Instrument Type   | Value Type |
|-------|-------------------|------------|
|  `By` |  ObservableGauge  | `Double`   |

### process.cpu.time

Total CPU seconds broken down by states.

| Units | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|-------|-------------------|------------|------------------|------------------|
|  `s`  | ObservableCounter | `Double`   | state            | user, system     |

### process.cpu.utilization

Difference in process.cpu.time since the last measurement,
divided by the elapsed time and number of CPUs available to the process.

| Units | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|-------|-------------------|------------|------------------|------------------|
|  `1`  | ObservableCounter | `Double`   | state            | user, system     |

### process.threads

Process threads count.

| Units           | Instrument Type   | Value Type |
|-----------------|-------------------|------------|
| `{threads}`     | ObservableCounter | `Int32`    |

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
