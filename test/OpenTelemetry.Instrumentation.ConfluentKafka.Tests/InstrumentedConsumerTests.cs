// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

public class InstrumentedConsumerTests
{
    [Fact]
    public void Consume_CancellationToken_CreatesActivityWithCorrectTags()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(activities)
            .Build())
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

            tracerProvider.ForceFlush();
        }

        var activity = activities.Single(a => a.DisplayName == "consume-topic receive");
        Assert.Equal(ActivityKind.Consumer, activity.Kind);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("receive", activity.GetTagValue(SemanticConventions.AttributeMessagingOperation));
        Assert.Equal("consume-topic", activity.GetTagValue(SemanticConventions.AttributeMessagingDestinationName));
        Assert.Equal(42L, activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaMessageOffset));
        Assert.Equal("test-group", activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaConsumerGroup));
        Assert.Equal("msg-key", activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaMessageKey));
    }

    [Fact]
    public void Consume_TracesDisabled_NoActivityCreated()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(activities)
            .Build())
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

        Assert.DoesNotContain(activities, a => a.DisplayName == "disabled-traces-topic receive");
    }

    [Fact]
    public void Consume_PartitionEof_DoesNotCreateActivity()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(activities)
            .Build())
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

            var options = new ConfluentKafkaConsumerInstrumentationOptions<string, string>
            {
                Traces = true,
                Metrics = false,
            };
            var instrumentedConsumer = new InstrumentedConsumer<string, string>(fakeConsumer, options);

            _ = instrumentedConsumer.Consume(CancellationToken.None);

            tracerProvider.ForceFlush();
        }

        Assert.DoesNotContain(activities, a => a.DisplayName == "eof-topic receive");
    }

    [Fact]
    public void Consume_WithPropagatedTraceContext_LinksToProducerActivity()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(activities)
            .Build())
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

            var options = new ConfluentKafkaConsumerInstrumentationOptions<string, string>
            {
                Traces = true,
                Metrics = false,
            };
            var instrumentedConsumer = new InstrumentedConsumer<string, string>(fakeConsumer, options);

            _ = instrumentedConsumer.Consume(CancellationToken.None);

            tracerProvider.ForceFlush();
        }

        var snapshot = activities.ToList();
        var activity = snapshot.Single(a => a.DisplayName == "linked-topic receive");

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

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(activities)
            .Build())
        {
            var error = new Error(ErrorCode.Local_ValueDeserialization, "Deserialization error");
            ConsumeResult<byte[], byte[]>? nullConsumerRecord = null;
            var exception = new ConsumeException(nullConsumerRecord, error);

            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumerExceptionToThrow = exception,
            };

            var options = new ConfluentKafkaConsumerInstrumentationOptions<string, string>
            {
                Traces = true,
                Metrics = false,
            };
            var instrumentedConsumer = new InstrumentedConsumer<string, string>(fakeConsumer, options)
            {
                GroupId = "test-group",
            };

            Assert.Throws<ConsumeException>(() => instrumentedConsumer.Consume(CancellationToken.None));

            tracerProvider.ForceFlush();
        }

        var activity = activities.FirstOrDefault(a => a.DisplayName == "receive");
        Assert.NotNull(activity);
        Assert.Equal(ActivityKind.Consumer, activity.Kind);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("fake-consumer-1", activity.GetTagValue(SemanticConventions.AttributeMessagingClientId));
        Assert.Equal("test-group", activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaConsumerGroup));
        Assert.Equal("receive", activity.GetTagValue(SemanticConventions.AttributeMessagingOperation));
        Assert.Equal("ConsumeException: Deserialization error", activity.GetTagValue(SemanticConventions.AttributeErrorType));
        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeMessagingDestinationName));
        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaDestinationPartition));
        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaMessageOffset));
        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaMessageKey));
    }

    [Fact]
    public void Consume_ConsumeException_WithNullMessage_CreatesActivityWithError()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(activities)
            .Build())
        {
            var consumerRecord = new ConsumeResult<byte[], byte[]>
            {
                Topic = "error-topic",
                Partition = new Partition(2),
                Offset = new Offset(100),
                Message = null,
            };

            var error = new Error(ErrorCode.Local_KeyDeserialization, "Key deserialization error");
            var exception = new ConsumeException(consumerRecord, error);

            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumerExceptionToThrow = exception,
            };

            var options = new ConfluentKafkaConsumerInstrumentationOptions<string, string>
            {
                Traces = true,
                Metrics = false,
            };
            var instrumentedConsumer = new InstrumentedConsumer<string, string>(fakeConsumer, options)
            {
                GroupId = "test-group",
            };

            Assert.Throws<ConsumeException>(() => instrumentedConsumer.Consume(CancellationToken.None));

            tracerProvider.ForceFlush();
        }

        var activity = activities.FirstOrDefault(a => a.DisplayName == "error-topic receive");
        Assert.NotNull(activity);
        Assert.Equal(ActivityKind.Consumer, activity.Kind);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("fake-consumer-1", activity.GetTagValue(SemanticConventions.AttributeMessagingClientId));
        Assert.Equal("test-group", activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaConsumerGroup));
        Assert.Equal("receive", activity.GetTagValue(SemanticConventions.AttributeMessagingOperation));
        Assert.Equal("ConsumeException: Key deserialization error", activity.GetTagValue(SemanticConventions.AttributeErrorType));
        Assert.Equal("error-topic", activity.GetTagValue(SemanticConventions.AttributeMessagingDestinationName));
        Assert.Equal(2, activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaDestinationPartition));
        Assert.Equal(100L, activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaMessageOffset));
        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaMessageKey));
    }

    [Fact]
    public void Consume_ConsumeException_WithValidMessageWithoutHeaders_CreatesActivityWithError()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(activities)
            .Build())
        {
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

            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumerExceptionToThrow = exception,
            };

            var options = new ConfluentKafkaConsumerInstrumentationOptions<string, string>
            {
                Traces = true,
                Metrics = false,
            };
            var instrumentedConsumer = new InstrumentedConsumer<string, string>(fakeConsumer, options)
            {
                GroupId = "test-group",
            };

            Assert.Throws<ConsumeException>(() => instrumentedConsumer.Consume(CancellationToken.None));

            tracerProvider.ForceFlush();
        }

        var activity = activities.FirstOrDefault(a => a.DisplayName == "error-topic-no-headers receive");
        Assert.NotNull(activity);
        Assert.Equal(ActivityKind.Consumer, activity.Kind);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("fake-consumer-1", activity.GetTagValue(SemanticConventions.AttributeMessagingClientId));
        Assert.Equal("test-group", activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaConsumerGroup));
        Assert.Equal("receive", activity.GetTagValue(SemanticConventions.AttributeMessagingOperation));
        Assert.Equal("ConsumeException: All brokers down", activity.GetTagValue(SemanticConventions.AttributeErrorType));
        Assert.Equal("error-topic-no-headers", activity.GetTagValue(SemanticConventions.AttributeMessagingDestinationName));
        Assert.Equal(3, activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaDestinationPartition));
        Assert.Equal(150L, activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaMessageOffset));
        Assert.Equal("error-key", Encoding.UTF8.GetString((byte[])activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaMessageKey)!));
    }

    [Fact]
    public void Consume_ConsumeException_WithValidMessageWithHeaders_CreatesActivityWithError()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(activities)
            .Build())
        {
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

            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumerExceptionToThrow = exception,
            };

            var options = new ConfluentKafkaConsumerInstrumentationOptions<string, string>
            {
                Traces = true,
                Metrics = false,
            };
            var instrumentedConsumer = new InstrumentedConsumer<string, string>(fakeConsumer, options)
            {
                GroupId = "test-group",
            };

            Assert.Throws<ConsumeException>(() => instrumentedConsumer.Consume(CancellationToken.None));

            tracerProvider.ForceFlush();
        }

        var activity = activities.FirstOrDefault(a => a.DisplayName == "error-topic-with-headers receive");
        Assert.NotNull(activity);
        Assert.NotEmpty(activity.Links);
        Assert.Equal("0af7651916cd43dd8448eb211c80319c", activity.Links.First().Context.TraceId.ToHexString());
        Assert.Equal(ActivityKind.Consumer, activity.Kind);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("fake-consumer-1", activity.GetTagValue(SemanticConventions.AttributeMessagingClientId));
        Assert.Equal("test-group", activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaConsumerGroup));
        Assert.Equal("receive", activity.GetTagValue(SemanticConventions.AttributeMessagingOperation));
        Assert.Equal("ConsumeException: Broker not available", activity.GetTagValue(SemanticConventions.AttributeErrorType));
        Assert.Equal("error-topic-with-headers", activity.GetTagValue(SemanticConventions.AttributeMessagingDestinationName));
        Assert.Equal(5, activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaDestinationPartition));
        Assert.Equal(200L, activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaMessageOffset));
        Assert.Equal("error-key-with-headers", Encoding.UTF8.GetString((byte[])activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaMessageKey)!));
    }

    [Fact]
    public void Consume_TimeSpan_ConsumeException_CreatesActivityWithError()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(activities)
            .Build())
        {
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

        var activity = activities.FirstOrDefault(a => a.DisplayName == "timeout-topic receive");
        Assert.NotNull(activity);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("ConsumeException: Operation timed out", activity.GetTagValue(SemanticConventions.AttributeErrorType));
    }

    [Fact]
    public void Consume_Milliseconds_ConsumeException_CreatesActivityWithError()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(activities)
            .Build())
        {
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

        var activity = activities.FirstOrDefault(a => a.DisplayName == "broker-error-topic receive");
        Assert.NotNull(activity);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("ConsumeException: Transport error", activity.GetTagValue(SemanticConventions.AttributeErrorType));
    }

    [Fact]
    public void Consume_ConsumeException_WithNullConsumerRecord_RecordsMetricsWithError()
    {
        var metrics = new List<Metric>();

        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(metrics)
            .Build())
        {
            var error = new Error(ErrorCode.Local_ValueDeserialization, "Deserialization error");
            ConsumeResult<byte[], byte[]>? nullConsumerRecord = null;
            var exception = new ConsumeException(nullConsumerRecord, error);

            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumerExceptionToThrow = exception,
            };

            var options = new ConfluentKafkaConsumerInstrumentationOptions<string, string>
            {
                Traces = false,
                Metrics = true,
            };
            var instrumentedConsumer = new InstrumentedConsumer<string, string>(fakeConsumer, options);

            Assert.Throws<ConsumeException>(() => instrumentedConsumer.Consume(CancellationToken.None));

            meterProvider.EnsureMetricsAreFlushed();
        }

        var receiveMessagesMetric = metrics.FirstOrDefault(m => m.Name == SemanticConventions.MetricMessagingReceiveMessages);
        AssertMetric(
            actualMetric: receiveMessagesMetric,
            expectedMessagingOperation: ConfluentKafkaCommon.ReceiveOperationName,
            expectedMessagingSystem: ConfluentKafkaCommon.KafkaMessagingSystem,
            expectedKafkaDestinationName: null,
            expectedKafkaDestinationPartition: null,
            expectedErrorType: "ConsumeException: Deserialization error");

        var receiveDurationMetric = metrics.FirstOrDefault(m => m.Name == SemanticConventions.MetricMessagingReceiveDuration);
        AssertMetric(
            actualMetric: receiveDurationMetric,
            expectedMessagingOperation: ConfluentKafkaCommon.ReceiveOperationName,
            expectedMessagingSystem: ConfluentKafkaCommon.KafkaMessagingSystem,
            expectedKafkaDestinationName: null,
            expectedKafkaDestinationPartition: null,
            expectedErrorType: "ConsumeException: Deserialization error");
    }

    [Fact]
    public void Consume_ConsumeException_WithNullMessage_RecordsMetricsWithError()
    {
        var metrics = new List<Metric>();

        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(metrics)
            .Build())
        {
            var consumerRecord = new ConsumeResult<byte[], byte[]>
            {
                Topic = "error-topic",
                Partition = new Partition(2),
                Offset = new Offset(100),
                Message = null,
            };

            var error = new Error(ErrorCode.Local_KeyDeserialization, "Key Deserialization error");
            var exception = new ConsumeException(consumerRecord, error);

            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumerExceptionToThrow = exception,
            };

            var options = new ConfluentKafkaConsumerInstrumentationOptions<string, string>
            {
                Traces = false,
                Metrics = true,
            };
            var instrumentedConsumer = new InstrumentedConsumer<string, string>(fakeConsumer, options);

            Assert.Throws<ConsumeException>(() => instrumentedConsumer.Consume(CancellationToken.None));

            meterProvider.EnsureMetricsAreFlushed();
        }

        var receiveMessagesMetric = metrics.FirstOrDefault(m => m.Name == SemanticConventions.MetricMessagingReceiveMessages);
        AssertMetric(
            actualMetric: receiveMessagesMetric,
            expectedMessagingOperation: ConfluentKafkaCommon.ReceiveOperationName,
            expectedMessagingSystem: ConfluentKafkaCommon.KafkaMessagingSystem,
            expectedKafkaDestinationName: "error-topic",
            expectedKafkaDestinationPartition: 2,
            expectedErrorType: "ConsumeException: Key Deserialization error");

        var receiveDurationMetric = metrics.FirstOrDefault(m => m.Name == SemanticConventions.MetricMessagingReceiveDuration);
        AssertMetric(
            actualMetric: receiveDurationMetric,
            expectedMessagingOperation: ConfluentKafkaCommon.ReceiveOperationName,
            expectedMessagingSystem: ConfluentKafkaCommon.KafkaMessagingSystem,
            expectedKafkaDestinationName: "error-topic",
            expectedKafkaDestinationPartition: 2,
            expectedErrorType: "ConsumeException: Key Deserialization error");
    }

    [Fact]
    public void Consume_ConsumeException_WithValidMessageWithoutHeaders_RecordsMetricsWithError()
    {
        var metrics = new List<Metric>();

        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(metrics)
            .Build())
        {
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

            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumerExceptionToThrow = exception,
            };

            var options = new ConfluentKafkaConsumerInstrumentationOptions<string, string>
            {
                Traces = false,
                Metrics = true,
            };
            var instrumentedConsumer = new InstrumentedConsumer<string, string>(fakeConsumer, options);

            Assert.Throws<ConsumeException>(() => instrumentedConsumer.Consume(CancellationToken.None));

            meterProvider.EnsureMetricsAreFlushed();
        }

        var receiveMessagesMetric = metrics.FirstOrDefault(m => m.Name == SemanticConventions.MetricMessagingReceiveMessages);
        AssertMetric(
            actualMetric: receiveMessagesMetric,
            expectedMessagingOperation: ConfluentKafkaCommon.ReceiveOperationName,
            expectedMessagingSystem: ConfluentKafkaCommon.KafkaMessagingSystem,
            expectedKafkaDestinationName: "error-topic-no-headers",
            expectedKafkaDestinationPartition: 3,
            expectedErrorType: "ConsumeException: All brokers down");

        var receiveDurationMetric = metrics.FirstOrDefault(m => m.Name == SemanticConventions.MetricMessagingReceiveDuration);
        AssertMetric(
            actualMetric: receiveDurationMetric,
            expectedMessagingOperation: ConfluentKafkaCommon.ReceiveOperationName,
            expectedMessagingSystem: ConfluentKafkaCommon.KafkaMessagingSystem,
            expectedKafkaDestinationName: "error-topic-no-headers",
            expectedKafkaDestinationPartition: 3,
            expectedErrorType: "ConsumeException: All brokers down");
    }

    [Fact]
    public void Consume_ConsumeException_WithValidMessageWithHeaders_RecordsMetricsWithError()
    {
        var metrics = new List<Metric>();

        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(metrics)
            .Build())
        {
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

            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumerExceptionToThrow = exception,
            };

            var options = new ConfluentKafkaConsumerInstrumentationOptions<string, string>
            {
                Traces = false,
                Metrics = true,
            };
            var instrumentedConsumer = new InstrumentedConsumer<string, string>(fakeConsumer, options)
            {
                GroupId = "test-group",
            };

            Assert.Throws<ConsumeException>(() => instrumentedConsumer.Consume(CancellationToken.None));

            meterProvider.EnsureMetricsAreFlushed();
        }

        var receiveMessagesMetric = metrics.FirstOrDefault(m => m.Name == SemanticConventions.MetricMessagingReceiveMessages);
        AssertMetric(
            actualMetric: receiveMessagesMetric,
            expectedMessagingOperation: ConfluentKafkaCommon.ReceiveOperationName,
            expectedMessagingSystem: ConfluentKafkaCommon.KafkaMessagingSystem,
            expectedKafkaDestinationName: "error-topic-with-headers",
            expectedKafkaDestinationPartition: 5,
            expectedErrorType: "ConsumeException: Broker not available");

        var receiveDurationMetric = metrics.FirstOrDefault(m => m.Name == SemanticConventions.MetricMessagingReceiveDuration);
        AssertMetric(
            actualMetric: receiveDurationMetric,
            expectedMessagingOperation: ConfluentKafkaCommon.ReceiveOperationName,
            expectedMessagingSystem: ConfluentKafkaCommon.KafkaMessagingSystem,
            expectedKafkaDestinationName: "error-topic-with-headers",
            expectedKafkaDestinationPartition: 5,
            expectedErrorType: "ConsumeException: Broker not available");
    }

    private static void AssertMetric(
       Metric? actualMetric,
       string? expectedMessagingOperation,
       string? expectedMessagingSystem,
       string? expectedKafkaDestinationName,
       int? expectedKafkaDestinationPartition,
       string? expectedErrorType)
    {
        Assert.NotNull(actualMetric);

        var metricPoint = GetMetricPoint(actualMetric);

        var destinationNameFound = false;
        var destinationPartitionFound = false;
        var errorTypeFound = false;

        foreach (var tag in metricPoint!.Value.Tags)
        {
            if (tag.Key == SemanticConventions.AttributeMessagingOperation)
            {
                Assert.Equal(expectedMessagingOperation, tag.Value?.ToString());
            }

            if (tag.Key == SemanticConventions.AttributeMessagingSystem)
            {
                Assert.Equal(expectedMessagingSystem, tag.Value?.ToString());
            }

            if (tag.Key == SemanticConventions.AttributeMessagingDestinationName)
            {
                Assert.Equal(expectedKafkaDestinationName, tag.Value?.ToString());
                destinationNameFound = true;
            }

            if (tag.Key == SemanticConventions.AttributeMessagingKafkaDestinationPartition)
            {
                Assert.Equal(expectedKafkaDestinationPartition, tag.Value);
                destinationPartitionFound = true;
            }

            if (tag.Key == SemanticConventions.AttributeErrorType)
            {
                Assert.Equal(expectedErrorType, tag.Value?.ToString());
                errorTypeFound = true;
            }
        }

        if (expectedKafkaDestinationName is null)
        {
            Assert.False(destinationNameFound);
        }

        if (expectedKafkaDestinationPartition is null)
        {
            Assert.False(destinationPartitionFound);
        }

        if (expectedErrorType is null)
        {
            Assert.False(errorTypeFound);
        }
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
        {
            if (this.ConsumerExceptionToThrow != null)
            {
                throw this.ConsumerExceptionToThrow;
            }

            return this.ConsumeResult;
        }

        public ConsumeResult<TKey, TValue>? Consume(CancellationToken cancellationToken = default)
        {
            if (this.ConsumerExceptionToThrow != null)
            {
                throw this.ConsumerExceptionToThrow;
            }

            return this.ConsumeResult;
        }

        public ConsumeResult<TKey, TValue>? Consume(TimeSpan timeout)
        {
            if (this.ConsumerExceptionToThrow != null)
            {
                throw this.ConsumerExceptionToThrow;
            }

            return this.ConsumeResult;
        }

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
