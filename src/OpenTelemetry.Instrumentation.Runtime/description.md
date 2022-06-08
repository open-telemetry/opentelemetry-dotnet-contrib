# Runtime metrics description

Metrics name are prefixed with the `process.runtime.dotnet.` namespace, following
the general guidance for runtime metrics in the
[specs](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/semantic_conventions/runtime-environment-metrics.md#runtime-environment-specific-metrics---processruntimeenvironment).
Instrument Units [should follow](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/semantic_conventions/README.md#instrument-units)
the Unified Code for Units of Measure.

## GC related metrics

The metrics in this section can be enabled by setting the
`RuntimeMetricsOptions.IsGcEnabled` switch.

| Name                                          | Description              | Units     | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|-----------------------------------------------|--------------------------|-----------|-------------------|------------|------------------|------------------|
| process.runtime.dotnet.**gc.totalmemorysize** | Total allocated bytes    | `By`      | ObservableGauge   | `Int64`    |                  |                  |
| process.runtime.dotnet.**gc.count**           | Garbage Collection count | `{times}` | ObservableCounter | `Int64`    | gen              | gen0, gen1, gen2 |

Question for .NET team: is GC.GetTotalMemory(false) always equal to the sum of
GC.GetGCMemoryInfo().GenerationInfo[i].SizeAfterBytes (i from 0 to 4)?

I need to decide whether it makes sense to include both of them in the memory/GC
size metrics.

- [GC.GetTotalMemory](https://docs.microsoft.com/dotnet/api/system.gc.gettotalmemory):
The number of bytes currently thought to be allocated.
It does not wait for garbage collection to occur before returning.

- [GC.CollectionCount](https://docs.microsoft.com/dotnet/api/system.gc.collectioncount):
The number of times garbage collection has occurred for the specified generation
of objects.

### Additional GC metrics only available for NETCOREAPP3_1_OR_GREATER

| Name                                              | Description                                      | Units | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|---------------------------------------------------|--------------------------------------------------|-------|-------------------|------------|------------------|------------------|
| process.runtime.dotnet.**gc.allocated.bytes**     | Bytes allocated over the lifetime of the process | `By`  | ObservableCounter | `Int64`    |                  |                  |
| process.runtime.dotnet.**gc.fragmentation.ratio** | GC fragmentation ratio                           | `1`   | ObservableGauge   | `Double`   |                  |                  |

- [GC.GetTotalAllocatedBytes](https://docs.microsoft.com/dotnet/api/system.gc.gettotalallocatedbytes):
  Gets a count of the bytes allocated over the lifetime of the process. The returned
value does not include any native allocations. The value is an approximate count.

- GC fragmentation ratio is calculated as:
If `gcMemoryInfo.HeapSizeBytes != 0`,
the value is
`gcMemoryInfo.FragmentedBytes * 1.0d / gcMemoryInfo.HeapSizeBytes`,
otherwise the value is `0`, where `var gcMemoryInfo = GC.GetGCMemoryInfo()`.

  - [GCMemoryInfo.FragmentedBytes](https://docs.microsoft.com/dotnet/api/system.gcmemoryinfo.fragmentedbytes?view=netcore-3.1):
  Gets the total fragmentation when the last garbage collection occurred.
  - [GCMemoryInfo.HeapSizeBytes](https://docs.microsoft.com/dotnet/api/system.gcmemoryinfo.heapsizebytes?view=netcore-3.1#system-gcmemoryinfo-heapsizebytes):
  Gets the total heap size when the last garbage collection occurred.

### Additional GC metrics only available for NET6_0_OR_GREATER

| Name                                    | Description        | Units | Instrument Type | Value Type | Attribute Key(s) | Attribute Values           |
|-----------------------------------------|--------------------|-------|-----------------|------------|------------------|----------------------------|
| process.runtime.dotnet.**gc.committed** | GC Committed Bytes | `By`  | ObservableGauge | `Int64`    |                  |                            |
| process.runtime.dotnet.**gc.heapsize**  |                    | `By`  | ObservableGauge | `Int64`    | gen              | gen0, gen1, gen2, loh, poh |

- [GCMemoryInfo.TotalCommittedBytes](https://docs.microsoft.com/dotnet/api/system.gcmemoryinfo.totalcommittedbytes?view=net-6.0#system-gcmemoryinfo-totalcommittedbytes):
Gets the total committed bytes of the managed heap.

- [GC.GetGCMemoryInfo().GenerationInfo[i].SizeAfterBytes](https://docs.microsoft.com/dotnet/api/system.gcgenerationinfo):
Represents the size in bytes of a generation on exit of the GC reported in GCMemoryInfo.
(The number of generations `i` is limited by [GC.MaxGeneration](https://docs.microsoft.com/dotnet/api/system.gc.maxgeneration))

## JIT Compiler related metrics

The metrics in this section can be enabled by setting the
`RuntimeMetricsOptions.IsJitEnabled` switch.

These metrics are only available for NET6_0_OR_GREATER.

| Name                                            | Description              | Units       | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|-------------------------------------------------|--------------------------|-------------|-------------------|------------|------------------|------------------|
| process.runtime.dotnet.**il.bytes.jitted**      | IL Bytes Jitted          | `By`        | ObservableCounter | `Int64`    |                  |                  |
| process.runtime.dotnet.**methods.jitted.count** | Number of Methods Jitted | `{methods}` | ObservableCounter | `Int64`    |                  |                  |
| process.runtime.dotnet.**time.in.jit**          | Time spent in JIT        | `ns`        | ObservableCounter | `Int64`   |                  |                  |

[JitInfo.GetCompiledILBytes](https://docs.microsoft.com/dotnet/api/system.runtime.jitinfo.getcompiledilbytes?view=net-6.0#system-runtime-jitinfo-getcompiledilbytes(system-boolean)):
Gets the number of bytes of intermediate language that have been compiled.
The scope of this value is global.

[JitInfo.GetCompiledMethodCount](https://docs.microsoft.com/dotnet/api/system.runtime.jitinfo.getcompiledmethodcount?view=net-6.0#system-runtime-jitinfo-getcompiledmethodcount(system-boolean)):
Gets the number of methods that have been compiled.
The scope of this value is global.

[JitInfo.GetCompilationTime](https://docs.microsoft.com/dotnet/api/system.runtime.jitinfo.getcompilationtime?view=net-6.0#system-runtime-jitinfo-getcompilationtime(system-boolean)):
Gets the amount of time the JIT Compiler has spent compiling methods.
The scope of this value is global.

## Threading related metrics

The metrics in this section can be enabled by setting the
`RuntimeMetricsOptions.IsThreadingEnabled` switch.

These metrics are only available for NETCOREAPP3_1_OR_GREATER.

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

The metrics in this section can be enabled by setting the
`RuntimeMetricsOptions.IsProcessEnabled` switch.

| Name                    | Description                            | Units          | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|-------------------------|----------------------------------------|----------------|-------------------|------------|------------------|------------------|
| process.cpu.utilization | CPU utilization of this process        | `1`            | ObservableGauge   | `Double`   |                  |                  |
| process.cpu.time        | Processor time of this process         | `s`            | ObservableCounter | `Int64`    | state            | user, system     |
| process.cpu.count       | The number of available logical CPUs   | `{processors}` | ObservableGauge   | `Int64`    |                  |                  |
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

- [Environment.ProcessorCount](https://docs.microsoft.com/dotnet/api/system.environment.processorcount):
  Gets the number of processors available to the current process.
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

The metrics in this section can be enabled by setting the
`RuntimeMetricsOptions.IsAssembliesEnabled` switch.

| Name                                      | Description                 | Units          | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|-------------------------------------------|-----------------------------|----------------|-------------------|------------|------------------|------------------|
| process.runtime.dotnet.**assembly.count** | Number of Assemblies Loaded | `{assemblies}` | ObservableCounter | `Int64`    |                  |                  |

- [AppDomain.GetAssemblies](https://docs.microsoft.com/dotnet/api/system.appdomain.getassemblies):
  Gets the number of the assemblies that have been loaded into the execution context
  of this application domain.

## Exception counter metric

The metrics in this section can be enabled by setting the
`RuntimeMetricsOptions.IsExceptionCounterEnabled` switch.

| Name                                       | Description                                | Units      | Instrument Type   | Value Type | Attribute Key(s) | Attribute Values |
|--------------------------------------------|--------------------------------------------|------------|-------------------|------------|------------------|------------------|
| process.runtime.dotnet.**exception.count** | Number of exception thrown in managed code | `{timers}` | ObservableCounter | `Int64`    |                  |                  |

- [AppDomain.FirstChanceException](https://docs.microsoft.com/dotnet/api/system.appdomain.firstchanceexception)
  Occurs when an exception is thrown in managed code, before the runtime searches
  the call stack for an exception handler in the application domain.

## Currently out of scope

Regarding process.runtime.dotnet.**time-in-gc**: (DisplayName in [EventCounter implementation](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Tracing/RuntimeEventSource.cs#L96)
is "% Time in GC since last GC".) A new metric should replace it by calling a new
API GC.GetTotalPauseDuration().
The new API is added in code but not available yet.
It is targeted for 7.0.0 milestone in .NET Runtime repo.
See [dotnet/runtime#65989](https://github.com/dotnet/runtime/issues/65989)
