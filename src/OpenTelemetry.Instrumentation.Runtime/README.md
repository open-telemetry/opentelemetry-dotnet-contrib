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

## Metrics

## GC related metrics

| Name                                            | Description                                                          | Units     | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|-------------------------------------------------|----------------------------------------------------------------------|-----------|-------------------|------------|------------------|------------------|
| process.runtime.dotnet.**gc.collections.count** | Number of times garbage collection has occurred since process start. | `{times}` | ObservableCounter | `Int64`    | gen              | gen0, gen1, gen2 |

- [GC.CollectionCount](https://docs.microsoft.com/dotnet/api/system.gc.collectioncount):
The number of times garbage collection has occurred for the specified generation
of objects.

### Additional GC metrics only available when targeting .NET Core 3.1 or later

| Name                                           | Description                                                                                                                                                                                             | Units | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-------|-------------------|------------|------------------|------------------|
| process.runtime.dotnet.**gc.allocations.size** | Count of the bytes allocated on the managed GC heap since the process start. .NET objects are allocated from this heap. Object allocations from unmanaged languages such as C/C++ do not use this heap. | `By`  | ObservableCounter | `Int64`    |                  |                  |

- [GC.GetTotalAllocatedBytes](https://docs.microsoft.com/dotnet/api/system.gc.gettotalallocatedbytes):
  Gets a count of the bytes allocated over the lifetime of the process. The returned
value does not include any native allocations. The value is an approximate count.

### Additional GC metrics only available when targeting .NET6 or later

| Name                                    | Description        | Units | Instrument Type | Value Type | Attribute Key(s) | Attribute Values           |
|-----------------------------------------|--------------------|-------|-----------------|------------|------------------|----------------------------|
| process.runtime.dotnet.**gc.committed_memory.size** | GC Committed Bytes | `By`  | ObservableGauge | `Int64`    |                  |                            |
| process.runtime.dotnet.**gc.heap.fragmentation.size** | GC fragmentation size                            | `By`  | ObservableGauge   | `Int64`    | gen              | gen0, gen1, gen2, loh, poh |
| process.runtime.dotnet.**gc.heap.size**  |                    | `By`  | ObservableGauge | `Int64`    | gen              | gen0, gen1, gen2, loh, poh |

- [GCMemoryInfo.TotalCommittedBytes](https://docs.microsoft.com/dotnet/api/system.gcmemoryinfo.totalcommittedbytes?view=net-6.0#system-gcmemoryinfo-totalcommittedbytes):
Gets the total committed bytes of the managed heap.

- [GCGenerationInfo.FragmentationAfterBytes Property](https://docs.microsoft.com/dotnet/api/system.gcgenerationinfo.fragmentationafterbytes#system-gcgenerationinfo-fragmentationafterbytes)
Gets the fragmentation in bytes on exit from the reported collection.

- [GC.GetGCMemoryInfo().GenerationInfo[i].SizeAfterBytes](https://docs.microsoft.com/dotnet/api/system.gcgenerationinfo):
Represents the size in bytes of a generation on exit of the GC reported in GCMemoryInfo.

## JIT Compiler related metrics

These metrics are only available when targeting .NET6 or later.

| Name                                            | Description              | Units       | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|-------------------------------------------------|--------------------------|-------------|-------------------|------------|------------------|------------------|
| process.runtime.dotnet.**il.bytes.jitted**      | IL Bytes Jitted          | `By`        | ObservableCounter | `Int64`    |                  |                  |
| process.runtime.dotnet.**methods.jitted.count** | Number of Methods Jitted | `{methods}` | ObservableCounter | `Int64`    |                  |                  |
| process.runtime.dotnet.**time.in.jit**          | Time spent in JIT        | `ns`        | ObservableCounter | `Int64`   |                  |                  |

[JitInfo.GetCompiledILBytes](https://docs.microsoft.com/dotnet/api/system.runtime.jitinfo.getcompiledilbytes?view=net-6.0#system-runtime-jitinfo-getcompiledilbytes(system-boolean)):
Gets the number of bytes of intermediate language that have been compiled.
The scope of this value is global. The same applies for other JIT related metrics.

[JitInfo.GetCompiledMethodCount](https://docs.microsoft.com/dotnet/api/system.runtime.jitinfo.getcompiledmethodcount?view=net-6.0#system-runtime-jitinfo-getcompiledmethodcount(system-boolean)):
Gets the number of methods that have been compiled.

[JitInfo.GetCompilationTime](https://docs.microsoft.com/dotnet/api/system.runtime.jitinfo.getcompilationtime?view=net-6.0#system-runtime-jitinfo-getcompilationtime(system-boolean)):
Gets the amount of time the JIT Compiler has spent compiling methods.

## Threading related metrics

These metrics are only available when targeting .NET Core 3.1 or later.

| Name                                                        | Description                          | Units       | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|-------------------------------------------------------------|--------------------------------------|-------------|-------------------|------------|------------------|------------------|
| process.runtime.dotnet.**monitor.lock.contention.count**    | Monitor Lock Contention Count        | `{times}`   | ObservableCounter | `Int64`    |                  |                  |
| process.runtime.dotnet.**threadpool.thread.count**          | ThreadPool Thread Count              | `{threads}` | ObservableGauge   | `Int32`    |                  |                  |
| process.runtime.dotnet.**threadpool.completed.items.count** | ThreadPool Completed Work Item Count | `{items}`   | ObservableCounter | `Int64`    |                  |                  |
| process.runtime.dotnet.**threadpool.queue.length**          | ThreadPool Queue Length              | `{items}`   | ObservableGauge   | `Int64`    |                  |                  |
| process.runtime.dotnet.**active.timer.count**               | Number of Active Timers              | `{timers}`  | ObservableGauge   | `Int64`    |                  |                  |

- [Monitor.LockContentionCount](https://docs.microsoft.com/dotnet/api/system.threading.monitor.lockcontentioncount?view=netcore-3.1):
  Gets the number of times there was contention when trying to take the monitor's
  lock.
- [ThreadPool.ThreadCount](https://docs.microsoft.com/dotnet/api/system.threading.threadpool.threadcount?view=netcore-3.1):
  Gets the number of thread pool threads that currently exist.
- [ThreadPool.CompletedWorkItemCount](https://docs.microsoft.com/dotnet/api/system.threading.threadpool.completedworkitemcount?view=netcore-3.1):
  Gets the number of work items that have been processed so far.
- [ThreadPool.PendingWorkItemCount](https://docs.microsoft.com/dotnet/api/system.threading.threadpool.pendingworkitemcount?view=netcore-3.1):
  Gets the number of work items that are currently queued to be processed.
- [Timer.ActiveCount](https://docs.microsoft.com/dotnet/api/system.threading.timer.activecount?view=netcore-3.1):
  Gets the number of timers that are currently active. An active timer is registered
  to tick at some point in the future, and has not yet been canceled.

## Process related metrics

| Name                    | Description                            | Units          | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|-------------------------|----------------------------------------|----------------|-------------------|------------|------------------|------------------|
| process.cpu.utilization | CPU utilization of this process        | `1`            | ObservableGauge   | `Double`   |                  |                  |
| process.cpu.time        | Processor time of this process         | `s`            | ObservableCounter | `Int64`    | state            | user, system     |
| process.memory.usage    | The amount of physical memory in use   | `By`           | ObservableGauge   | `Int64`    |                  |                  |
| process.memory.virtual  | The amount of committed virtual memory | `By`           | ObservableGauge   | `Int64`    |                  |                  |

- CPU utilization
  - [Process.TotalProcessorTime](https://docs.microsoft.com/dotnet/api/system.diagnostics.process.totalprocessortime)
  divided by ([Environment.ProcessorCount](https://docs.microsoft.com/dotnet/api/system.environment.processorcount)
  \* ([DateTime.Now](https://docs.microsoft.com/dotnet/api/system.datetime.now) -
  [Process.StartTime](https://docs.microsoft.com/dotnet/api/system.diagnostics.process.starttime)))

- CPU Time:
  - [Process.UserProcessorTime](https://docs.microsoft.com/dotnet/api/system.diagnostics.process.userprocessortime):
  Gets the user processor time for this process.
  - [Process.PrivilegedProcessorTime](https://docs.microsoft.com/dotnet/api/system.diagnostics.process.privilegedprocessortime):
  Gets the privileged processor time for this process.

- Memory usage: [Process.GetCurrentProcess().WorkingSet64](https://docs.microsoft.com/dotnet/api/system.diagnostics.process.workingset64):
  Gets the amount of physical memory, in bytes, allocated for the currently
  active process.
- Memory virtual: [Process.GetCurrentProcess().VirtualMemorySize64](https://docs.microsoft.com/dotnet/api/system.diagnostics.process.virtualmemorysize64):
  Gets the amount of the virtual memory, in bytes, allocated for the currently
  active process.

Question: EventCounter implementation exposes a metric named `working-set` with
`Environment.WorkingSet`. Is it equal to `Process.GetCurrentProcess().WorkingSet64`
property? I need to decide on which is more suitable for showing users the memory
usage for the process, or whether to include both.

- [Environment.WorkingSet](https://docs.microsoft.com/en-us/dotnet/api/system.environment.workingset?view=net-6.0):
  A 64-bit signed integer containing the number of bytes of physical memory mapped
  to the process context.

## Assemblies related metrics

| Name                                      | Description                 | Units          | Instrument Type | Value Type | Attribute Key(s) | Attribute Values |
|-------------------------------------------|-----------------------------|----------------|-----------------|------------|------------------|------------------|
| process.runtime.dotnet.**assembly.count** | Number of Assemblies Loaded | `{assemblies}` | ObservableGauge | `Int64`    |                  |                  |

- [AppDomain.GetAssemblies](https://docs.microsoft.com/dotnet/api/system.appdomain.getassemblies):
  Gets the number of the assemblies that have been loaded into the execution context
  of this application domain.

## Exception counter metric

| Name                                       | Description                                | Units      | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|--------------------------------------------|--------------------------------------------|------------|-------------------|------------|------------------|------------------|
| process.runtime.dotnet.**exception.count** | Number of exception thrown in managed code | `{timers}` | ObservableCounter | `Int64`    |                  |                  |

- [AppDomain.FirstChanceException](https://docs.microsoft.com/dotnet/api/system.appdomain.firstchanceexception)
  Occurs when an exception is thrown in managed code, before the runtime searches
  the call stack for an exception handler in the application domain.

## Troubleshooting

This component uses an
[EventSource](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventsource)
with the name "OpenTelemetry-Instrumentation-Runtime" for its internal
logging. Please refer to [SDK
troubleshooting](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry#troubleshooting)
for instructions on seeing these internal logs.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
