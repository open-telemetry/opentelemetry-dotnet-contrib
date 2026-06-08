// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Reflection;
using InfluxDB.Client;
using OpenTelemetry.Exporter.InfluxDB.Tests.Utils;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.InfluxDB.Tests;

public class InfluxDBMetricsExporterTests
{
    private static readonly string OpenTelemetrySdkVersion;
    private static readonly double[] TestBoundaries = [10D, 20D, 100D, 200D];

#pragma warning disable CA1810 // Initialize reference type static fields inline
    static InfluxDBMetricsExporterTests()
#pragma warning restore CA1810 // Initialize reference type static fields inline
    {
        var sdkVersion = typeof(Sdk).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        OpenTelemetrySdkVersion = sdkVersion != null ? Version.Parse(sdkVersion).ToString(3) : "0.0.0";
    }

    [Theory]
    [InlineData(MetricsSchema.TelegrafPrometheusV1, "test-gauge", "gauge")]
    [InlineData(MetricsSchema.TelegrafPrometheusV2, "prometheus", "test-gauge")]
    public void ExportIntGaugeMetric(MetricsSchema metricsSchema, string measurement, string valueKey)
    {
        // Arrange
        using var influxServer = new InfluxDBFakeServer();
        var influxServerEndpoint = influxServer.Endpoint;

        using var meter = new Meter("test-meter", "0.0.1");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .ConfigureDefaultTestResource()
            .AddInfluxDBMetricsExporter(options =>
            {
                options.WithDefaultTestConfiguration();
                options.Endpoint = influxServerEndpoint;
                options.MetricsSchema = metricsSchema;
            })
            .Build();

        // Act
        _ = meter.CreateObservableGauge("test-gauge", () => new[]
        {
            new Measurement<int>(42, new("tag_key_2", "tag_value_2"), new("tag_key_1", "tag_value_1")),
        });

        var before = DateTime.UtcNow;
        provider.ForceFlush();
        var after = DateTime.UtcNow;

        // Assert
        var dataPoint = influxServer.ReadPoint();
        Assert.Equal(measurement, dataPoint.Measurement);

        Assert.Single(dataPoint.Fields);
        AssertUtils.HasField(valueKey, 42L, dataPoint.Fields);

        AssertTags(dataPoint);

        Assert.InRange(dataPoint.Timestamp, before, after);
    }

    [Theory]
    [InlineData(MetricsSchema.TelegrafPrometheusV1, "test-gauge", "gauge")]
    [InlineData(MetricsSchema.TelegrafPrometheusV2, "prometheus", "test-gauge")]
    public void ExportDoubleGaugeMetric(MetricsSchema metricsSchema, string measurement, string valueKey)
    {
        // Arrange
        using var influxServer = new InfluxDBFakeServer();
        var influxServerEndpoint = influxServer.Endpoint;

        using var meter = new Meter("test-meter", "0.0.1");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .ConfigureDefaultTestResource()
            .AddInfluxDBMetricsExporter(options =>
            {
                options.WithDefaultTestConfiguration();
                options.Endpoint = influxServerEndpoint;
                options.MetricsSchema = metricsSchema;
            })
            .Build();

        // Act
        _ = meter.CreateObservableGauge("test-gauge", () => new[]
        {
            new Measurement<double>(55.42, new("tag_key_2", "tag_value_2"), new("tag_key_1", "tag_value_1")),
        });

        var before = DateTime.UtcNow;
        provider.ForceFlush();
        var after = DateTime.UtcNow;

        // Assert
        var dataPoint = influxServer.ReadPoint();
        Assert.Equal(measurement, dataPoint.Measurement);

        Assert.Single(dataPoint.Fields);
        AssertUtils.HasField(valueKey, 55.42D, dataPoint.Fields);

        AssertTags(dataPoint);

        Assert.InRange(dataPoint.Timestamp, before, after);
    }

