// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.Telemetry;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using OpenTelemetry.Instrumentation.AWS.Tests.Tools;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWS.Tests;

public class TestAWSClientMetricsInstrumentation
{
    [Fact]
#if NETFRAMEWORK
    public void TestS3PutObjectSuccessful()
#else
    public async Task TestS3PutObjectSuccessful()
#endif
    {
        var exportedItems = new List<Metrics.Metric>();

        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
                                      .AddAWSInstrumentation()
                                      .AddInMemoryExporter(exportedItems)
                                      .Build())
        {
            var s3 = new AmazonS3Client(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            CustomResponses.SetResponse(s3, null, "test_request_id", "extended_request_id", true);
            var putObjectRequest = new PutObjectRequest
            {
                BucketName = "TestBucket",
                Key = "TestKey",
                ContentBody = "Test Content",
            };

#if NETFRAMEWORK
            s3.PutObject(putObjectRequest);
#else
            await s3.PutObjectAsync(putObjectRequest);
#endif
            meterProvider.ForceFlush();
        }

        this.ValidateCommonMetrics(exportedItems);
        this.ValidateHTTPBytesMetric(exportedItems, "client.http.bytes_sent");
    }

    [Fact]
#if NETFRAMEWORK
    public void TestSNSCreateTopicUnsuccessful()
#else
    public async Task TestSNSCreateTopicUnsuccessful()
#endif
    {
        var exportedItems = new List<Metrics.Metric>();

        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
                                      .AddAWSInstrumentation()
                                      .AddInMemoryExporter(exportedItems)
                                      .Build())
        {
            var sns = new AmazonSimpleNotificationServiceClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            var amazonServiceException = new AmazonServiceException
            {
                StatusCode = System.Net.HttpStatusCode.NotFound,
                RequestId = "requestId",
            };
            CustomResponses.SetResponse(sns, (request) => { throw amazonServiceException; });
            var createTopicRequest = new CreateTopicRequest
            {
                Name = "NewTopic",
            };

            try
            {
#if NETFRAMEWORK
                sns.CreateTopic(createTopicRequest);
#else
                await sns.CreateTopicAsync(createTopicRequest);
#endif
            }
            catch (AmazonServiceException)
            {
                // Expected
            }
            finally
            {
                meterProvider.ForceFlush();
            }
        }

        this.ValidateCommonMetrics(exportedItems, false);

        var callErrorsMetric = exportedItems.FirstOrDefault(i => i.Name == "client.call.errors");
        Assert.NotNull(callErrorsMetric);

        var metricPoints = new List<MetricPoint>();
        foreach (var p in callErrorsMetric.GetMetricPoints())
        {
            metricPoints.Add(p);
        }

        var metricPoint = metricPoints[0];
        var sum = metricPoint.GetSumLong();

        Assert.Equal(MetricType.LongSum, callErrorsMetric.MetricType);
        Assert.Equal("{error}", callErrorsMetric.Unit);
        Assert.True(sum > 0);
    }

    [Fact]
#if NETFRAMEWORK
    public void TestSQSCreateQueueSuccessful()
#else
    public async Task TestSQSCreateQueueSuccessful()
