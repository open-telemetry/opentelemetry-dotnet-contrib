# OpenTelemetry .NET SDK preview features and extensions

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Extensions)](https://www.nuget.org/packages/OpenTelemetry.Extensions)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Extensions)](https://www.nuget.org/packages/OpenTelemetry.Extensions)

Contains useful features and extensions to the OpenTelemetry .NET SDK that are
not part of the official OpenTelemetry specification but might be added in the
future.

## Logging

### AttachLogsToActivityEvent

Adds a log processor which will convert log messages into events and attach them
to the currently running `Activity`.

## Traces

### AutoFlushActivityProcessor

Processor that flushes its containing `TracerProvider` if an ended `Activity`
matches a predicate. Note that this processor must be added *after* exporter
related `Activity` processors.

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
        .AddAutoFlushActivityProcessor(a => a.Parent == null && (a.Kind == ActivityKind.Server || a.Kind == ActivityKind.Consumer), 5000);
}
```

### BaggageActivityProcessor

The BaggageActivityProcessor reads entries stored in Baggage from the current `Baggage`
and adds the baggage keys and values to the `Activity` as tags (attributes) on start.

Add this activity processor to a tracer provider.

For example, to add all baggage entries to new activities:

```cs
var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddBaggageActivityProcessor(BaggageSpanProcessor.AllowAllBaggageKeys)
    .Build();
```

Alternatively, you can select which baggage keys you want to copy using a
custom predicate function.

For example, to only copy baggage entries that start with `my-key` using a
custom function:

```cs
var tracerProvider = Sdk.CreateTracerProviderBuilder()
  .AddBaggageActivityProcessor((baggageKey) => baggageKey.StartWith("my-key", System.StringComparison.Ordinal))
  .Build();
```

For example, to only copy baggage entries that start with `my-key` using a
regular expression:

```cs
var regex = new Regex("^mykey");
var tracerProvider = Sdk.CreateTracerProviderBuilder()
  .AddBaggageActivityProcessor((baggageKey) => regex.IsMatch(baggageKey))
  .Build();
```

Warning: The baggage key predicate is executed for every started activity.
Do not use slow or intensive operations.
