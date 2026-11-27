// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.AzureDataExplorer.Tests;

public class AzureDataExplorerMetricExporterTests
{
    [Fact]
    public void ExportsSumAndGaugeMetrics()
    {
        var ingestor = new TestIngestor();
        var meterName = Guid.NewGuid().ToString("N");
        using var meter = new Meter(meterName, "1.0.0");

        using (CreateMeterProvider(meterName, ingestor, ResourceBuilder.CreateEmpty().AddAttributes([new("host.name", "my-host")])))
        {
            var counter = meter.CreateCounter<long>("requests", "{request}", "Request count");
            counter.Add(5, new KeyValuePair<string, object?>("route", "/home"), new KeyValuePair<string, object?>("scope.version", "point"));
            meter.CreateObservableGauge("temperature", () => 21.5d);
        }

        Assert.Equal("OTELMetrics", ingestor.TableName);
        Assert.Equal(1, ingestor.DisposeCount);

        var rows = ingestor.Rows.ToList();
        var sum = Assert.Single(rows, r => r.GetProperty("MetricName").GetString() == "requests");
        Assert.Equal("Sum", sum.GetProperty("MetricType").GetString());
        Assert.Equal("{request}", sum.GetProperty("MetricUnit").GetString());
        Assert.Equal("Request count", sum.GetProperty("MetricDescription").GetString());
        Assert.Equal(5d, sum.GetProperty("MetricValue").GetDouble());
        Assert.Equal("my-host", sum.GetProperty("Host").GetString());
        Assert.Equal("my-host", sum.GetProperty("ResourceAttributes").GetProperty("host.name").GetString());

        var attributes = sum.GetProperty("MetricAttributes");
        Assert.Equal(meterName, attributes.GetProperty("scope.name").GetString());

        // Point attributes override scope attributes, as in the Collector exporter.
        Assert.Equal("point", attributes.GetProperty("scope.version").GetString());
        Assert.Equal("/home", attributes.GetProperty("route").GetString());
        Assert.False(sum.TryGetProperty("Exemplars", out _));

        var gauge = Assert.Single(rows, r => r.GetProperty("MetricName").GetString() == "temperature");
        Assert.Equal("Gauge", gauge.GetProperty("MetricType").GetString());
        Assert.Equal(21.5d, gauge.GetProperty("MetricValue").GetDouble());
        Assert.Equal("1.0.0", gauge.GetProperty("MetricAttributes").GetProperty("scope.version").GetString());
    }

    [Fact]
    public void HostFallsBackToMachineName()
    {
        var ingestor = new TestIngestor();
        var meterName = Guid.NewGuid().ToString("N");
        using var meter = new Meter(meterName);

        using (CreateMeterProvider(meterName, ingestor, ResourceBuilder.CreateEmpty()))
        {
            meter.CreateCounter<long>("c").Add(1);
        }

        Assert.Equal(Environment.MachineName, Assert.Single(ingestor.Rows).GetProperty("Host").GetString());
    }

    [Fact]
    public void ExportsHistogramsAsPrometheusStyleRows()
    {
        var ingestor = new TestIngestor();
        var meterName = Guid.NewGuid().ToString("N");
        using var meter = new Meter(meterName);

        using (Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
            .AddView("latency", new ExplicitBucketHistogramConfiguration { Boundaries = [1, 2.5, 1000000] })
            .AddAzureDataExplorerExporter(o => TestIngestor.Configure(o, ingestor))
            .Build())
        {
            var histogram = meter.CreateHistogram<double>("latency", "s", "Latency");
            histogram.Record(0.5, new KeyValuePair<string, object?>("route", "/a"));
            histogram.Record(2, new KeyValuePair<string, object?>("route", "/a"));
            histogram.Record(5000000, new KeyValuePair<string, object?>("route", "/a"));
        }

        var rows = ingestor.Rows.ToList();
        Assert.All(rows, r => Assert.Equal("Histogram", r.GetProperty("MetricType").GetString()));
        Assert.All(rows, r => Assert.Equal("/a", r.GetProperty("MetricAttributes").GetProperty("route").GetString()));

        Assert.Equal(
            ["latency_sum", "latency_count", "latency_min", "latency_max", "latency_bucket", "latency_bucket", "latency_bucket", "latency_bucket"],
            rows.Select(r => r.GetProperty("MetricName").GetString()));

        Assert.Equal(5000002.5d, rows[0].GetProperty("MetricValue").GetDouble());

        // The description suffix is appended without a separator, as in the Collector exporter.
        Assert.Equal("Latency(Sum total of samples)", rows[0].GetProperty("MetricDescription").GetString());
        Assert.Equal(3d, rows[1].GetProperty("MetricValue").GetDouble());
        Assert.Equal("Latency(Count of samples)", rows[1].GetProperty("MetricDescription").GetString());
        Assert.Equal(0.5d, rows[2].GetProperty("MetricValue").GetDouble());
        Assert.Equal("Latency(Minimum value observed)", rows[2].GetProperty("MetricDescription").GetString());
        Assert.Equal(5000000d, rows[3].GetProperty("MetricValue").GetDouble());
        Assert.Equal("Latency(Maximum value observed)", rows[3].GetProperty("MetricDescription").GetString());

        // Exemplars are only attached to the _count row.
        Assert.Equal(3, rows[1].GetProperty("Exemplars").GetArrayLength());
        Assert.All(rows.Where((_, i) => i != 1), r => Assert.False(r.TryGetProperty("Exemplars", out _)));

        var buckets = rows.Skip(4).ToList();
        Assert.Equal(["1", "2.5", "1e+06", "+Inf"], buckets.Select(b => b.GetProperty("MetricAttributes").GetProperty("le").GetString()));
        Assert.Equal([1d, 2d, 2d, 3d], buckets.Select(b => b.GetProperty("MetricValue").GetDouble()));
        Assert.All(buckets, b => Assert.Equal("Latency", b.GetProperty("MetricDescription").GetString()));
    }

