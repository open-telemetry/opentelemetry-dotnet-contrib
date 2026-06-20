// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

public class OpenTelemetryProducerBuilderExtensionsTests
{
    [Fact]
    public void ShouldConvertToInstrumentedProducerBuilder()
    {
        // Arrange
        var config = new List<KeyValuePair<string, string>>
        {
            new("bootstrap.servers", "localhost:9092"),
        };

        var producerBuilder = new ProducerBuilder<string, string>(config);

        var keySerializer = Serializers.Utf8;
        var valueSerializer = Serializers.Utf8;

        producerBuilder.SetErrorHandler(ErrorHandler);
        producerBuilder.SetLogHandler(LogHandler);
        producerBuilder.SetStatisticsHandler(StatisticsHandler);
        producerBuilder.SetOAuthBearerTokenRefreshHandler(OAuthBearerTokenRefreshHandler);
        producerBuilder.SetKeySerializer(keySerializer);
        producerBuilder.SetValueSerializer(valueSerializer);

        // Act
        var instrumentedProducerBuilder = producerBuilder.AsInstrumentedProducerBuilder();

        // Assert
        Assert.Equal(ErrorHandler, instrumentedProducerBuilder.GetInternalErrorHandler());
        Assert.Equal(LogHandler, instrumentedProducerBuilder.GetInternalLogHandler());
        Assert.Equal(StatisticsHandler, instrumentedProducerBuilder.GetInternalStatisticsHandler());
        Assert.Equal(OAuthBearerTokenRefreshHandler, instrumentedProducerBuilder.GetInternalOAuthBearerTokenRefreshHandler());
        Assert.Equal(keySerializer, instrumentedProducerBuilder.GetInternalKeySerializer());
        Assert.Equal(valueSerializer, instrumentedProducerBuilder.GetInternalValueSerializer());
        Assert.False(instrumentedProducerBuilder.EnableMetrics);
        Assert.False(instrumentedProducerBuilder.EnableTraces);
        return;

        void ErrorHandler(IProducer<string, string> producer, Error error)
        {
        }

        void LogHandler(IProducer<string, string> producer, LogMessage logMessage)
        {
        }

        void StatisticsHandler(IProducer<string, string> producer, string statistics)
        {
        }

        void OAuthBearerTokenRefreshHandler(IProducer<string, string> producer, string oauthBearerToken)
        {
        }
    }

    [Fact]
    public void ShouldConvertUserDefinedProducerBuilderToInstrumentedProducerBuilder()
    {
        // Arrange
        var config = new List<KeyValuePair<string, string>>
        {
            new("bootstrap.servers", "localhost:9092"),
        };

        var producerBuilder = new CustomProducerBuilder<string, string>(config);

        var keySerializer = Serializers.Utf8;
        var valueSerializer = Serializers.Utf8;

        producerBuilder.SetErrorHandler(ErrorHandler);
        producerBuilder.SetLogHandler(LogHandler);
        producerBuilder.SetStatisticsHandler(StatisticsHandler);
        producerBuilder.SetOAuthBearerTokenRefreshHandler(OAuthBearerTokenRefreshHandler);
        producerBuilder.SetKeySerializer(keySerializer);
        producerBuilder.SetValueSerializer(valueSerializer);

        // Act
        var instrumentedProducerBuilder = producerBuilder.AsInstrumentedProducerBuilder();

        // Assert
        Assert.Equal(ErrorHandler, instrumentedProducerBuilder.GetInternalErrorHandler());
        Assert.Equal(LogHandler, instrumentedProducerBuilder.GetInternalLogHandler());
        Assert.Equal(StatisticsHandler, instrumentedProducerBuilder.GetInternalStatisticsHandler());
        Assert.Equal(OAuthBearerTokenRefreshHandler, instrumentedProducerBuilder.GetInternalOAuthBearerTokenRefreshHandler());
        Assert.Equal(keySerializer, instrumentedProducerBuilder.GetInternalKeySerializer());
        Assert.Equal(valueSerializer, instrumentedProducerBuilder.GetInternalValueSerializer());
        return;

        void ErrorHandler(IProducer<string, string> producer, Error error)
        {
        }

        void LogHandler(IProducer<string, string> producer, LogMessage logMessage)
        {
        }

        void StatisticsHandler(IProducer<string, string> producer, string statistics)
        {
        }

        void OAuthBearerTokenRefreshHandler(IProducer<string, string> producer, string oauthBearerToken)
        {
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
        var config = new ProducerConfig();
        var builder = new ProducerBuilder<string, string>(config);

        var options = new ConfluentKafkaInstrumentedProducerBuilderOptions
        {
            EnableMetrics = enableMetrics,
            EnableTraces = enableTraces,
        };

        // Act
        var instrumentedBuilder = builder.AsInstrumentedProducerBuilder(options);

        // Assert
        Assert.Equal(enableMetrics, instrumentedBuilder.EnableMetrics);
        Assert.Equal(enableTraces, instrumentedBuilder.EnableTraces);
    }

    [Fact]
    public void ShouldNotThrowNullReferenceExceptionsIfOptionsIsNull()
    {
        // Arrange
        var config = new ProducerConfig();
        var builder = new ProducerBuilder<string, string>(config);

        // Act
        var instrumentedBuilder = builder.AsInstrumentedProducerBuilder(null);

        // Assert
        Assert.False(instrumentedBuilder.EnableMetrics);
        Assert.False(instrumentedBuilder.EnableTraces);
    }

    private class CustomProducerBuilder<TKey, TValue>(IEnumerable<KeyValuePair<string, string>> config)
        : ProducerBuilder<TKey, TValue>(config);
}
