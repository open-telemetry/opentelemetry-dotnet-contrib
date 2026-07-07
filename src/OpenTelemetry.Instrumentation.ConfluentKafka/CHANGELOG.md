# Changelog

## Unreleased

* Updated OpenTelemetry core component version(s) to `1.16.0`.
  ([#4487](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4487))

* Supported manual configuration of traces and metrics for Kafka consumers
  and producers created outside a DI container.
  ([#4545](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4545))

* Assemblies are now digitally signed using cosign.
  ([#4637](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4637))

* **Breaking change**: Updated the traces and metrics emitted by the Kafka
  producer and consumer to follow the
  [v1.43.0 messaging semantic conventions](https://github.com/open-telemetry/semantic-conventions/tree/v1.43.0/docs/messaging).
  The producer `send` span (previously `publish`) and the consumer `poll`
  span (previously `receive`, and now emitted with `ActivityKind.Client`) now
  set:

  * `messaging.operation.name` and `messaging.operation.type`
    (replacing `messaging.operation`)
  * `messaging.client.id` (replacing `messaging.client_id`)
  * `messaging.consumer.group.name` (replacing
    `messaging.kafka.consumer.group`)
  * `messaging.destination.partition.id` (a string) (replacing
    `messaging.kafka.destination.partition`)
  * `messaging.kafka.offset` (replacing `messaging.kafka.message.offset`)

  `messaging.kafka.message.key` is now emitted as a string when the key has an
  unambiguous canonical representation; otherwise, the attribute is omitted.

  The metrics were renamed to:

  * `messaging.client.operation.duration`
    (replacing `messaging.publish.duration` and `messaging.receive.duration`)
  * `messaging.client.sent.messages` (replacing `messaging.publish.messages`)
  * `messaging.client.consumed.messages`
    (replacing `messaging.receive.messages`).

  `error.type` is now reported as the language-agnostic Kafka error code for
  Kafka errors (`ProduceException`/`ConsumeException`), or the exception type
  name for other failures (such as an `ArgumentException` from the producer or
  an exception thrown by a message handler).
  ([#4636](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4636))

* Metrics and traces now include a telemetry schema URL.
  ([#4636](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4636))

## 0.1.0-alpha.7

Released 2026-May-29

* Fixed an issue in `InstrumentedConsumer` where `messaging.receive.duration`
  and `messaging.receive.messages` metrics were not emitted when `ConsumeException`
  is thrown by the `Consume` method.
  ([#4433](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4433))

## 0.1.0-alpha.6

Released 2026-Apr-21

* Updated OpenTelemetry core component version(s) to `1.15.3`.
  ([#4166](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4166))

## 0.1.0-alpha.5

Released 2026-Jan-21

* Updated OpenTelemetry core component version(s) to `1.15.0`.
  ([#3721](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/3721))

## 0.1.0-alpha.4

Released 2025-Nov-13

* Add support for .NET 10.0.
  ([#2822](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2822))

* Update .NET 10.0 NuGet package versions from `10.0.0-rc.2.25502.107` to `10.0.0`.
  ([#3403](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/3403))

* Updated OpenTelemetry core component version(s) to `1.14.0`.
  ([#3403](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/3403))

## 0.1.0-alpha.3

Released 2025-Oct-23

* Drop support for .NET 6 as this target is no longer supported
  and add .NET Standard 2.0 target.
  ([#2142](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2142))

* Trace instrumentation will now call the [Activity.SetStatus](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity.setstatus)
  API instead of the deprecated OpenTelemetry API package extension when setting
  span status. For details see: [Setting Status](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Api/README.md#setting-status).
  ([#2358](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2358))

* The `messaging.receive.duration` and `messaging.publish.duration` histograms
  (measured in seconds) produced by the metrics instrumentation in this package
  now uses the [Advice API](https://github.com/open-telemetry/opentelemetry-dotnet/blob/core-1.10.0/docs/metrics/customizing-the-sdk/README.md#explicit-bucket-histogram-aggregation)
  to set default explicit buckets following the [OpenTelemetry Specification](https://github.com/open-telemetry/semantic-conventions/blob/v1.24.0/docs/messaging/messaging-metrics.md).
  ([#2430](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2430))

* Rethrow exception on consume and process.
  ([#2847](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2847))

* Updated OpenTelemetry core component version(s) to `1.13.1`.
  ([#3218](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/3218))

## 0.1.0-alpha.2

Released 2024-Sep-18

* Add named instrumentation support
  ([#2074](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/2074))

## 0.1.0-alpha.1

Released 2024-Sep-16

* Initial release
