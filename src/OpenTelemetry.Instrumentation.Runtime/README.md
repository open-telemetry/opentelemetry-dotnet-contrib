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

Runtime instrumentation should be enabled at application startup.

#### For ASP.NET Core applications

The example below enables this instrumentation by using an extension method on
`IServiceCollection`. This extension method requires adding the package
[`OpenTelemetry.Extensions.Hosting`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Extensions.Hosting/README.md)
to the application. This ensures the instrumentation is disposed when the host
is shutdown.

Additionally, this examples sets up the OpenTelemetry Prometheus exporter, which
requires adding the package
[`OpenTelemetry.Exporter.Prometheus`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Prometheus/README.md)
to the application.

```csharp
using OpenTelemetry.Metrics;

builder.Services.AddOpenTelemetryMetrics(options =>
{
    options.AddRuntimeInstrumentation();
    options.AddPrometheusExporter();
});
```

#### For general purpose applications

Configure directly:

```csharp
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddRuntimeInstrumentation()
    .AddPrometheusExporter()
    .Build();
```

Refer to [Program.cs](../../examples/runtime-instrumentation/Program.cs) for a
complete demo.

## Metrics

### GC related metrics

#### process.runtime.dotnet.**gc.collections.count**

Number of garbage collections that have occurred since process start.

Note: Collecting a generation means collecting objects in that generation and all
its younger generations. However, each dimension for this metrics doesn't include
the collection counts for the lower generation.
e.g. count for gen1 is `GC.CollectionCount(1) - GC.CollectionCount(0)`.

| Units           | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|-----------------|-------------------|------------|------------------|------------------|
| `{collections}` | ObservableCounter | `Int64`    | generation       | gen0, gen1, gen2 |

The API used to retrieve the value is:

