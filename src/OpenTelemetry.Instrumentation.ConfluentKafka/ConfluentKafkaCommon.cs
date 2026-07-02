// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

/// <summary>
/// Contains common constants and static members used by the Confluent Kafka instrumentation.
/// </summary>
/// <remarks>
/// Follows the v1.42.0 messaging semantic conventions:
/// https://github.com/open-telemetry/semantic-conventions/tree/v1.42.0/docs/messaging.
/// </remarks>
internal static class ConfluentKafkaCommon
{
    internal const string KafkaMessagingSystem = "kafka";

    // messaging.operation.name values (system-specific operation names).
    internal const string SendOperationName = "send";
    internal const string PollOperationName = "poll";
    internal const string ProcessOperationName = "process";

    // messaging.operation.type values.
    internal const string SendOperationType = "send";
    internal const string ReceiveOperationType = "receive";
    internal const string ProcessOperationType = "process";

    internal static readonly Version SemanticConventionsVersion = new(1, 42, 0);

    internal static readonly ActivitySource ActivitySource = ActivitySourceFactory.Create(typeof(ConfluentKafkaCommon), SemanticConventionsVersion);
    internal static readonly Meter Meter = MeterFactory.Create(typeof(ConfluentKafkaCommon), SemanticConventionsVersion);

    internal static readonly Histogram<double> OperationDurationHistogram = Meter.CreateHistogram(
        SemanticConventions.MetricMessagingClientOperationDuration,
        unit: "s",
        description: "Duration of messaging operation initiated by a producer or consumer client.",
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10] });

    internal static readonly Counter<long> SentMessagesCounter = Meter.CreateCounter<long>(
        SemanticConventions.MetricMessagingClientSentMessages,
        unit: "{message}",
        description: "Number of messages producer attempted to send to the broker.");

    internal static readonly Counter<long> ConsumedMessagesCounter = Meter.CreateCounter<long>(
        SemanticConventions.MetricMessagingClientConsumedMessages,
        unit: "{message}",
        description: "Number of messages that were delivered to the application.");
}
