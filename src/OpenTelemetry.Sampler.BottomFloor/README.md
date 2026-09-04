# OpenTelemetry Bottom-Floor Sampler

| Status | |
| ------ | --- |
| Stability | [Development](../../README.md#development) |
| Code Owners | [@jmacd](https://github.com/jmacd), [@Yun-Ting](https://github.com/Yun-Ting) |

This package provides fixed-memory sampling for callsite-labelled log streams.
It includes:

* `BottomFloorLogExporter`, which samples OpenTelemetry log export batches; and
* `BottomFloorLogRecordProcessor`, which combines batching and sampling in one
  processor.

The sampling algorithm itself is an internal implementation detail of this
package; it may be exposed as public API in a later release.

The sampler keeps at most a configured number of records from each window and
adds adjusted-count attributes to retained records so downstream systems can
estimate the original event counts.

## How it works

Within a window, each arrival is assigned an exponential priority based on its
callsite's weight. The sampler retains the `k + 1` smallest priorities. When the
window closes, the first `k` records form the sample and the remaining priority
defines the inclusion threshold.

The resulting callsite estimates determine the weights for the next window.
Frequently occurring callsites receive lower weights, while newly observed and
rare callsites retain a higher chance of inclusion. The only required setting is
the per-window record budget `k`.

## Sampling OpenTelemetry logs

Register `BottomFloorLogRecordProcessor` with an inner exporter:

```csharp
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Sampler.BottomFloor;

var options = new BottomFloorLogSamplerOptions
{
    Budget = 100,
};

// The sampler decorates an exporter rather than replacing it, forwarding only
// the records it keeps. Any BaseExporter<LogRecord> works; substitute the OTLP
// exporter for a real deployment.
BaseExporter<LogRecord> innerExporter = new ConsoleLogRecordExporter(new ConsoleExporterOptions());

using var loggerFactory = LoggerFactory.Create(builder => builder.AddOpenTelemetry(loggerOptions =>
    loggerOptions.AddProcessor(new BottomFloorLogRecordProcessor(
        innerExporter,
        options,
        maxExportBatchSize: 2048,
        scheduledDelayMilliseconds: 5000))));

var logger = loggerFactory.CreateLogger("MyCompany.MyApp");
```

The processor takes ownership of `innerExporter` and disposes it, so do not
register that exporter separately.

Each exported batch is one sampling window. `maxExportBatchSize` must be larger
than `Budget`, otherwise the budget could never bind and the processor throws;
a window containing no more records than the budget is forwarded without
sampling.

The exporter identifies a callsite from the log category, event ID, and event
name. Retained records may contain:

| Attribute | Meaning |
| --------- | ------- |
| `otel.logs.adjusted_count` | Reciprocal of the record's inclusion probability. Omitted when the value is one. |
| `otel.logs.cv2` | Squared coefficient of variation for a sampled callsite's count estimate. |

Attribute names are configurable through `BottomFloorLogSamplerOptions`.

### Callsite quality

How well the sampler works depends on how sharply callsites separate distinct
log statements. The identity is a hash of the category, the numeric event id and
the event name, so it is only as specific as what the logging call supplies.

Compile-time source generation gives the best result. `[LoggerMessage]` sets the
event name to the generated method name, so each log statement becomes its own
callsite. The sampler can then suppress a chatty statement while still
protecting a rare one in the same category.

Plain interpolated calls such as `logger.LogInformation("Order {OrderId}
placed", orderId)` leave the event id at zero and the event name unset, so the
identity collapses to the category alone. Every statement in that category then
shares a single callsite, and the sampler can no longer tell the flood apart
from the rare error inside it; sampling degenerates towards uniform within that
category. Use `[LoggerMessage]`, or set an explicit `EventId`, for any category
where that distinction matters.
