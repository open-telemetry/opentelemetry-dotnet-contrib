// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

public class OpenTelemetryConsumerBuilderExtensionsTests
{
    [Fact]
    public void ShouldConvertConsumerBuilderToInstrumentedConsumerBuilder()
    {
        // Arrange
        var config = new List<KeyValuePair<string, string>>
        {
            new("bootstrap.servers", "localhost:9092"),
        };

        var consumerBuilder = new ConsumerBuilder<string, string>(config);

        var keyDeserializer = Deserializers.Utf8;
        var valueDeserializer = Deserializers.Utf8;

        consumerBuilder.SetErrorHandler(ErrorHandler);
        consumerBuilder.SetLogHandler(LogHandler);
        consumerBuilder.SetStatisticsHandler(StatisticsHandler);
        consumerBuilder.SetOAuthBearerTokenRefreshHandler(OAuthBearerTokenRefreshHandler);
        consumerBuilder.SetOffsetsCommittedHandler(OffsetsCommittedHandler);
        consumerBuilder.SetPartitionsAssignedHandler(PartitionsAssignedHandler);
        consumerBuilder.SetPartitionsRevokedHandler(PartitionsRevokedHandler);
        consumerBuilder.SetPartitionsLostHandler(PartitionsLostHandler);
        consumerBuilder.SetKeyDeserializer(keyDeserializer);
        consumerBuilder.SetValueDeserializer(valueDeserializer);

        // Act
        var instrumentedConsumerBuilder = consumerBuilder.AsInstrumentedConsumerBuilder();

        // Assert
        Assert.Equal(ErrorHandler, instrumentedConsumerBuilder.GetInternalErrorHandler());
        Assert.Equal(LogHandler, instrumentedConsumerBuilder.GetInternalLogHandler());
        Assert.Equal(StatisticsHandler, instrumentedConsumerBuilder.GetInternalStatisticsHandler());
        Assert.Equal(OAuthBearerTokenRefreshHandler, instrumentedConsumerBuilder.GetInternalOAuthBearerTokenRefreshHandler());
        Assert.Equal(OffsetsCommittedHandler, instrumentedConsumerBuilder.GetInternalOffsetsCommittedHandler());
        Assert.Equal(PartitionsAssignedHandler, instrumentedConsumerBuilder.GetInternalPartitionsAssignedHandler());
        Assert.Equal(PartitionsRevokedHandler, instrumentedConsumerBuilder.GetInternalPartitionsRevokedHandler());
        Assert.Equal(PartitionsLostHandler, instrumentedConsumerBuilder.GetInternalPartitionsLostHandler());
        Assert.Equal(keyDeserializer, instrumentedConsumerBuilder.GetInternalKeyDeserializer());
        Assert.Equal(valueDeserializer, instrumentedConsumerBuilder.GetInternalValueDeserializer());
        Assert.False(instrumentedConsumerBuilder.EnableMetrics);
        Assert.False(instrumentedConsumerBuilder.EnableTraces);
        return;

        void ErrorHandler(IConsumer<string, string> consumer, Error error)
        {
        }

        void LogHandler(IConsumer<string, string> consumer, LogMessage logMessage)
        {
        }

        void StatisticsHandler(IConsumer<string, string> consumer, string statistics)
        {
        }

        void OAuthBearerTokenRefreshHandler(IConsumer<string, string> consumer, string oauthBearerToken)
        {
        }

        void OffsetsCommittedHandler(IConsumer<string, string> consumer, CommittedOffsets offsets)
        {
        }

        IEnumerable<TopicPartitionOffset> PartitionsAssignedHandler(IConsumer<string, string> consumer, List<TopicPartition> partitions)
        {
            return [];
        }

        IEnumerable<TopicPartitionOffset> PartitionsRevokedHandler(IConsumer<string, string> consumer, List<TopicPartitionOffset> partitions)
        {
            return [];
        }

        IEnumerable<TopicPartitionOffset> PartitionsLostHandler(IConsumer<string, string> consumer, List<TopicPartitionOffset> partitions)
        {
            return [];
        }
    }

