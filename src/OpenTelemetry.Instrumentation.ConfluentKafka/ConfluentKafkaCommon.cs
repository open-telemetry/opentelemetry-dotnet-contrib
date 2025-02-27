// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal static class ConfluentKafkaCommon
{
    internal const string ReceiveOperationName = "receive";
    internal const string ProcessOperationName = "process";
    internal const string KafkaMessagingSystem = "kafka";
    internal const string PublishOperationName = "publish";

    internal static readonly string InstrumentationName = typeof(ConfluentKafkaCommon).Assembly.GetName().Name!;
    internal static readonly string InstrumentationVersion = typeof(ConfluentKafkaCommon).Assembly.GetPackageVersion();
    internal static readonly ActivitySource ConsumerActivitySource = new(InstrumentationName + ".Consumer");
    internal static readonly ActivitySource ProducerActivitySource = new(InstrumentationName + ".Producer");
    internal static readonly ActivitySource ProcessorActivitySource = new(InstrumentationName + ".Processor");
    internal static readonly Meter ProducerMeter = new(InstrumentationName + ".Producer", InstrumentationVersion);
    internal static readonly Meter ConsumerMeter = new(InstrumentationName + ".Consumer", InstrumentationVersion);
    internal static readonly Counter<long> ReceiveMessagesCounter = ConsumerMeter.CreateCounter<long>(
        SemanticConventions.MetricMessagingReceiveMessages,
        description: "Measures the number of received messages.");

    internal static readonly Histogram<double> ReceiveDurationHistogram = ConsumerMeter.CreateHistogram(
        SemanticConventions.MetricMessagingReceiveDuration,
        unit: "s",
        description: "Measures the duration of receive operation.",
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10] });

    internal static readonly Counter<long> PublishMessagesCounter = ProducerMeter.CreateCounter<long>(
        SemanticConventions.MetricMessagingPublishMessages,
        description: "Measures the number of published messages.");

    internal static readonly Histogram<double> PublishDurationHistogram = ProducerMeter.CreateHistogram(
        SemanticConventions.MetricMessagingPublishDuration,
        unit: "s",
        description: "Measures the duration of publish operation.",
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10] });
}
