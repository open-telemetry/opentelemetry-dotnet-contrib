# Confluent.Kafka client instrumentation for OpenTelemetry

| Status | |
| ------ | --- |
| Stability | [Development](../../README.md#development) |
| Code Owners | [@g7ed6e](https://github.com/g7ed6e) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.ConfluentKafka)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.ConfluentKafka)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.ConfluentKafka)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.ConfluentKafka)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Instrumentation.ConfluentKafka)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Instrumentation.ConfluentKafka)

## Usage

To use the `OpenTelemetry.Instrumentation.ConfluentKafka` package, follow these
steps:

1. **Install the package**:

    ```shell
    dotnet add package OpenTelemetry.Instrumentation.ConfluentKafka --prerelease
    ```

2. **Configure OpenTelemetry in your application**:

    ```csharp
    using Confluent.Kafka;
    using OpenTelemetry.Metrics;
    using OpenTelemetry.Trace;

    var builder = Host.CreateApplicationBuilder(args);

    const string bootstrapServers = "localhost:9092";

    builder.Services.AddSingleton(_ =>
    {
        ProducerConfig producerConfig = new() { BootstrapServers = bootstrapServers };
        return new InstrumentedProducerBuilder<string, string>(producerConfig);
    });
    builder.Services.AddSingleton(_ =>
    {
        ConsumerConfig consumerConfigA = new()
        {
            BootstrapServers = bootstrapServers,
            GroupId = "group-a",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnablePartitionEof = true,
        };
        return new InstrumentedConsumerBuilder<string, string>(consumerConfigA);
    });

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing =>
        {
            tracing.AddConsoleExporter()
                .AddOtlpExporter()
                // AddKafkaProducerInstrumentation and AddKafkaConsumerInstrumentation
                // are what enable Kafka traces.
                .AddKafkaProducerInstrumentation<string, string>()
                .AddKafkaConsumerInstrumentation<string, string>();
        })
        .WithMetrics(metering =>
        {
            metering.AddConsoleExporter()
                .AddOtlpExporter()
                // AddKafkaProducerInstrumentation and AddKafkaConsumerInstrumentation
                // are what enable Kafka metrics.
                .AddKafkaProducerInstrumentation<string, string>()
                .AddKafkaConsumerInstrumentation<string, string>();
        });

    builder.Services.AddHostedService<ProduceConsumeHostedService>();

    var app = builder.Build();
    await app.RunAsync();
    ```

This will set up OpenTelemetry instrumentation for Confluent.Kafka producers
and consumers, allowing you to collect and export telemetry data.

## Metrics

The instrumentation is implemented based on the [messaging metrics semantic
conventions](https://github.com/open-telemetry/semantic-conventions/blob/v1.44.0/docs/messaging/messaging-metrics.md).
The following metrics are produced:

| Name | Instrument Type | Unit | Description | Attributes |
| --- | --- | --- | --- | --- |
| `messaging.client.operation.duration` | Histogram | `s` | Duration of messaging operation initiated by a producer or consumer client. | `messaging.operation.name`, `messaging.operation.type`, `messaging.system`, `messaging.destination.name`[^1], `messaging.destination.partition.id`[^2], `messaging.consumer.group.name`[^3], `error.type`[^4] |
| `messaging.client.sent.messages` | Counter | `{message}` | Number of messages producer attempted to send to the broker. | `messaging.operation.name`, `messaging.operation.type`, `messaging.system`, `messaging.destination.name`, `messaging.destination.partition.id`[^2], `error.type`[^4] |
| `messaging.client.consumed.messages` | Counter | `{message}` | Number of messages that were delivered to the application. | `messaging.operation.name`, `messaging.operation.type`, `messaging.system`, `messaging.destination.name`[^1], `messaging.destination.partition.id`[^2], `messaging.consumer.group.name`[^3], `error.type`[^4] |

[^1]: `messaging.destination.name` is only included for consumer operations
  when the topic partition is known (for example, it is omitted after a
  `PartitionEOF` event that carries no topic partition).
[^2]: `messaging.destination.partition.id` is only included when the topic
  partition is known.
[^3]: `messaging.consumer.group.name` is only included for consumer
  operations, when a consumer group ID is configured.
[^4]: `error.type` is only included when an error occurs.

### Native producer client metrics

Native producer metrics are opt-in because librdkafka statistics generation and
JSON parsing have a runtime cost. Enable them through the producer builder
options, then register the instrumentation with a meter provider before building
the producer:

```csharp
var instrumentedBuilder = producerBuilder.AsInstrumentedProducerBuilder(
    new ConfluentKafkaInstrumentedProducerBuilderOptions
    {
        EnableClientMetrics = true,
        StatisticsInterval = TimeSpan.FromSeconds(10),
    });

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddKafkaProducerInstrumentation(instrumentedBuilder)
    // Add an exporter here.
    .Build();
using var producer = instrumentedBuilder.Build();
```

`EnableClientMetrics` defaults to `false` and is independent of `EnableMetrics`
(the existing application-operation metrics). `StatisticsInterval` defaults to
ten seconds and accepts one to `Int32.MaxValue` milliseconds. Fractional
milliseconds are truncated when configuring librdkafka. An explicitly configured
`statistics.interval.ms` always takes precedence, including `0`, which disables
statistics callbacks. An existing application statistics handler is preserved;
instrumentation and handler failures are reported through EventSource without
escaping into Kafka processing.

The following names follow the Java Kafka client instrumentation. All eight
metrics are **process aggregates across opted-in producers**, including producers
connected to different clusters. They have no client, topic, partition, or broker
attributes. Neither `client.id` nor librdkafka's generated handle name identifies
a metric series. Existing `messaging.client.*` metrics are unchanged.

| Name | Instrument type | Unit | librdkafka source |
| --- | --- | --- | --- |
| `kafka.producer.record_send_total` | Counter | `{record}` | Top-level `txmsgs` |
| `kafka.producer.byte_total` | Counter | `By` | Top-level `txmsg_bytes` |
| `kafka.producer.requests_in_flight` | Observable gauge | `{request}` | Sum of broker `waitresp_cnt` |
| `kafka.producer.buffer_total_bytes` | Observable gauge | `By` | Sum of producer `msg_size_max` |
| `kafka.producer.buffer_available_bytes` | Observable gauge | `By` | Sum of producer `max(0, msg_size_max - msg_size)` |
| `kafka.producer.record_queue_time_avg` | Observable gauge | `ms` | Sample-weighted broker `int_latency.sum / cnt`, converted from microseconds |
| `kafka.producer.produce_throttle_time_avg` | Observable gauge | `ms` | Sample-weighted broker `throttle.sum / cnt` (already milliseconds) |
| `kafka.producer.produce_throttle_time_max` | Observable gauge | `ms` | Maximum sampled broker `throttle.max` |

Transmitted records are native client transmissions, not application send
attempts or guaranteed successful deliveries. Transmitted message bytes include
message and batch framing, so they are not strictly equivalent to Java's record
byte measurements. Client totals are used directly rather than counting topic or
partition totals again. Bootstrap broker requests are included in process totals.

Each producer records deltas from its own cumulative counters; the first snapshot
contributes its total, and a decrease starts a new counter epoch. Duplicate
`client.id` values do not require special handling. Disposing a producer removes
its gauge observations without subtracting previously recorded counter values.
A callback already accepted before disposal may finish recording its counters.
Counter rates can be calculated by the metrics backend.

Gauges use each producer's latest valid statistics snapshot. Timing averages are
weighted by sample counts across brokers and producers, rather than averaging
averages; these statistics windows are not necessarily synchronized. Empty timing
windows (`cnt = 0`) produce no timing observation, while sampled zeroes are valid.
Unknown JSON fields are ignored. Missing or invalid fields are unavailable, not
zero; aggregates use the available contributions. Malformed JSON retains the
previous snapshot until a valid update or producer disposal.

Consumer, topic-, partition-, and broker-labelled metrics are deferred. Retry
metrics are also deferred because librdkafka reports request retries rather than
record retries. No rate gauges or per-client identity options are included.

## Runnable example

A complete end-to-end sample that produces and consumes messages with
instrumentation enabled is available in
[`examples/kafka`](../../examples/kafka). Follow that example's README to
start a local Kafka broker and see traces and metrics flowing to the
configured exporters.

## Extending `ConsumerBuilder` or `ProducerBuilder` instances

To extend an already built `ConsumerBuilder<TKey, TValue>`
or `ProducerBuilder<TKey, TValue>`
instance with OpenTelemetry instrumentation, you can use the
`AsInstrumentedConsumerBuilder`
and `AsInstrumentedProducerBuilder` extension methods.

> [!IMPORTANT]
> When you create dynamic producers or consumers outside a DI container,
> OpenTelemetry instrumentation (metrics and traces) is disabled by default.
> You must explicitly pass configuration options to enable it.
> If you do not use the standard DI registration methods (such as
`.AddKafkaProducerInstrumentation()`
> or `.AddKafkaConsumerInstrumentation()`), you must also manually call
`.AddSource("OpenTelemetry.Instrumentation.ConfluentKafka")` on your
> TracerProviderBuilder
> and `.AddMeter("OpenTelemetry.Instrumentation.ConfluentKafka")` on your
> MeterProviderBuilder so that the providers can listen to the emitted signals.

### Example for `ConsumerBuilder<TKey, TValue>`

```csharp
using Confluent.Kafka;
using OpenTelemetry.Instrumentation.ConfluentKafka;

var consumerConfig = new ConsumerConfig
{
    BootstrapServers = "localhost:9092",
    GroupId = "my-group",
    AutoOffsetReset = AutoOffsetReset.Earliest
};

var consumerBuilder = new ConsumerBuilder<string, string>(consumerConfig);

// Set various handlers and properties
consumerBuilder.SetErrorHandler((consumer, error) => Console.WriteLine($"Error: {error.Reason}"));
consumerBuilder.SetLogHandler((consumer, logMessage) => Console.WriteLine($"Log: {logMessage.Message}"));
consumerBuilder.SetStatisticsHandler((consumer, statistics) => Console.WriteLine($"Statistics: {statistics}"));

// Explicitly enable OpenTelemetry features for standalone usage
var telemetryOptions = new ConfluentKafkaInstrumentedConsumerBuilderOptions
{
    EnableTraces = true,
    EnableMetrics = true,
};

// Convert to InstrumentedConsumerBuilder with options
var instrumentedConsumerBuilder = consumerBuilder.AsInstrumentedConsumerBuilder(telemetryOptions);

// Build the consumer
var consumer = instrumentedConsumerBuilder.Build();
```

### Example for `ProducerBuilder<TKey, TValue>`

```csharp
using Confluent.Kafka;
using OpenTelemetry.Instrumentation.ConfluentKafka;

var producerConfig = new ProducerConfig
{
    BootstrapServers = "localhost:9092"
};

var producerBuilder = new ProducerBuilder<string, string>(producerConfig);

// Set various handlers and properties
producerBuilder.SetErrorHandler((producer, error) => Console.WriteLine($"Error: {error.Reason}"));
producerBuilder.SetLogHandler((producer, logMessage) => Console.WriteLine($"Log: {logMessage.Message}"));
producerBuilder.SetStatisticsHandler((producer, statistics) => Console.WriteLine($"Statistics: {statistics}"));

// Explicitly enable OpenTelemetry features for standalone usage
var telemetryOptions = new ConfluentKafkaInstrumentedProducerBuilderOptions
{
    EnableTraces = true,
    EnableMetrics = true,
};

// Convert to InstrumentedProducerBuilder with options
var instrumentedProducerBuilder = producerBuilder.AsInstrumentedProducerBuilder(telemetryOptions);

// Build the producer
var producer = instrumentedProducerBuilder.Build();
```