    [Theory]
    [InlineData(MetricsSchema.TelegrafPrometheusV1, "test-counter", "counter")]
    [InlineData(MetricsSchema.TelegrafPrometheusV2, "prometheus", "test-counter")]
    public void ExportIntSumMetric(MetricsSchema metricsSchema, string measurement, string valueKey)
    {
        // Arrange
        using var influxServer = new InfluxDBFakeServer();
        var influxServerEndpoint = influxServer.Endpoint;

        using var meter = new Meter("MyMeter", "0.0.1");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .ConfigureDefaultTestResource()
            .AddInfluxDBMetricsExporter(options =>
            {
                options.WithDefaultTestConfiguration();
                options.Endpoint = influxServerEndpoint;
                options.MetricsSchema = metricsSchema;
            })
            .Build();

        // Act
        var counter = meter.CreateCounter<int>("test-counter");
        counter.Add(100, new("tag_key_2", "tag_value_2"), new("tag_key_1", "tag_value_1"));

        var before = DateTime.UtcNow;
        provider.ForceFlush();
        var after = DateTime.UtcNow;

        // Assert
        var dataPoint = influxServer.ReadPoint();
        Assert.Equal(measurement, dataPoint.Measurement);

        Assert.Single(dataPoint.Fields);
        AssertUtils.HasField(valueKey, 100L, dataPoint.Fields);

        AssertTags(dataPoint);

        Assert.InRange(dataPoint.Timestamp, before, after);
    }

    [Theory]
    [InlineData(MetricsSchema.TelegrafPrometheusV1, "test-counter", "counter")]
    [InlineData(MetricsSchema.TelegrafPrometheusV2, "prometheus", "test-counter")]
    public void ExportDoubleSumMetric(MetricsSchema metricsSchema, string measurement, string valueKey)
    {
        // Arrange
        using var influxServer = new InfluxDBFakeServer();
        var influxServerEndpoint = influxServer.Endpoint;

        using var meter = new Meter("MyMeter", "0.0.1");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .ConfigureDefaultTestResource()
            .AddInfluxDBMetricsExporter(options =>
            {
                options.WithDefaultTestConfiguration();
                options.Endpoint = influxServerEndpoint;
                options.MetricsSchema = metricsSchema;
            })
            .Build();

        // Act
        var counter = meter.CreateCounter<double>("test-counter");
        counter.Add(12.59, new("tag_key_2", "tag_value_2"), new("tag_key_1", "tag_value_1"));

        var before = DateTime.UtcNow;
        provider.ForceFlush();
        var after = DateTime.UtcNow;

        // Assert
        var dataPoint = influxServer.ReadPoint();
        Assert.Equal(measurement, dataPoint.Measurement);

        Assert.Single(dataPoint.Fields);
        AssertUtils.HasField(valueKey, 12.59D, dataPoint.Fields);

        AssertTags(dataPoint);

        Assert.InRange(dataPoint.Timestamp, before, after);
    }

    [Theory]
    [InlineData(MetricsSchema.TelegrafPrometheusV1, "non_monotonic_sum", "gauge")]
    [InlineData(MetricsSchema.TelegrafPrometheusV2, "prometheus", "non_monotonic_sum")]
    public void ExportNonMonotonicIntSumMetric(MetricsSchema metricsSchema, string measurement, string valueKey)
    {
        // Arrange
        using var influxServer = new InfluxDBFakeServer();
        var influxServerEndpoint = influxServer.Endpoint;

        using var meter = new Meter("MyMeter", "0.0.1");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .ConfigureDefaultTestResource()
            .AddInfluxDBMetricsExporter(options =>
            {
                options.WithDefaultTestConfiguration();
                options.Endpoint = influxServerEndpoint;
                options.MetricsSchema = metricsSchema;
            })
            .Build();

        // Act
        var upDownCounter = meter.CreateUpDownCounter<int>("non_monotonic_sum");
        upDownCounter.Add(50, new("tag_key_1", "tag_value_1"), new("tag_key_2", "tag_value_2"));
        upDownCounter.Add(-100, new("tag_key_1", "tag_value_1"), new("tag_key_2", "tag_value_2"));

        var before = DateTime.UtcNow;
        provider.ForceFlush();
        var after = DateTime.UtcNow;

        // Assert
        var dataPoint = influxServer.ReadPoint();
        Assert.Equal(measurement, dataPoint.Measurement);

        Assert.Single(dataPoint.Fields);
        AssertUtils.HasField(valueKey, -50L, dataPoint.Fields);

        AssertTags(dataPoint);

        Assert.InRange(dataPoint.Timestamp, before, after);
    }

