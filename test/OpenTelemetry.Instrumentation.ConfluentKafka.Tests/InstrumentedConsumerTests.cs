// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

public class InstrumentedConsumerTests
{
    [Fact]
    public void Consume_CancellationToken_CreatesActivityWithCorrectTags()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = CreateTraceProvider(activities))
        {
            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumeResult = new ConsumeResult<string, string>
                {
                    Topic = "consume-topic",
                    Partition = new Partition(1),
                    Offset = new Offset(42),
                    Message = new Message<string, string> { Key = "msg-key", Value = "msg-value" },
                },
            };

            ConsumeEventAndCaptureTraces(fakeConsumer);

            tracerProvider.ForceFlush();
        }

        var activity = activities.Single(a => a.DisplayName == "poll consume-topic");
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal("https://opentelemetry.io/schemas/1.42.0", activity.Source.TelemetrySchemaUrl);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("poll", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationName));
        Assert.Equal("receive", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationType));
        Assert.Equal("consume-topic", activity.GetTagValue(SemanticConventions.AttributeMessagingDestinationName));
        Assert.Equal("1", activity.GetTagValue(SemanticConventions.AttributeMessagingDestinationPartitionId));
        Assert.Equal(42L, activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaOffset));
        Assert.Equal("test-group", activity.GetTagValue(SemanticConventions.AttributeMessagingConsumerGroupName));
        Assert.Equal("msg-key", activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaMessageKey));
    }

    [Fact]
    public void Consume_TracesDisabled_NoActivityCreated()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = CreateTraceProvider(activities))
        {
            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumeResult = new ConsumeResult<string, string>
                {
                    Topic = "disabled-traces-topic",
                    Partition = new Partition(0),
                    Offset = new Offset(1),
                    Message = new Message<string, string> { Value = "msg-value" },
                },
            };

            var options = new ConfluentKafkaConsumerInstrumentationOptions<string, string>
            {
                Traces = false,
                Metrics = false,
            };
            var instrumentedConsumer = new InstrumentedConsumer<string, string>(fakeConsumer, options);

            _ = instrumentedConsumer.Consume(CancellationToken.None);

            tracerProvider.ForceFlush();
        }

        Assert.DoesNotContain(activities, a => a.DisplayName == "poll disabled-traces-topic");
    }

    [Fact]
    public void Consume_PartitionEof_DoesNotCreateActivity()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = CreateTraceProvider(activities))
        {
            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumeResult = new ConsumeResult<string, string>
                {
                    Topic = "eof-topic",
                    Partition = new Partition(0),
                    Offset = Offset.End,
                    IsPartitionEOF = true,
                },
            };

            ConsumeEventAndCaptureTraces(fakeConsumer);

            tracerProvider.ForceFlush();
        }

        Assert.DoesNotContain(activities, a => a.DisplayName == "poll eof-topic");
    }

    [Fact]
    public void Consume_WithPropagatedTraceContext_LinksToProducerActivity()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = CreateTraceProvider(activities))
        {
            // A well-formed traceparent header representing a remote producer span
            const string traceparent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
            var headers = new Headers
            {
                { "traceparent", Encoding.UTF8.GetBytes(traceparent) },
            };

            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumeResult = new ConsumeResult<string, string>
                {
                    Topic = "linked-topic",
                    Partition = new Partition(0),
                    Offset = new Offset(10),
                    Message = new Message<string, string> { Value = "value", Headers = headers },
                },
            };

            ConsumeEventAndCaptureTraces(fakeConsumer);

            tracerProvider.ForceFlush();
        }

        var snapshot = activities.ToList();
        var activity = snapshot.Single(a => a.DisplayName == "poll linked-topic");

        // The extracted producer context should be linked (not parented) per messaging conventions
        Assert.NotEmpty(activity.Links);
        Assert.Equal(
            "0af7651916cd43dd8448eb211c80319c",
            activity.Links.First().Context.TraceId.ToHexString());
    }

    [Fact]
    public void Consume_ConsumeException_WithNullConsumerRecord_CreatesActivityWithError()
    {
        var activities = new List<Activity>();

        var error = new Error(ErrorCode.Local_ValueDeserialization, "Deserialization error");
        ConsumeResult<byte[], byte[]>? nullConsumerRecord = null;
        var exception = new ConsumeException(nullConsumerRecord, error);

        using (var tracerProvider = CreateTraceProvider(activities))
        {
            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumerExceptionToThrow = exception,
            };

            Assert.Throws<ConsumeException>(() => ConsumeEventAndCaptureTraces(fakeConsumer));

            tracerProvider.ForceFlush();
        }

        var activity = activities.FirstOrDefault(a => a.DisplayName == "poll");
        Assert.NotNull(activity);
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("fake-consumer-1", activity.GetTagValue(SemanticConventions.AttributeMessagingClientId));
        Assert.Equal("test-group", activity.GetTagValue(SemanticConventions.AttributeMessagingConsumerGroupName));
        Assert.Equal("poll", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationName));
        Assert.Equal("receive", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationType));
        Assert.Equal(exception.Error.Code.ToString(), activity.GetTagValue(SemanticConventions.AttributeErrorType));
        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeMessagingDestinationName));
        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeMessagingDestinationPartitionId));
        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaOffset));
        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaMessageKey));
    }

    [Fact]
    public void Consume_ConsumeException_WithNullMessage_CreatesActivityWithError()
    {
        var activities = new List<Activity>();

        var consumerRecord = new ConsumeResult<byte[], byte[]>
        {
            Topic = "error-topic",
            Partition = new Partition(2),
            Offset = new Offset(100),
            Message = null,
        };

        var error = new Error(ErrorCode.Local_KeyDeserialization, "Key deserialization error");
        var exception = new ConsumeException(consumerRecord, error);

        using (var tracerProvider = CreateTraceProvider(activities))
        {
            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumerExceptionToThrow = exception,
            };

            Assert.Throws<ConsumeException>(() => ConsumeEventAndCaptureTraces(fakeConsumer));

            tracerProvider.ForceFlush();
        }

        var activity = activities.FirstOrDefault(a => a.DisplayName == "poll error-topic");
        Assert.NotNull(activity);
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("fake-consumer-1", activity.GetTagValue(SemanticConventions.AttributeMessagingClientId));
        Assert.Equal("test-group", activity.GetTagValue(SemanticConventions.AttributeMessagingConsumerGroupName));
        Assert.Equal("poll", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationName));
        Assert.Equal("receive", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationType));
        Assert.Equal(exception.Error.Code.ToString(), activity.GetTagValue(SemanticConventions.AttributeErrorType));
        Assert.Equal("error-topic", activity.GetTagValue(SemanticConventions.AttributeMessagingDestinationName));
        Assert.Equal("2", activity.GetTagValue(SemanticConventions.AttributeMessagingDestinationPartitionId));
        Assert.Equal(100L, activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaOffset));
        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaMessageKey));
    }

    [Fact]
    public void Consume_ConsumeException_WithValidMessageWithoutHeaders_CreatesActivityWithError()
    {
        var activities = new List<Activity>();

        var consumerRecord = new ConsumeResult<byte[], byte[]>
        {
            Topic = "error-topic-no-headers",
            Partition = new Partition(3),
            Offset = new Offset(150),
            Message = new Message<byte[], byte[]>
            {
                Key = Encoding.UTF8.GetBytes("error-key"),
                Value = Encoding.UTF8.GetBytes("error-value"),
            },
        };

        var error = new Error(ErrorCode.Local_AllBrokersDown, "All brokers down");
        var exception = new ConsumeException(consumerRecord, error);

        using (var tracerProvider = CreateTraceProvider(activities))
        {
            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumerExceptionToThrow = exception,
            };

            Assert.Throws<ConsumeException>(() => ConsumeEventAndCaptureTraces(fakeConsumer));

            tracerProvider.ForceFlush();
        }

        var activity = activities.FirstOrDefault(a => a.DisplayName == "poll error-topic-no-headers");
        Assert.NotNull(activity);
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("fake-consumer-1", activity.GetTagValue(SemanticConventions.AttributeMessagingClientId));
        Assert.Equal("test-group", activity.GetTagValue(SemanticConventions.AttributeMessagingConsumerGroupName));
        Assert.Equal("poll", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationName));
        Assert.Equal("receive", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationType));
        Assert.Equal(exception.Error.Code.ToString(), activity.GetTagValue(SemanticConventions.AttributeErrorType));
        Assert.Equal("error-topic-no-headers", activity.GetTagValue(SemanticConventions.AttributeMessagingDestinationName));
        Assert.Equal("3", activity.GetTagValue(SemanticConventions.AttributeMessagingDestinationPartitionId));
        Assert.Equal(150L, activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaOffset));
        Assert.Equal("error-key", Encoding.UTF8.GetString((byte[])activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaMessageKey)!));
    }

    [Fact]
    public void Consume_ConsumeException_WithValidMessageWithHeaders_CreatesActivityWithError()
    {
        var activities = new List<Activity>();

        // A well-formed traceparent header representing a remote producer span
        const string traceparent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
        var headers = new Headers
        {
            { "traceparent", Encoding.UTF8.GetBytes(traceparent) },
        };

        var consumerRecord = new ConsumeResult<byte[], byte[]>
        {
            Topic = "error-topic-with-headers",
            Partition = new Partition(5),
            Offset = new Offset(200),
            Message = new Message<byte[], byte[]>
            {
                Key = Encoding.UTF8.GetBytes("error-key-with-headers"),
                Value = Encoding.UTF8.GetBytes("error-value-with-headers"),
                Headers = headers,
            },
        };

        var error = new Error(ErrorCode.BrokerNotAvailable, "Broker not available");
        var exception = new ConsumeException(consumerRecord, error);

        using (var tracerProvider = CreateTraceProvider(activities))
        {
            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumerExceptionToThrow = exception,
            };

            Assert.Throws<ConsumeException>(() => ConsumeEventAndCaptureTraces(fakeConsumer));

            tracerProvider.ForceFlush();
        }

        var activity = activities.FirstOrDefault(a => a.DisplayName == "poll error-topic-with-headers");
        Assert.NotNull(activity);
        Assert.NotEmpty(activity.Links);
        Assert.Equal("0af7651916cd43dd8448eb211c80319c", activity.Links.First().Context.TraceId.ToHexString());
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("fake-consumer-1", activity.GetTagValue(SemanticConventions.AttributeMessagingClientId));
        Assert.Equal("test-group", activity.GetTagValue(SemanticConventions.AttributeMessagingConsumerGroupName));
        Assert.Equal("poll", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationName));
        Assert.Equal("receive", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationType));
        Assert.Equal(exception.Error.Code.ToString(), activity.GetTagValue(SemanticConventions.AttributeErrorType));
        Assert.Equal("error-topic-with-headers", activity.GetTagValue(SemanticConventions.AttributeMessagingDestinationName));
        Assert.Equal("5", activity.GetTagValue(SemanticConventions.AttributeMessagingDestinationPartitionId));
        Assert.Equal(200L, activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaOffset));
        Assert.Equal("error-key-with-headers", Encoding.UTF8.GetString((byte[])activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaMessageKey)!));
    }

    [Fact]
    public void Consume_TimeSpan_ConsumeException_CreatesActivityWithError()
    {
        var activities = new List<Activity>();

        var consumerRecord = new ConsumeResult<byte[], byte[]>
        {
            Topic = "timeout-topic",
            Partition = new Partition(0),
            Offset = new Offset(50),
            Message = new Message<byte[], byte[]>
            {
                Value = Encoding.UTF8.GetBytes("timeout-value"),
            },
        };

        var error = new Error(ErrorCode.Local_TimedOut, "Operation timed out");
        var exception = new ConsumeException(consumerRecord, error);

        using (var tracerProvider = CreateTraceProvider(activities))
        {
            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumerExceptionToThrow = exception,
            };

            var options = new ConfluentKafkaConsumerInstrumentationOptions<string, string>
            {
                Traces = true,
                Metrics = false,
            };
            var instrumentedConsumer = new InstrumentedConsumer<string, string>(fakeConsumer, options);

            Assert.Throws<ConsumeException>(() => instrumentedConsumer.Consume(TimeSpan.FromSeconds(1)));

            tracerProvider.ForceFlush();
        }

        var activity = activities.FirstOrDefault(a => a.DisplayName == "poll timeout-topic");
        Assert.NotNull(activity);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(exception.Error.Code.ToString(), activity.GetTagValue(SemanticConventions.AttributeErrorType));
    }

    [Fact]
    public void Consume_Milliseconds_ConsumeException_CreatesActivityWithError()
    {
        var activities = new List<Activity>();

        var consumerRecord = new ConsumeResult<byte[], byte[]>
        {
            Topic = "broker-error-topic",
            Partition = new Partition(1),
            Offset = new Offset(75),
            Message = new Message<byte[], byte[]>
            {
                Value = Encoding.UTF8.GetBytes("broker-error-value"),
            },
        };

        var error = new Error(ErrorCode.Local_Transport, "Transport error");
        var exception = new ConsumeException(consumerRecord, error);

        using (var tracerProvider = CreateTraceProvider(activities))
        {
            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumerExceptionToThrow = exception,
            };

            var options = new ConfluentKafkaConsumerInstrumentationOptions<string, string>
            {
                Traces = true,
                Metrics = false,
            };
            var instrumentedConsumer = new InstrumentedConsumer<string, string>(fakeConsumer, options);

            Assert.Throws<ConsumeException>(() => instrumentedConsumer.Consume(1000));

            tracerProvider.ForceFlush();
        }

        var activity = activities.FirstOrDefault(a => a.DisplayName == "poll broker-error-topic");
        Assert.NotNull(activity);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(exception.Error.Code.ToString(), activity.GetTagValue(SemanticConventions.AttributeErrorType));
    }

    [Fact]
    public void Consume_ConsumeException_WithNullConsumerRecord_RecordsMetricsWithError()
    {
        var metrics = new List<Metric>();

        var error = new Error(ErrorCode.Local_ValueDeserialization, "Deserialization error");
        ConsumeResult<byte[], byte[]>? nullConsumerRecord = null;
        var exception = new ConsumeException(nullConsumerRecord, error);

        using (var meterProvider = CreateMeterProvider(metrics))
        {
            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumerExceptionToThrow = exception,
            };

            Assert.Throws<ConsumeException>(() => ConsumeEventAndCaptureMetrics(fakeConsumer));

            meterProvider.EnsureMetricsAreFlushed();
        }

        var receiveMessagesMetric = metrics.FirstOrDefault(m => m.Name == SemanticConventions.MetricMessagingClientConsumedMessages);
        AssertMetric(
            actualMetric: receiveMessagesMetric,
            expectedMessagingOperation: ConfluentKafkaCommon.PollOperationName,
            expectedMessagingSystem: ConfluentKafkaCommon.KafkaMessagingSystem,
            expectedKafkaDestinationName: null,
            expectedKafkaDestinationPartition: null,
            expectedErrorType: exception.Error.Code.ToString());

        var receiveDurationMetric = metrics.FirstOrDefault(m => m.Name == SemanticConventions.MetricMessagingClientOperationDuration);
        AssertMetric(
            actualMetric: receiveDurationMetric,
            expectedMessagingOperation: ConfluentKafkaCommon.PollOperationName,
            expectedMessagingSystem: ConfluentKafkaCommon.KafkaMessagingSystem,
            expectedKafkaDestinationName: null,
            expectedKafkaDestinationPartition: null,
            expectedErrorType: exception.Error.Code.ToString());
    }

    [Fact]
    public void Consume_ConsumeException_WithNullMessage_RecordsMetricsWithError()
    {
        var metrics = new List<Metric>();

        var consumerRecord = new ConsumeResult<byte[], byte[]>
        {
            Topic = "error-topic",
            Partition = new Partition(2),
            Offset = new Offset(100),
            Message = null,
        };

        var error = new Error(ErrorCode.Local_KeyDeserialization, "Key Deserialization error");
        var exception = new ConsumeException(consumerRecord, error);

        using (var meterProvider = CreateMeterProvider(metrics))
        {
            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumerExceptionToThrow = exception,
            };

            Assert.Throws<ConsumeException>(() => ConsumeEventAndCaptureMetrics(fakeConsumer));

            meterProvider.EnsureMetricsAreFlushed();
        }

        var receiveMessagesMetric = metrics.FirstOrDefault(m => m.Name == SemanticConventions.MetricMessagingClientConsumedMessages);
        AssertMetric(
            actualMetric: receiveMessagesMetric,
            expectedMessagingOperation: ConfluentKafkaCommon.PollOperationName,
            expectedMessagingSystem: ConfluentKafkaCommon.KafkaMessagingSystem,
            expectedKafkaDestinationName: "error-topic",
            expectedKafkaDestinationPartition: "2",
            expectedErrorType: exception.Error.Code.ToString());

        var receiveDurationMetric = metrics.FirstOrDefault(m => m.Name == SemanticConventions.MetricMessagingClientOperationDuration);
        AssertMetric(
            actualMetric: receiveDurationMetric,
            expectedMessagingOperation: ConfluentKafkaCommon.PollOperationName,
            expectedMessagingSystem: ConfluentKafkaCommon.KafkaMessagingSystem,
            expectedKafkaDestinationName: "error-topic",
            expectedKafkaDestinationPartition: "2",
            expectedErrorType: exception.Error.Code.ToString());
    }

    [Fact]
    public void Consume_ConsumeException_WithValidMessageWithoutHeaders_RecordsMetricsWithError()
    {
        var metrics = new List<Metric>();

        var consumerRecord = new ConsumeResult<byte[], byte[]>
        {
            Topic = "error-topic-no-headers",
            Partition = new Partition(3),
            Offset = new Offset(150),
            Message = new Message<byte[], byte[]>
            {
                Key = Encoding.UTF8.GetBytes("error-key"),
                Value = Encoding.UTF8.GetBytes("error-value"),
            },
        };

        var error = new Error(ErrorCode.Local_AllBrokersDown, "All brokers down");
        var exception = new ConsumeException(consumerRecord, error);

        using (var meterProvider = CreateMeterProvider(metrics))
        {
            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumerExceptionToThrow = exception,
            };

            Assert.Throws<ConsumeException>(() => ConsumeEventAndCaptureMetrics(fakeConsumer));

            meterProvider.EnsureMetricsAreFlushed();
        }

        var receiveMessagesMetric = metrics.FirstOrDefault(m => m.Name == SemanticConventions.MetricMessagingClientConsumedMessages);
        AssertMetric(
            actualMetric: receiveMessagesMetric,
            expectedMessagingOperation: ConfluentKafkaCommon.PollOperationName,
            expectedMessagingSystem: ConfluentKafkaCommon.KafkaMessagingSystem,
            expectedKafkaDestinationName: "error-topic-no-headers",
            expectedKafkaDestinationPartition: "3",
            expectedErrorType: exception.Error.Code.ToString());

        var receiveDurationMetric = metrics.FirstOrDefault(m => m.Name == SemanticConventions.MetricMessagingClientOperationDuration);
        AssertMetric(
            actualMetric: receiveDurationMetric,
            expectedMessagingOperation: ConfluentKafkaCommon.PollOperationName,
            expectedMessagingSystem: ConfluentKafkaCommon.KafkaMessagingSystem,
            expectedKafkaDestinationName: "error-topic-no-headers",
            expectedKafkaDestinationPartition: "3",
            expectedErrorType: exception.Error.Code.ToString());
    }

    [Fact]
    public void Consume_ConsumeException_WithValidMessageWithHeaders_RecordsMetricsWithError()
    {
        var metrics = new List<Metric>();

        var headers = new Headers
        {
            { "test-header", Encoding.UTF8.GetBytes("test-value") },
            { "another-header", Encoding.UTF8.GetBytes("another-value") },
        };

        var consumerRecord = new ConsumeResult<byte[], byte[]>
        {
            Topic = "error-topic-with-headers",
            Partition = new Partition(5),
            Offset = new Offset(200),
            Message = new Message<byte[], byte[]>
            {
                Key = Encoding.UTF8.GetBytes("error-key-with-headers"),
                Value = Encoding.UTF8.GetBytes("error-value-with-headers"),
                Headers = headers,
            },
        };

        var error = new Error(ErrorCode.BrokerNotAvailable, "Broker not available");
        var exception = new ConsumeException(consumerRecord, error);

        using (var meterProvider = CreateMeterProvider(metrics))
        {
            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumerExceptionToThrow = exception,
            };

            Assert.Throws<ConsumeException>(() => ConsumeEventAndCaptureMetrics(fakeConsumer));

            meterProvider.EnsureMetricsAreFlushed();
        }

        var receiveMessagesMetric = metrics.FirstOrDefault(m => m.Name == SemanticConventions.MetricMessagingClientConsumedMessages);
        AssertMetric(
            actualMetric: receiveMessagesMetric,
            expectedMessagingOperation: ConfluentKafkaCommon.PollOperationName,
            expectedMessagingSystem: ConfluentKafkaCommon.KafkaMessagingSystem,
            expectedKafkaDestinationName: "error-topic-with-headers",
            expectedKafkaDestinationPartition: "5",
            expectedErrorType: exception.Error.Code.ToString());

        var receiveDurationMetric = metrics.FirstOrDefault(m => m.Name == SemanticConventions.MetricMessagingClientOperationDuration);
        AssertMetric(
            actualMetric: receiveDurationMetric,
            expectedMessagingOperation: ConfluentKafkaCommon.PollOperationName,
            expectedMessagingSystem: ConfluentKafkaCommon.KafkaMessagingSystem,
            expectedKafkaDestinationName: "error-topic-with-headers",
            expectedKafkaDestinationPartition: "5",
            expectedErrorType: exception.Error.Code.ToString());
    }

    private static TracerProvider CreateTraceProvider(List<Activity> activities) => Sdk.CreateTracerProviderBuilder()
        .AddSource(ConfluentKafkaCommon.ActivitySource.Name)
        .AddInMemoryExporter(activities)
        .Build();

    private static MeterProvider CreateMeterProvider(List<Metric> metrics) => Sdk.CreateMeterProviderBuilder()
        .AddMeter(ConfluentKafkaCommon.Meter.Name)
        .AddInMemoryExporter(metrics)
        .Build();

    private static void ConsumeEventAndCaptureTraces(IConsumer<string, string> fakeConsumer)
    {
        var options = new ConfluentKafkaConsumerInstrumentationOptions<string, string>
        {
            Traces = true,
            Metrics = false,
        };

        var instrumentedConsumer = new InstrumentedConsumer<string, string>(fakeConsumer, options)
        {
            GroupId = "test-group",
        };

        _ = instrumentedConsumer.Consume(CancellationToken.None);
    }

    private static void ConsumeEventAndCaptureMetrics(IConsumer<string, string> fakeConsumer)
    {
        var options = new ConfluentKafkaConsumerInstrumentationOptions<string, string>
        {
            Traces = false,
            Metrics = true,
        };

        var instrumentedConsumer = new InstrumentedConsumer<string, string>(fakeConsumer, options)
        {
            GroupId = "test-group",
        };

        _ = instrumentedConsumer.Consume(CancellationToken.None);
    }

    private static void AssertMetric(
       Metric? actualMetric,
       string? expectedMessagingOperation,
       string? expectedMessagingSystem,
       string? expectedKafkaDestinationName,
       string? expectedKafkaDestinationPartition,
       string? expectedErrorType)
    {
        Assert.NotNull(actualMetric);
        Assert.StartsWith("https://opentelemetry.io/schemas/", actualMetric.MeterSchemaUrl, StringComparison.Ordinal);

        var metricPoint = GetMetricPoint(actualMetric);

        var messagingOperationFound = false;
        var messagingSystemFound = false;
        var destinationNameFound = false;
        var destinationPartitionFound = false;
        var errorTypeFound = false;

        foreach (var tag in metricPoint!.Value.Tags)
        {
            if (tag.Key == SemanticConventions.AttributeMessagingOperationName)
            {
                Assert.Equal(expectedMessagingOperation, tag.Value?.ToString());
                messagingOperationFound = true;
            }

            if (tag.Key == SemanticConventions.AttributeMessagingSystem)
            {
                Assert.Equal(expectedMessagingSystem, tag.Value?.ToString());
                messagingSystemFound = true;
            }

            if (tag.Key == SemanticConventions.AttributeMessagingDestinationName)
            {
                Assert.Equal(expectedKafkaDestinationName, tag.Value?.ToString());
                destinationNameFound = true;
            }

            if (tag.Key == SemanticConventions.AttributeMessagingDestinationPartitionId)
            {
                Assert.Equal(expectedKafkaDestinationPartition, tag.Value?.ToString());
                destinationPartitionFound = true;
            }

            if (tag.Key == SemanticConventions.AttributeErrorType)
            {
                Assert.Equal(expectedErrorType, tag.Value?.ToString());
                errorTypeFound = true;
            }
        }

        static void AssertTagExists(bool tagFound, object? tagExpectedValue)
        {
            if (tagExpectedValue is null)
            {
                Assert.False(tagFound);
            }
            else
            {
                Assert.True(tagFound);
            }
        }

        AssertTagExists(messagingOperationFound, expectedMessagingOperation);
        AssertTagExists(messagingSystemFound, expectedMessagingSystem);
        AssertTagExists(destinationNameFound, expectedKafkaDestinationName);
        AssertTagExists(destinationPartitionFound, expectedKafkaDestinationPartition);
        AssertTagExists(errorTypeFound, expectedErrorType);
    }

    private static MetricPoint? GetMetricPoint(Metric? metric)
    {
        if (metric == null)
        {
            return null;
        }

        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            return metricPoint;
        }

        return null;
    }

    private sealed class FakeConsumer<TKey, TValue> : IConsumer<TKey, TValue>
    {
        public ConsumeResult<TKey, TValue>? ConsumeResult { get; set; }

        public ConsumeException? ConsumerExceptionToThrow { get; set; }

        public Handle Handle => null!;

        public string Name => "fake-consumer-1";

        public string MemberId => string.Empty;

        public List<TopicPartition> Assignment => [];

        public List<string> Subscription => [];

        public IConsumerGroupMetadata ConsumerGroupMetadata => null!;

        public int AddBrokers(string brokers) => 0;

        public void SetSaslCredentials(string username, string password)
        {
            // No-op
        }

        public ConsumeResult<TKey, TValue>? Consume(int millisecondsTimeout)
            => this.ConsumerExceptionToThrow != null ? throw this.ConsumerExceptionToThrow : this.ConsumeResult;

        public ConsumeResult<TKey, TValue>? Consume(CancellationToken cancellationToken = default)
            => this.ConsumerExceptionToThrow != null ? throw this.ConsumerExceptionToThrow : this.ConsumeResult;

        public ConsumeResult<TKey, TValue>? Consume(TimeSpan timeout)
            => this.ConsumerExceptionToThrow != null ? throw this.ConsumerExceptionToThrow : this.ConsumeResult;

        public void Subscribe(IEnumerable<string> topics)
        {
            // No-op
        }

        public void Subscribe(string topic)
        {
            // No-op
        }

        public void Unsubscribe()
        {
            // No-op
        }

        public void Assign(TopicPartition partition)
        {
            // No-op
        }

        public void Assign(TopicPartitionOffset partition)
        {
            // No-op
        }

        public void Assign(IEnumerable<TopicPartitionOffset> partitions)
        {
            // No-op
        }

        public void Assign(IEnumerable<TopicPartition> partitions)
        {
            // No-op
        }

        public void IncrementalAssign(IEnumerable<TopicPartitionOffset> partitions)
        {
            // No-op
        }

        public void IncrementalAssign(IEnumerable<TopicPartition> partitions)
        {
            // No-op
        }

        public void IncrementalUnassign(IEnumerable<TopicPartition> partitions)
        {
            // No-op
        }

        public void Unassign()
        {
            // No-op
        }

        public void StoreOffset(ConsumeResult<TKey, TValue> result)
        {
            // No-op
        }

        public void StoreOffset(TopicPartitionOffset offset)
        {
            // No-op
        }

        public List<TopicPartitionOffset> Commit() => [];

        public void Commit(IEnumerable<TopicPartitionOffset> offsets)
        {
            // No-op
        }

        public void Commit(ConsumeResult<TKey, TValue> result)
        {
            // No-op
        }

        public void Seek(TopicPartitionOffset tpo)
        {
            // No-op
        }

        public void Pause(IEnumerable<TopicPartition> partitions)
        {
            // No-op
        }

        public void Resume(IEnumerable<TopicPartition> partitions)
        {
            // No-op
        }

        public List<TopicPartitionOffset> Committed(TimeSpan timeout) => [];

        public List<TopicPartitionOffset> Committed(IEnumerable<TopicPartition> partitions, TimeSpan timeout) => [];

        public Offset Position(TopicPartition partition) => Offset.Unset;

        public List<TopicPartitionOffset> OffsetsForTimes(IEnumerable<TopicPartitionTimestamp> timestampsToSearch, TimeSpan timeout) => [];

        public WatermarkOffsets GetWatermarkOffsets(TopicPartition topicPartition) => new(Offset.Unset, Offset.Unset);

        public WatermarkOffsets QueryWatermarkOffsets(TopicPartition topicPartition, TimeSpan timeout) => new(Offset.Unset, Offset.Unset);

        public void Close()
        {
            // No-op
        }

        public void Dispose()
        {
            // No-op
        }
    }
}
