# OpenTelemetry .NET SDK preview features and extensions

[![nuget](https://img.shields.io/nuget/v/OpenTelemetry.Extensions.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Extensions.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions)

Contains useful features and extensions to the OpenTelemetry .NET SDK that are
not part of the official OpenTelemetry specification but might be added in the
future.

## Logging

### AttachLogsToActivityEvent

Adds a log processor which will convert log messages into events and attach them
to the currently running Activity.

## Traces

### AutoFlushActivityProcessor

Processor that flushes its containing TracerProvider if an ended activity matches a predicate. 
Note that this processor must be added *after* exporter related span processors.

Example of AutoFlushActivityProcessor usage:
```cs
public static TracerProviderBuilder AddMyExporter(this TracerProviderBuilder builder, MyExporterOptions options)
{
    return builder
        .AddProcessor(new BatchActivityExportProcessor(
                    new MyExporter(options),
                    options.BatchExportProcessorOptions.MaxQueueSize,
                    options.BatchExportProcessorOptions.ScheduledDelayMilliseconds,
                    options.BatchExportProcessorOptions.ExporterTimeoutMilliseconds,
                    options.BatchExportProcessorOptions.MaxExportBatchSize))
        .AddAutoFlushProcessorProcessor(a => a.Parent == null && (a.Kind == ActivityKind.Server || a.Kind == ActivityKind.Consumer), 5000);
}
```