    [Theory]
    [InlineData(MetricsSchema.TelegrafPrometheusV1, "non_monotonic_sum", "gauge")]
    [InlineData(MetricsSchema.TelegrafPrometheusV2, "prometheus", "non_monotonic_sum")]
    public void ExportNonMonotonicDoubleSumMetric(MetricsSchema metricsSchema, string measurement, string valueKey)
    {
        // Arrange
        using var influxServer = new InfluxDBFakeServer();
        var influxServerEndpoint = influxServer.Endpoint;

        using var meter = new Meter("MyMeter", "0.0.1");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .ConfigureDefaultTestResource()
            .AddInfluxDBMetricsExporter(options =>
            {
                options.WithDefaultTestConfiguration();
                options.Endpoint = influxServerEndpoint;
                options.MetricsSchema = metricsSchema;
            })
            .Build();

        // Act
        var upDownCounter = meter.CreateUpDownCounter<double>("non_monotonic_sum");
        upDownCounter.Add(50.11, new("tag_key_2", "tag_value_2"), new("tag_key_1", "tag_value_1"));
        upDownCounter.Add(-100.22, new("tag_key_2", "tag_value_2"), new("tag_key_1", "tag_value_1"));

        var before = DateTime.UtcNow;
        provider.ForceFlush();
        var after = DateTime.UtcNow;

        // Assert
        var dataPoint = influxServer.ReadPoint();
        Assert.Equal(measurement, dataPoint.Measurement);

        Assert.Single(dataPoint.Fields);
        AssertUtils.HasField(valueKey, -50.11D, dataPoint.Fields);

        AssertTags(dataPoint);

        Assert.InRange(dataPoint.Timestamp, before, after);
    }

    [Fact]
    public void ExportHistogramMetricWhenTelegrafPrometheusV1MetricsSchemaUsed()
    {
        // Arrange
        using var influxServer = new InfluxDBFakeServer();
        var influxServerEndpoint = influxServer.Endpoint;

        using var meter = new Meter("MyMeter", "0.0.1");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView("histogram_metric", new ExplicitBucketHistogramConfiguration
            {
                Boundaries = TestBoundaries,
                RecordMinMax = true,
            })
            .ConfigureDefaultTestResource()
            .AddInfluxDBMetricsExporter(options =>
            {
                options.WithDefaultTestConfiguration();
                options.Endpoint = influxServerEndpoint;
                options.MetricsSchema = MetricsSchema.TelegrafPrometheusV1;
            })
            .Build();

        // Act
        var histogram = meter.CreateHistogram<int>("histogram_metric");
        histogram.Record(50, new("tag_key_2", "tag_value_2"), new("tag_key_1", "tag_value_1"));
        histogram.Record(100, new("tag_key_2", "tag_value_2"), new("tag_key_1", "tag_value_1"));
        histogram.Record(150, new("tag_key_2", "tag_value_2"), new("tag_key_1", "tag_value_1"));
        histogram.Record(250, new("tag_key_2", "tag_value_2"), new("tag_key_1", "tag_value_1"));

        var before = DateTime.UtcNow;
        provider.ForceFlush();
        var after = DateTime.UtcNow;

        // Assert
        var dataPoint = influxServer.ReadPoint();
        Assert.Equal("histogram_metric", dataPoint.Measurement);

        Assert.Equal(9, dataPoint.Fields.Count);
        AssertUtils.HasField("count", 4L, dataPoint.Fields);
        AssertUtils.HasField("sum", 550D, dataPoint.Fields);
        AssertUtils.HasField("min", 50D, dataPoint.Fields);
        AssertUtils.HasField("max", 250D, dataPoint.Fields);
        AssertUtils.HasField("10.00", 0L, dataPoint.Fields);
        AssertUtils.HasField("20.00", 0L, dataPoint.Fields);
        AssertUtils.HasField("100.00", 2L, dataPoint.Fields);
        AssertUtils.HasField("200.00", 1L, dataPoint.Fields);
        AssertUtils.HasField("+Inf", 1L, dataPoint.Fields);

        AssertTags(dataPoint);

        Assert.InRange(dataPoint.Timestamp, before, after);
    }