* [GC.CollectionCount](https://docs.microsoft.com/dotnet/api/system.gc.collectioncount):
  The number of times garbage collection has occurred for the specified generation
of objects.

#### process.runtime.dotnet.**gc.allocations.size**

Count of bytes allocated on the managed GC heap since the process start.
.NET objects are allocated from this heap. Object allocations from unmanaged languages
such as C/C++ do not use this heap.

Note: This metric is only available when targeting .NET Core 3.1 or later.

| Units   | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|---------|-------------------|------------|------------------|------------------|
| `bytes` | ObservableCounter | `Int64`    | No Attributes    | N/A              |

The API used to retrieve the value is:

* [GC.GetTotalAllocatedBytes](https://docs.microsoft.com/dotnet/api/system.gc.gettotalallocatedbytes):
  Gets a count of the bytes allocated over the lifetime of the process. The returned
value does not include any native allocations. The value is an approximate count.

#### process.runtime.dotnet.**gc.committed_memory.size**

The amount of committed virtual memory for the managed GC heap, as
observed during the latest garbage collection. Committed virtual memory may be
larger than the heap size because it includes both memory for storing existing
objects (the heap size) and some extra memory that is ready to handle newly
allocated objects in the future. The value will be unavailable until at least one
garbage collection has occurred.

Note: `ObservableGauge` should be changed to `ObservableUpDownCounter` once available,
as `ObservableUpDownCounter` is the best fit of instrument type. The same applies
to all the `ObservableGauge` below.

Note: This metric is only available when targeting .NET6 or later.

Note: `gc.heap.fragmentation.size` metrics is removed for .NET 6 because of a
[bug](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/496).

| Units   | Instrument Type | Value Type | Attribute Key(s) | Attribute Values |
|---------|-----------------|------------|------------------|------------------|
| `bytes` | ObservableGauge | `Int64`    | No Attributes    | N/A              |

The API used to retrieve the value is:

* [GCMemoryInfo.TotalCommittedBytes](https://docs.microsoft.com/dotnet/api/system.gcmemoryinfo.totalcommittedbytes):
  Gets the total committed bytes of the managed heap.

#### process.runtime.dotnet.**gc.heap.size**

The heap size (including fragmentation), as observed during the
latest garbage collection. The value will be unavailable until at least one
garbage collection has occurred.

Note: This metric is only available when targeting .NET6 or later.

| Units   | Instrument Type | Value Type | Attribute Key(s) | Attribute Values           |
|---------|-----------------|------------|------------------|----------------------------|
| `bytes` | ObservableGauge | `Int64`    | generation       | gen0, gen1, gen2, loh, poh |

The API used to retrieve the value is:

* [GC.GetGCMemoryInfo().GenerationInfo[i].SizeAfterBytes](https://docs.microsoft.com/dotnet/api/system.gcgenerationinfo):
  Represents the size in bytes of a generation on exit of the GC reported in GCMemoryInfo.
  Note that this API on .NET 6 has a [bug](https://github.com/dotnet/runtime/pull/60309).
  For .NET 6, heap size is retrieved with an internal method `GC.GetGenerationSize`,
  which is how the [well-known EventCounters](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/available-counters)
  retrieve the values.
  See source code [here](https://github.com/dotnet/runtime/blob/b4dd16b4418de9b3af08ae85f0f3653e55dc420a/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Tracing/RuntimeEventSource.cs#L110-L114).

#### process.runtime.dotnet.**gc.heap.fragmentation.size**

The heap fragmentation, as observed during the latest garbage collection.
The value will be unavailable until at least one garbage collection has occurred.

Note: This metric is only available when targeting .NET6 or later.

| Units   | Instrument Type | Value Type | Attribute Key(s) | Attribute Values           |
|---------|-----------------|------------|------------------|----------------------------|
| `bytes` | ObservableGauge | `Int64`    | generation       | gen0, gen1, gen2, loh, poh |

The API used to retrieve the value is:

* [GCGenerationInfo.FragmentationAfterBytes Property](https://docs.microsoft.com/dotnet/api/system.gcgenerationinfo.fragmentationafterbytes)
  Gets the fragmentation in bytes on exit from the reported collection.

### JIT Compiler related metrics

These metrics are only available when targeting .NET6 or later.

#### process.runtime.dotnet.**jit.il_compiled.size**

Count of bytes of intermediate language that have been compiled since
the process start.

| Units   | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|---------|-------------------|------------|------------------|------------------|
| `bytes` | ObservableCounter | `Int64`    | No Attributes    | N/A              |

#### process.runtime.dotnet.**jit.methods_compiled.count**

The number of times the JIT compiler compiled a method since the process
start.  The JIT compiler may be invoked multiple times for the same method to compile
with different generic parameters, or because tiered compilation requested different
optimization settings.

| Units       | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|-------------|-------------------|------------|------------------|------------------|
| `{methods}` | ObservableCounter | `Int64`    | No Attributes    | N/A              |

#### process.runtime.dotnet.**jit.compilation_time**

The amount of time the JIT compiler has spent compiling methods since
the process start.

| Units | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|-------|-------------------|------------|------------------|------------------|
| `ns`  | ObservableCounter | `Int64`    | No Attributes    | N/A              |

The APIs used to retrieve the values are:

* [JitInfo.GetCompiledILBytes](https://docs.microsoft.com/dotnet/api/system.runtime.jitinfo.getcompiledilbytes):
  Gets the number of bytes of intermediate language that have been compiled.
The scope of this value is global. The same applies for other JIT related metrics.

* [JitInfo.GetCompiledMethodCount](https://docs.microsoft.com/dotnet/api/system.runtime.jitinfo.getcompiledmethodcount):
  Gets the number of methods that have been compiled.

* [JitInfo.GetCompilationTime](https://docs.microsoft.com/dotnet/api/system.runtime.jitinfo.getcompilationtime):
  Gets the amount of time the JIT Compiler has spent compiling methods.

### Threading related metrics

These metrics are only available when targeting .NET Core 3.1 or later.

#### process.runtime.dotnet.**monitor.lock_contention.count**

The number of times there was contention when trying to acquire a
monitor lock since the process start. Monitor locks are commonly acquired by using
the lock keyword in C#, or by calling Monitor.Enter() and Monitor.TryEnter().

| Units                      | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|----------------------------|-------------------|------------|------------------|------------------|
| `{contended_acquisitions}` | ObservableCounter | `Int64`    | No Attributes    | N/A              |

#### process.runtime.dotnet.**thread_pool.threads.count**

The number of thread pool threads that currently exist.

| Units       | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|-------------|-------------------|------------|------------------|------------------|
| `{threads}` | ObservableGauge   | `Int32`    | No Attributes    | N/A              |

#### process.runtime.dotnet.**thread_pool.completed_items.count**

The number of work items that have been processed by the thread pool
since the process start.

| Units       | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|-------------|-------------------|------------|------------------|------------------|
| `{items}`   | ObservableCounter | `Int64`    | No Attributes    | N/A              |

#### process.runtime.dotnet.**thread_pool.queue.length**

The number of work items that are currently queued to be processed
by the thread pool.

| Units       | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|-------------|-------------------|------------|------------------|------------------|
| `{items}`   | ObservableGauge   | `Int64`    | No Attributes    | N/A              |

#### process.runtime.dotnet.**timer.count**

The number of timer instances that are currently active. Timers can
be created by many sources such as System.Threading.Timer, Task.Delay, or the
timeout in a CancellationSource. An active timer is registered to tick at some
point in the future and has not yet been canceled.

| Units       | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|-------------|-------------------|------------|------------------|------------------|
| `{timers}`  | ObservableGauge   | `Int64`    | No Attributes    | N/A              |

The APIs used to retrieve the values are:

* [Monitor.LockContentionCount](https://docs.microsoft.com/dotnet/api/system.threading.monitor.lockcontentioncount):
  Gets the number of times there was contention when trying to take the monitor's
  lock.
* [ThreadPool.ThreadCount](https://docs.microsoft.com/dotnet/api/system.threading.threadpool.threadcount):
  Gets the number of thread pool threads that currently exist.
* [ThreadPool.CompletedWorkItemCount](https://docs.microsoft.com/dotnet/api/system.threading.threadpool.completedworkitemcount):
  Gets the number of work items that have been processed so far.
* [ThreadPool.PendingWorkItemCount](https://docs.microsoft.com/dotnet/api/system.threading.threadpool.pendingworkitemcount):
  Gets the number of work items that are currently queued to be processed.
* [Timer.ActiveCount](https://docs.microsoft.com/dotnet/api/system.threading.timer.activecount):
  Gets the number of timers that are currently active. An active timer is registered
  to tick at some point in the future, and has not yet been canceled.

### Assemblies related metrics

#### process.runtime.dotnet.**assemblies.count**

The number of .NET assemblies that are currently loaded.

| Units          | Instrument Type | Value Type | Attribute Key(s) | Attribute Values |
|----------------|-----------------|------------|------------------|------------------|
| `{assemblies}` | ObservableGauge | `Int64`    | No Attributes    | N/A              |

The API used to retrieve the value is:

* [AppDomain.GetAssemblies](https://docs.microsoft.com/dotnet/api/system.appdomain.getassemblies):
  Gets the number of the assemblies that have been loaded into the execution context
  of this application domain.

### Exception counter metric

#### process.runtime.dotnet.**exceptions.count**

Count of exceptions that have been thrown in managed code, since the
observation started. The value will be unavailable until an exception has been
thrown after OpenTelemetry.Instrumentation.Runtime initialization.

Note: The value is tracked by incrementing a counter whenever an AppDomain.FirstChanceException
event occurs. The observation starts when the Runtime instrumentation library is
initialized, so the value will be unavailable until an exception has been
thrown after the initialization.

| Units          | Instrument Type | Value Type | Attribute Key(s) | Attribute Values |
|----------------|-----------------|------------|------------------|------------------|
| `{exceptions}` | Counter         | `Int64`    | No Attributes    | N/A              |

Relevant API:

* [AppDomain.FirstChanceException](https://docs.microsoft.com/dotnet/api/system.appdomain.firstchanceexception)
  Occurs when an exception is thrown in managed code, before the runtime searches
  the call stack for an exception handler in the application domain.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