#endif
    {
        var exportedItems = new List<Metrics.Metric>();

        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
                                      .AddAWSInstrumentation()
                                      .AddInMemoryExporter(exportedItems)
                                      .Build())
        {
            var sqs = new AmazonSQSClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
            var dummyResponse = "{}";
            CustomResponses.SetResponse(sqs, dummyResponse, "requestId", "extended_request_id", true);
            var send_msg_req = new CreateQueueRequest()
            {
                QueueName = "MyTestQueue",
            };

#if NETFRAMEWORK
            sqs.CreateQueue(send_msg_req);
#else
            await sqs.CreateQueueAsync(send_msg_req);
#endif
            meterProvider.ForceFlush();
        }

        this.ValidateCommonMetrics(exportedItems);
        this.ValidateHTTPBytesMetric(exportedItems, "client.http.bytes_sent");
        this.ValidateHTTPBytesMetric(exportedItems, "client.http.bytes_received");
    }

    [Fact]
    public void TestAWSUpDownCounterIsCalledProperly()
    {
        var countAmount = 7;
        var counterName = $"{nameof(this.TestAWSUpDownCounterIsCalledProperly)}.Counter";
        var meterName = $"{TelemetryConstants.TelemetryScopePrefix}.{nameof(this.TestAWSUpDownCounterIsCalledProperly)}";

        var exportedItems = new List<Metrics.Metric>();

        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
                                      .AddAWSInstrumentation()
                                      .AddInMemoryExporter(exportedItems)
                                      .Build())
        {
            using var meter = AWSConfigs.TelemetryProvider.MeterProvider.GetMeter(meterName);
            var counter = meter.CreateUpDownCounter<long>(counterName);

            counter.Add(countAmount);
            counter.Add(countAmount);

            meterProvider.ForceFlush();
        }

        var counterMetric = exportedItems.FirstOrDefault(i => i.Name == counterName);

        Assert.NotNull(counterMetric);
        Assert.Equal(MetricType.LongSumNonMonotonic, counterMetric.MetricType);

        var metricPoints = new List<MetricPoint>();
        foreach (var p in counterMetric.GetMetricPoints())
        {
            metricPoints.Add(p);
        }

        Assert.Single(metricPoints);
        var metricPoint = metricPoints[0];

        Assert.Equal(countAmount * 2, metricPoint.GetSumLong());
        Assert.Null(counterMetric.MeterTags);
    }

    [Fact]
    public void TestAWSUpDownCounterIsntCalledAfterMeterDispose()
    {
        var countAmount = 7;
        var counterName = $"{nameof(this.TestAWSUpDownCounterIsntCalledAfterMeterDispose)}.Counter";
        var meterName = $"{TelemetryConstants.TelemetryScopePrefix}.TestDisposedMeter";

        var exportedItems = new List<Metrics.Metric>();

        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
                                      .AddAWSInstrumentation()
                                      .AddInMemoryExporter(exportedItems)
                                      .Build())
        {
            using var meter = AWSConfigs.TelemetryProvider.MeterProvider.GetMeter(meterName);
            var counter = meter.CreateUpDownCounter<long>(counterName);

            meter.Dispose();
            counter.Add(countAmount);

            meterProvider.ForceFlush();
        }

        var counterMetric = exportedItems.FirstOrDefault(i => i.MeterName == meterName && i.Name == counterName);
        Assert.Null(counterMetric);
    }

    [Fact]
    public void TestMeterProviderAddsTagsToMetrics()
    {
        var countAmount = 7;
        var counterName = $"{nameof(this.TestMeterProviderAddsTagsToMetrics)}.Counter";
        var meterName = $"{TelemetryConstants.TelemetryScopePrefix}.{nameof(this.TestMeterProviderAddsTagsToMetrics)}";

        var attributes = new Attributes([new KeyValuePair<string, object>("key1", "value1"), new KeyValuePair<string, object>("key2", "value2")]);

        var exportedItems = new List<Metrics.Metric>();
        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
                                      .AddAWSInstrumentation()
                                      .AddInMemoryExporter(exportedItems)
                                      .Build())
        {
            using var meter = AWSConfigs.TelemetryProvider.MeterProvider.GetMeter(meterName, attributes);
            var counter = meter.CreateUpDownCounter<long>(counterName);

            counter.Add(countAmount);
            counter.Add(countAmount);

            meterProvider.ForceFlush();
        }

        var counterMetric = exportedItems.FirstOrDefault(i => i.Name == counterName);

        Assert.NotNull(counterMetric);
        Assert.Equal(MetricType.LongSumNonMonotonic, counterMetric.MetricType);

        var metricPoints = new List<MetricPoint>();
        foreach (var p in counterMetric.GetMetricPoints())
        {
            metricPoints.Add(p);
        }

        Assert.Single(metricPoints);
        var metricPoint = metricPoints[0];

        Assert.Equal(countAmount * 2, metricPoint.GetSumLong());

        Assert.NotNull(counterMetric.MeterTags);
        Assert.NotEmpty(counterMetric.MeterTags);
        Assert.Contains(new KeyValuePair<string, object?>("key1", "value1"), counterMetric.MeterTags);
        Assert.Contains(new KeyValuePair<string, object?>("key2", "value2"), counterMetric.MeterTags);
    }

    private void ValidateHTTPBytesMetric(List<Metrics.Metric> exportedMetrics, string metricName)
    {
        var httpBytesSent = exportedMetrics.FirstOrDefault(i => i.Name == metricName);
        Assert.NotNull(httpBytesSent);

        var metricPoints = new List<MetricPoint>();
        foreach (var p in httpBytesSent.GetMetricPoints())
        {
            metricPoints.Add(p);
        }

        var metricPoint = metricPoints[0];
        var sum = metricPoint.GetSumLong();

        Assert.Equal(MetricType.LongSum, httpBytesSent.MetricType);
        Assert.Equal("By", httpBytesSent.Unit);
        Assert.True(sum > 0);
    }

    private void ValidateCommonMetrics(List<Metrics.Metric> exportedMetrics, bool successful = true)
    {
        var callDuration = exportedMetrics.FirstOrDefault(i => i.Name == "client.call.duration");
        this.ValidateDurationMetric(callDuration);

        var callSerializationDuration = exportedMetrics.FirstOrDefault(i => i.Name == "client.call.serialization_duration");
        this.ValidateDurationMetric(callSerializationDuration);

        var callResolveEndpointDuration = exportedMetrics.FirstOrDefault(i => i.Name == "client.call.resolve_endpoint_duration");
        this.ValidateDurationMetric(callResolveEndpointDuration);

        var callAttemptDuration = exportedMetrics.FirstOrDefault(i => i.Name == "client.call.attempt_duration");
        this.ValidateDurationMetric(callAttemptDuration);

        // Unsuccessful calls wont reach the deserialization stage.
        if (successful)
        {
            var callDeserializationDuration = exportedMetrics.FirstOrDefault(i => i.Name == "client.call.deserialization_duration");
            this.ValidateDurationMetric(callDeserializationDuration);
        }

        foreach (var metric in exportedMetrics)
        {
            Assert.NotNull(metric.MeterVersion);
            Assert.NotEmpty(metric.MeterVersion);
            Assert.StartsWith("https://opentelemetry.io/schemas/1.28.0", metric.MeterSchemaUrl);
        }
    }

    private void ValidateDurationMetric(Metrics.Metric? durationMetric)
    {
        Assert.NotNull(durationMetric);

        var metricPoints = new List<MetricPoint>();
        foreach (var p in durationMetric.GetMetricPoints())
        {
            metricPoints.Add(p);
        }

        var metricPoint = metricPoints[0];
        var count = metricPoint.GetHistogramCount();

        Assert.Equal(MetricType.Histogram, durationMetric.MetricType);
        Assert.Equal("s", durationMetric.Unit);
        Assert.True(count > 0);
    }
}