    [Fact]
    public void ExportHistogramMetricWithoutMinMaxFieldsWhenTelegrafPrometheusV1MetricsSchemaUsed()
    {
        // Arrange
        using var influxServer = new InfluxDBFakeServer();
        var influxServerEndpoint = influxServer.Endpoint;

        using var meter = new Meter("MyMeter", "0.0.1");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView("histogram_metric", new ExplicitBucketHistogramConfiguration
            {
                RecordMinMax = false,
            })
            .AddInfluxDBMetricsExporter(options =>
            {
                options.WithDefaultTestConfiguration();
                options.Endpoint = influxServerEndpoint;
                options.MetricsSchema = MetricsSchema.TelegrafPrometheusV1;
            })
            .Build();

        // Act
        var histogram = meter.CreateHistogram<int>("histogram_metric");
        histogram.Record(50);
        provider.ForceFlush();

        // Assert
        var dataPoint = influxServer.ReadPoint();
        Assert.Equal("histogram_metric", dataPoint.Measurement);
        Assert.DoesNotContain("min", dataPoint.Fields);
        Assert.DoesNotContain("max", dataPoint.Fields);
    }

    [Fact]
    public void ExportHistogramMetricWhenTelegrafPrometheusV2MetricsSchemaUsed()
    {
        // Arrange
        using var influxServer = new InfluxDBFakeServer();
        var influxServerEndpoint = influxServer.Endpoint;

        using var meter = new Meter("MyMeter", "0.0.1");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView("histogram_metric", new ExplicitBucketHistogramConfiguration
            {
                Boundaries = TestBoundaries,
                RecordMinMax = true,
            })
            .ConfigureDefaultTestResource()
            .AddInfluxDBMetricsExporter(options =>
            {
                options.Endpoint = influxServerEndpoint;
                options.MetricsSchema = MetricsSchema.TelegrafPrometheusV2;
                options.WithDefaultTestConfiguration();
            })
            .Build();

        // Act
        var histogram = meter.CreateHistogram<int>("histogram_metric");
        histogram.Record(50, new("tag_key_2", "tag_value_2"), new("tag_key_1", "tag_value_1"));
        histogram.Record(100, new("tag_key_2", "tag_value_2"), new("tag_key_1", "tag_value_1"));
        histogram.Record(150, new("tag_key_2", "tag_value_2"), new("tag_key_1", "tag_value_1"));
        histogram.Record(250, new("tag_key_2", "tag_value_2"), new("tag_key_1", "tag_value_1"));

        var before = DateTime.UtcNow;
        provider.ForceFlush();
        var after = DateTime.UtcNow;

        // Assert
        var headDataPoint = influxServer.ReadPoint();
        Assert.Equal("prometheus", headDataPoint.Measurement);
        Assert.Equal(4, headDataPoint.Fields.Count);
        AssertUtils.HasField("histogram_metric_count", 4L, headDataPoint.Fields);
        AssertUtils.HasField("histogram_metric_sum", 550D, headDataPoint.Fields);
        AssertUtils.HasField("histogram_metric_min", 50D, headDataPoint.Fields);
        AssertUtils.HasField("histogram_metric_max", 250D, headDataPoint.Fields);

        AssertTags(headDataPoint);
        Assert.InRange(headDataPoint.Timestamp, before, after);

        AssertBucketDataPoint(influxServer.ReadPoint(), "10.00", 0);
        AssertBucketDataPoint(influxServer.ReadPoint(), "20.00", 0);
        AssertBucketDataPoint(influxServer.ReadPoint(), "100.00", 2);
        AssertBucketDataPoint(influxServer.ReadPoint(), "200.00", 3);
        AssertBucketDataPoint(influxServer.ReadPoint(), "+Inf", 4);

        static void AssertBucketDataPoint(PointData dataPoint, string bound, long count)
        {
            Assert.Equal("prometheus", dataPoint.Measurement);
            AssertUtils.HasField("histogram_metric_bucket", count, dataPoint.Fields);
            AssertUtils.HasTag("le", bound, 0, dataPoint.Tags);
            AssertUtils.HasTag("service.instance.id", "my-service-id", 1, dataPoint.Tags);
            AssertUtils.HasTag("service.name", "my-service", 2, dataPoint.Tags);
            AssertUtils.HasTag("service.namespace", "my-service-namespace", 3, dataPoint.Tags);
            AssertUtils.HasTag("service.version", "1.0", 4, dataPoint.Tags);
            AssertUtils.HasTag("tag_key_1", "tag_value_1", 5, dataPoint.Tags);
            AssertUtils.HasTag("tag_key_2", "tag_value_2", 6, dataPoint.Tags);
            AssertUtils.HasTag("telemetry.sdk.language", "dotnet", 7, dataPoint.Tags);
            AssertUtils.HasTag("telemetry.sdk.name", "opentelemetry", 8, dataPoint.Tags);
            AssertUtils.HasTag("telemetry.sdk.version", OpenTelemetrySdkVersion, 9, dataPoint.Tags);
        }
    }