    [Fact]
    public void ShouldConvertUserDefinedConsumerBuilderToInstrumentedConsumerBuilder()
    {
        // Arrange
        var config = new List<KeyValuePair<string, string>>
        {
            new("bootstrap.servers", "localhost:9092"),
        };

        var consumerBuilder = new CustomConsumerBuilder<string, string>(config);

        var keyDeserializer = Deserializers.Utf8;
        var valueDeserializer = Deserializers.Utf8;

        consumerBuilder.SetErrorHandler(ErrorHandler);
        consumerBuilder.SetLogHandler(LogHandler);
        consumerBuilder.SetStatisticsHandler(StatisticsHandler);
        consumerBuilder.SetOAuthBearerTokenRefreshHandler(OAuthBearerTokenRefreshHandler);
        consumerBuilder.SetOffsetsCommittedHandler(OffsetsCommittedHandler);
        consumerBuilder.SetPartitionsAssignedHandler(PartitionsAssignedHandler);
        consumerBuilder.SetPartitionsRevokedHandler(PartitionsRevokedHandler);
        consumerBuilder.SetPartitionsLostHandler(PartitionsLostHandler);
        consumerBuilder.SetKeyDeserializer(keyDeserializer);
        consumerBuilder.SetValueDeserializer(valueDeserializer);

        // Act
        var instrumentedConsumerBuilder = consumerBuilder.AsInstrumentedConsumerBuilder();

        // Assert
        Assert.Equal(ErrorHandler, instrumentedConsumerBuilder.GetInternalErrorHandler());
        Assert.Equal(LogHandler, instrumentedConsumerBuilder.GetInternalLogHandler());
        Assert.Equal(StatisticsHandler, instrumentedConsumerBuilder.GetInternalStatisticsHandler());
        Assert.Equal(OAuthBearerTokenRefreshHandler, instrumentedConsumerBuilder.GetInternalOAuthBearerTokenRefreshHandler());
        Assert.Equal(OffsetsCommittedHandler, instrumentedConsumerBuilder.GetInternalOffsetsCommittedHandler());
        Assert.Equal(PartitionsAssignedHandler, instrumentedConsumerBuilder.GetInternalPartitionsAssignedHandler());
        Assert.Equal(PartitionsRevokedHandler, instrumentedConsumerBuilder.GetInternalPartitionsRevokedHandler());
        Assert.Equal(PartitionsLostHandler, instrumentedConsumerBuilder.GetInternalPartitionsLostHandler());
        Assert.Equal(keyDeserializer, instrumentedConsumerBuilder.GetInternalKeyDeserializer());
        Assert.Equal(valueDeserializer, instrumentedConsumerBuilder.GetInternalValueDeserializer());
        return;

        void ErrorHandler(IConsumer<string, string> consumer, Error error)
        {
        }

        void LogHandler(IConsumer<string, string> consumer, LogMessage logMessage)
        {
        }

        void StatisticsHandler(IConsumer<string, string> consumer, string statistics)
        {
        }

        void OAuthBearerTokenRefreshHandler(IConsumer<string, string> consumer, string oauthBearerToken)
        {
        }

        void OffsetsCommittedHandler(IConsumer<string, string> consumer, CommittedOffsets offsets)
        {
        }

        IEnumerable<TopicPartitionOffset> PartitionsAssignedHandler(IConsumer<string, string> consumer, List<TopicPartition> partitions)
        {
            return [];
        }

        IEnumerable<TopicPartitionOffset> PartitionsRevokedHandler(IConsumer<string, string> consumer, List<TopicPartitionOffset> partitions)
        {
            return [];
        }

        IEnumerable<TopicPartitionOffset> PartitionsLostHandler(IConsumer<string, string> consumer, List<TopicPartitionOffset> partitions)
        {
            return [];
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ShouldSetTracingPropertiesIfOptionsIsDefined(bool enableMetrics, bool enableTraces)
    {
        // Arrange
        var config = new List<KeyValuePair<string, string>>
        {
            new("bootstrap.servers", "localhost:9092"),
        };

        var consumerBuilder = new ConsumerBuilder<string, string>(config);

        var options = new ConfluentKafkaInstrumentedConsumerBuilderOptions
        {
            EnableMetrics = enableMetrics,
            EnableTraces = enableTraces,
        };

        // Act
        var instrumentedConsumerBuilder = consumerBuilder.AsInstrumentedConsumerBuilder(options);

        // Assert
        Assert.Equal(enableMetrics, instrumentedConsumerBuilder.EnableMetrics);
        Assert.Equal(enableTraces, instrumentedConsumerBuilder.EnableTraces);
    }

    [Fact]
    public void ShouldNotThrowNullReferenceExceptionsIfOptionsIsNull()
    {
        // Arrange
        var config = new List<KeyValuePair<string, string>>
        {
            new("bootstrap.servers", "localhost:9092"),
        };

        var consumerBuilder = new ConsumerBuilder<string, string>(config);

        // Act
        var instrumentedBuilder = consumerBuilder.AsInstrumentedConsumerBuilder(null);

        // Assert
        Assert.False(instrumentedBuilder.EnableMetrics);
        Assert.False(instrumentedBuilder.EnableTraces);
    }

    private class CustomConsumerBuilder<TKey, TValue>(IEnumerable<KeyValuePair<string, string>> config)
        : ConsumerBuilder<TKey, TValue>(config);
}