    [Fact]
    public void ExponentialHistogramsAreSkippedWithoutFailingTheBatch()
    {
        var ingestor = new TestIngestor();
        var meterName = Guid.NewGuid().ToString("N");
        using var meter = new Meter(meterName);

        using (Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddView("latency", new Base2ExponentialBucketHistogramConfiguration())
            .AddAzureDataExplorerExporter(o => TestIngestor.Configure(o, ingestor))
            .Build())
        {
            meter.CreateHistogram<double>("latency").Record(1.5);
            meter.CreateCounter<long>("requests").Add(1);
        }

        var row = Assert.Single(ingestor.Rows);
        Assert.Equal("requests", row.GetProperty("MetricName").GetString());
    }

    [Fact]
    public void ExemplarsAreWrittenForSums()
    {
        var ingestor = new TestIngestor();
        var meterName = Guid.NewGuid().ToString("N");
        using var meter = new Meter(meterName);

        using (Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
            .AddAzureDataExplorerExporter(o => TestIngestor.Configure(o, ingestor))
            .Build())
        {
            meter.CreateCounter<long>("c").Add(7);
        }

        var exemplar = Assert.Single(Assert.Single(ingestor.Rows).GetProperty("Exemplars").EnumerateArray());
        Assert.Equal(7, exemplar.GetProperty("value").GetInt64());
        Assert.Equal(string.Empty, exemplar.GetProperty("trace_id").GetString());
        Assert.Equal(string.Empty, exemplar.GetProperty("span_id").GetString());
        Assert.Equal(JsonValueKind.Object, exemplar.GetProperty("attributes").ValueKind);
        Assert.EndsWith("Z", exemplar.GetProperty("timestamp").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void NonFiniteValuesAreDropped()
    {
        var ingestor = new TestIngestor();
        var meterName = Guid.NewGuid().ToString("N");
        using var meter = new Meter(meterName);

        using (CreateMeterProvider(meterName, ingestor, ResourceBuilder.CreateEmpty()))
        {
            meter.CreateObservableGauge("nan", () => double.NaN);
            meter.CreateObservableGauge("finite", () => 1d);
        }

        Assert.Equal("finite", Assert.Single(ingestor.Rows).GetProperty("MetricName").GetString());
    }

    [Fact]
    public void DeltaTemporalityIsHonored()
    {
        var ingestor = new TestIngestor();
        var meterName = Guid.NewGuid().ToString("N");
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("c");

        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddAzureDataExplorerExporter(
                o => TestIngestor.Configure(o, ingestor))
            .ConfigureServices(s => s.Configure<MetricReaderOptions>(o => o.TemporalityPreference = MetricReaderTemporalityPreference.Delta))
            .Build())
        {
            counter.Add(2);
            meterProvider.ForceFlush();
            counter.Add(3);
        }

        Assert.Equal([2d, 3d], ingestor.Rows.Select(r => r.GetProperty("MetricValue").GetDouble()));
    }

    private static MeterProvider CreateMeterProvider(string meterName, TestIngestor ingestor, ResourceBuilder resourceBuilder)
        => Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(meterName)
            .AddAzureDataExplorerExporter(o => TestIngestor.Configure(o, ingestor))
            .Build();
}