    [Fact]
    public void ExportHistogramMetricWithoutMinMaxFieldsWhenTelegrafPrometheusV2MetricsSchemaUsed()
    {
        // Arrange
        using var influxServer = new InfluxDBFakeServer();
        var influxServerEndpoint = influxServer.Endpoint;

        using var meter = new Meter("MyMeter", "0.0.1");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView("histogram_metric", new ExplicitBucketHistogramConfiguration
            {
                RecordMinMax = false,
            })
            .AddInfluxDBMetricsExporter(options =>
            {
                options.WithDefaultTestConfiguration();
                options.Endpoint = influxServerEndpoint;
                options.MetricsSchema = MetricsSchema.TelegrafPrometheusV2;
            })
            .Build();

        // Act
        var histogram = meter.CreateHistogram<int>("histogram_metric");
        histogram.Record(50);
        provider.ForceFlush();

        // Assert
        var pointData = influxServer.ReadPoint();
        Assert.Equal("prometheus", pointData.Measurement);
        Assert.DoesNotContain("histogram_metric_min", pointData.Fields);
        Assert.DoesNotContain("histogram_metric_max", pointData.Fields);
    }

    [Theory]
    [InlineData(MetricsSchema.TelegrafPrometheusV1, "test-gauge", "gauge")]
    [InlineData(MetricsSchema.TelegrafPrometheusV2, "prometheus", "test-gauge")]
    public void ExportMetricWithMeterTags(MetricsSchema metricsSchema, string measurement, string valueKey)
    {
        // Arrange
        using var influxServer = new InfluxDBFakeServer();
        var influxServerEndpoint = influxServer.Endpoint;

        using var meter = new Meter(
            "test-meter-with-tags",
            "1.0",
            new List<KeyValuePair<string, object?>> { new("MeterTag", "MeterValue") });

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .ConfigureDefaultTestResource()
            .AddInfluxDBMetricsExporter(options =>
            {
                options.WithDefaultTestConfiguration();
                options.Endpoint = influxServerEndpoint;
                options.MetricsSchema = metricsSchema;
            })
            .Build();

        // Act
        _ = meter.CreateObservableGauge("test-gauge", () => new[]
        {
            new Measurement<int>(42),
        });

        provider.ForceFlush();

        // Assert
        var dataPoint = influxServer.ReadPoint();
        Assert.Equal(measurement, dataPoint.Measurement);
        AssertUtils.HasField(valueKey, 42L, dataPoint.Fields);
        AssertUtils.HasTag("MeterTag", "MeterValue", 0, dataPoint.Tags);
    }

