# OpenTelemetry Bottom-Floor Sampler

| Status | |
| ------ | --- |
| Stability | [Development](../../README.md#development) |
| Code Owners | [@jmacd](https://github.com/jmacd), [@Yun-Ting](https://github.com/Yun-Ting) |

This package provides fixed-memory sampling for callsite-labelled log streams.
It includes:

* `BottomFloorSampler<TCallsite>`, the sampling algorithm;
* `BottomFloorLogExporter`, which samples OpenTelemetry log export batches; and
* `BottomFloorLogRecordProcessor`, which combines batching and sampling in one
  processor.

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

## Per-span log coverage

`BottomFloorLogRecordProcessor` can also retain a bounded sample of logs for
each recording span. This is disabled by default. Set
`MaxLogsPerSpanPerWindow` to the maximum number of records retained per span to
enable it. While it is enabled, `Budget` no longer bounds the total number of
forwarded records, because span-only survivors are forwarded in addition to the
whole-stream sample.

```csharp
var options = new BottomFloorLogSamplerOptions
{
    Budget = 100,
    MaxLogsPerSpanPerWindow = 5,
};
```

Records selected by the per-span sample may contain
`otel.span_logs.adjusted_count`. A record selected only for span coverage has
`otel.logs.adjusted_count` set to zero so it does not contribute to the
whole-stream estimate. Per-span coverage groups records by the span id the SDK
already records on them, so it needs no cooperation from the application.

## Using the sampling algorithm directly

```csharp
using OpenTelemetry.Sampler.BottomFloor;

var sampler = new BottomFloorSampler<(string Category, int EventId)>(budget: 100);
var buffered = new Dictionary<long, MyEvent>();

foreach (var window in windows)
{
    foreach (var item in window)
    {
        var outcome = sampler.Offer((item.Category, item.EventId));
        if (!outcome.Admitted)
        {
            continue;
        }

        // Honour the eviction, so the buffer holds exactly the reservoir.
        if (outcome.Evicted)
        {
            buffered.Remove(outcome.EvictedToken);
        }

        buffered[outcome.Token] = item;
    }

    var summary = sampler.CloseWindow();
    foreach (var kept in summary.KeptItems)
    {
        var estimate = summary.Estimates[kept.Callsite];

        // The per-record adjusted count is 1 / inclusion probability. Summed
        // over a callsite's kept records it reproduces estimate.EstimatedCount,
        // that callsite's estimated arrival count for the window.
        Export(buffered[kept.Token], 1.0 / estimate.InclusionProbability);
    }

    // The next window starts from an empty reservoir, so nothing carries over.
    buffered.Clear();
}
```

`CloseWindow` both reports the window and starts the next one, so it must be
called on a regular boundary; the estimates and weights it derives are what let
the following window self-calibrate.

The reservoir holds `k + 1` entries while a window is open, because the extra
entry is the boundary that becomes the threshold. Size a caller-side buffer for
`Budget + 1`, not `Budget`.

`BottomFloorSampler<TCallsite>` is not thread-safe. Serialize calls to `Offer`
and `CloseWindow` when sampling a concurrent stream.
