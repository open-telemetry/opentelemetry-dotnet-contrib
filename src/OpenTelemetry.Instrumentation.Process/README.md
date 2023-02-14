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

Refer to [Program.cs](../../examples/process-instrumentation/Program.cs) for a
complete demo.

## Metrics

### process.memory.usage

The amount of physical memory allocated for this process.

| Units | Instrument Type         | Value Type |
|-------|-------------------------|------------|
| `By`  | ObservableUpDownCounter | `Double`   |

The API used to retrieve the value is:

* [Process.WorkingSet64](https://learn.microsoft.com/dotnet/api/system.diagnostics.process.workingset64):
Gets the amount of physical memory, in bytes,
allocated for the associated process.

### process.memory.virtual

The amount of committed virtual memory for this process.
One way to think of this is all the address space this process can read from
without trigerring an access violation; this includes memory backed solely by RAM,
by a swapfile/pagefile and by other mapped files on disk.

| Units | Instrument Type         | Value Type |
|-------|-------------------------|------------|
|  `By` | ObservableUpDownCounter | `Double`   |

The API used to retrieve the value is:

* [Process.VirtualMemorySize64](https://learn.microsoft.com/dotnet/api/system.diagnostics.process.virtualmemorysize64):
Gets the amount of the virtual memory, in bytes,
allocated for the associated process.

### process.cpu.time

Total CPU seconds broken down by states.

| Units | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|-------|-------------------|------------|------------------|------------------|
|  `s`  | ObservableCounter | `Double`   | state            | user, system     |

The APIs used to retrieve the values are:

* [Process.UserProcessorTime](https://learn.microsoft.com/dotnet/api/system.diagnostics.process.userprocessortime):
Gets the user processor time for this process.

* [Process.PrivilegedProcessorTime](https://learn.microsoft.com/dotnet/api/system.diagnostics.process.privilegedprocessortime):
Gets the privileged processor time for this process.

### process.cpu.count

The number of processors (CPU cores) available to the current process.

| Units         | Instrument Type         | Value Type |
|---------------|-------------------------|------------|
| `{processors}`| ObservableUpDownCounter | `Int32`    |

The API used to retrieve the value is [System.Environment.ProcessorCount](https://learn.microsoft.com/dotnet/api/system.environment.processorcount).

### process.threads

Process threads count.

| Units      | Instrument Type         | Value Type |
|------------|-------------------------|------------|
| `{threads}`| ObservableUpDownCounter | `Int32`    |

The API used to retrieve the value is:

* [Process.Threads](https://learn.microsoft.com/dotnet/api/system.diagnostics.process.threads):
Gets the set of threads that are running
in the associated process.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