    [Fact]
    public void ExportMetricWhenBackpressureHandlingIsEnabled()
    {
        // Arrange
        using var influxServer = new InfluxDBFakeServer();
        var influxServerEndpoint = influxServer.Endpoint;

        using var meter = new Meter("test-meter", "0.0.1");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .ConfigureDefaultTestResource()
            .AddInfluxDBMetricsExporter(options =>
            {
                options.WithDefaultTestConfiguration();
                options.Endpoint = influxServerEndpoint;
                options.MaxPendingExports = 1;
                options.BackpressureMode = BackpressureMode.Wait;
            })
            .Build();

        // Act
        _ = meter.CreateObservableGauge("test-gauge", () => new[]
        {
            new Measurement<int>(42, new("tag_key_2", "tag_value_2"), new("tag_key_1", "tag_value_1")),
        });

        var before = DateTime.UtcNow;
        provider.ForceFlush();
        var after = DateTime.UtcNow;

        // Assert
        var dataPoint = influxServer.ReadPoint();
        Assert.Equal("test-gauge", dataPoint.Measurement);
        Assert.Single(dataPoint.Fields);
        AssertUtils.HasField("gauge", 42L, dataPoint.Fields);
        AssertTags(dataPoint);
        Assert.InRange(dataPoint.Timestamp, before, after);
    }

    [Fact]
    public async Task ExportReturnsSuccessWhenDropNewestDropsCurrentPayload()
    {
        using var gate = new ManualResetEventSlim(false);
        using var payloadWriter = new BlockingPayloadWriter(gate);
        using var batch = CreateBatchWithSingleMetric();

        var options = new InfluxDBMetricsExporterOptions
        {
            MaxPendingExports = 1,
            BackpressureMode = BackpressureMode.DropNewest,
        };

        using var exporter = new InfluxDBMetricsExporter(
            new TestMetricsWriter(),
            new InfluxDBClient(new InfluxDBClientOptions("http://localhost:8086")),
            writeApi: null,
            writeApiAsync: null,
            options,
            payloadWriter);

        Assert.Equal(ExportResult.Success, exporter.Export(batch.Batch));
        await payloadWriter.WaitForFirstWriteAsync();

        Assert.Equal(ExportResult.Success, exporter.Export(batch.Batch));

        gate.Set();
        Assert.True(exporter.ForceFlush());
    }

    [Fact]
    public async Task ForceFlushReturnsFalseWhenBackgroundWriteFails()
    {
        using var payloadWriter = new ThrowingPayloadWriter();
        using var batch = CreateBatchWithSingleMetric();

        var options = new InfluxDBMetricsExporterOptions
        {
            MaxPendingExports = 1,
            BackpressureMode = BackpressureMode.Wait,
        };

        using var exporter = new InfluxDBMetricsExporter(
            new TestMetricsWriter(),
            new InfluxDBClient(new InfluxDBClientOptions("http://localhost:8086")),
            writeApi: null,
            writeApiAsync: null,
            options,
            payloadWriter);

        Assert.Equal(ExportResult.Success, exporter.Export(batch.Batch));
        await payloadWriter.WaitForWriteAttemptAsync();

        Assert.False(exporter.ForceFlush());
        Assert.True(exporter.ForceFlush());
    }

    private static void AssertTags(PointData dataPoint)
    {
        Assert.Equal(9, dataPoint.Tags.Count);
        AssertUtils.HasTag("service.instance.id", "my-service-id", 0, dataPoint.Tags);
        AssertUtils.HasTag("service.name", "my-service", 1, dataPoint.Tags);
        AssertUtils.HasTag("service.namespace", "my-service-namespace", 2, dataPoint.Tags);
        AssertUtils.HasTag("service.version", "1.0", 3, dataPoint.Tags);
        AssertUtils.HasTag("tag_key_1", "tag_value_1", 4, dataPoint.Tags);
        AssertUtils.HasTag("tag_key_2", "tag_value_2", 5, dataPoint.Tags);
        AssertUtils.HasTag("telemetry.sdk.language", "dotnet", 6, dataPoint.Tags);
        AssertUtils.HasTag("telemetry.sdk.name", "opentelemetry", 7, dataPoint.Tags);
        AssertUtils.HasTag("telemetry.sdk.version", OpenTelemetrySdkVersion, 8, dataPoint.Tags);
    }

    private static CapturedBatch CreateBatchWithSingleMetric()
    {
        var meter = new Meter("InfluxDBMetricsExporterTests.BatchGenerator", "0.0.1");
        var counter = meter.CreateCounter<long>("test-counter");

        var batchGeneratorExporter = new BatchGenerator();
        var batchGeneratorReader = new BaseExportingMetricReader(batchGeneratorExporter);
        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddReader(batchGeneratorReader)
            .Build();

        counter.Add(1);
        meterProvider.ForceFlush();

        return new CapturedBatch(meter, meterProvider, batchGeneratorExporter.Batch);
    }

    private sealed class BatchGenerator : BaseExporter<Metric>
    {
        public Batch<Metric> Batch { get; private set; }

        public override ExportResult Export(in Batch<Metric> batch)
        {
            this.Batch = batch;
            return ExportResult.Success;
        }
    }

    private sealed class CapturedBatch : IDisposable
    {
        private readonly Meter meter;
        private readonly MeterProvider meterProvider;

        public CapturedBatch(Meter meter, MeterProvider meterProvider, Batch<Metric> batch)
        {
            this.meter = meter;
            this.meterProvider = meterProvider;
            this.Batch = batch;
        }

        public Batch<Metric> Batch { get; }

        public void Dispose()
        {
            this.meterProvider.Dispose();
            this.meter.Dispose();
        }
    }

    private sealed class TestMetricsWriter : IMetricsWriter
    {
        public void Write(Metric metric, OpenTelemetry.Resources.Resource? resource, ICollection<string> lineProtocol)
        {
            lineProtocol.Add("test-counter counter=1i");
        }
    }

    private sealed class BlockingPayloadWriter : IInfluxDBExportPayloadWriter, IDisposable
    {
        private readonly ManualResetEventSlim gate;
        private readonly TaskCompletionSource<bool> firstWriteStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingPayloadWriter(ManualResetEventSlim gate)
        {
            this.gate = gate;
        }

        public void Dispose()
        {
        }

        public Task WaitForFirstWriteAsync() => this.firstWriteStarted.Task;

        public Task WriteAsync(IReadOnlyCollection<string> lineProtocol, CancellationToken cancellationToken)
        {
            this.firstWriteStarted.TrySetResult(true);
            this.gate.Wait(cancellationToken);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPayloadWriter : IInfluxDBExportPayloadWriter, IDisposable
    {
        private readonly TaskCompletionSource<bool> writeAttempted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Dispose()
        {
        }

        public Task WaitForWriteAttemptAsync() => this.writeAttempted.Task;

        public Task WriteAsync(IReadOnlyCollection<string> lineProtocol, CancellationToken cancellationToken)
        {
            this.writeAttempted.TrySetResult(true);
            throw new InvalidOperationException("Simulated export failure.");
        }
    }
}
