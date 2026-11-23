// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.AzureDataExplorer.Tests;

/// <summary>
/// End-to-end scenarios shared by the Kusto emulator and live cluster tests.
/// Telemetry is emitted through the public builder extensions and then read
/// back from Kusto to verify the schema, types and values that were ingested.
/// </summary>
internal static class IntegrationScenarios
{
    public static string NewServiceName() => "adx-it-" + Guid.NewGuid().ToString("N");

    public static async Task ExportAndVerifyTracesAsync(
        KustoTestClient client,
        Action<AzureDataExplorerExporterOptions> configure,
        TimeSpan timeout,
        TimeSpan pollInterval)
    {
        var serviceName = NewServiceName();
        using var source = new ActivitySource(serviceName);
        ActivityTraceId traceId;
        ActivitySpanId parentId;
        ActivitySpanId childId;

        using (Sdk.CreateTracerProviderBuilder()
            .ConfigureResource(r => r.Clear().AddService(serviceName))
            .AddSource(serviceName)
            .AddAzureDataExplorerExporter(configure)
            .Build())
        {
            using var parent = source.StartActivity("parent", ActivityKind.Server)!;
            traceId = parent.TraceId;
            parentId = parent.SpanId;

            using var child = source.StartActivity("child", ActivityKind.Client)!;
            childId = child.SpanId;
            child.SetTag("http.request.method", "GET");
            child.SetTag("retry.count", 3);
            child.AddEvent(new ActivityEvent("checkpoint", tags: new ActivityTagsCollection { ["stage"] = "one" }));
            child.SetStatus(ActivityStatusCode.Error, "boom");
        }

        var rows = await client.WaitForRowsAsync(
            $$"""
            OTELTraces
            | where tostring(ResourceAttributes['service.name']) == {{KustoTestClient.Quote(serviceName)}}
            | project TraceID, SpanID, ParentID, SpanName, SpanKind, SpanStatus, SpanStatusMessage, StartTime, EndTime,
                Method = tostring(TraceAttributes['http.request.method']),
                RetryCount = tolong(TraceAttributes['retry.count']),
                EventName = tostring(Events[0].EventName),
                EventStage = tostring(Events[0].EventAttributes.stage),
                LinkCount = array_length(Links)
            """,
            expectedCount: 2,
            timeout,
            pollInterval,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, rows.Count);

        var parentRow = Assert.Single(rows, r => (string?)r["SpanName"] == "parent");
        Assert.Equal(traceId.ToHexString(), parentRow["TraceID"]);
        Assert.Equal(parentId.ToHexString(), parentRow["SpanID"]);
        Assert.Equal(string.Empty, parentRow["ParentID"]);
        Assert.Equal("SPAN_KIND_SERVER", parentRow["SpanKind"]);
        Assert.Equal("STATUS_CODE_UNSET", parentRow["SpanStatus"]);
        Assert.Equal(0L, parentRow["LinkCount"]);

        var childRow = Assert.Single(rows, r => (string?)r["SpanName"] == "child");
        Assert.Equal(traceId.ToHexString(), childRow["TraceID"]);
        Assert.Equal(childId.ToHexString(), childRow["SpanID"]);
        Assert.Equal(parentId.ToHexString(), childRow["ParentID"]);
        Assert.Equal("SPAN_KIND_CLIENT", childRow["SpanKind"]);
        Assert.Equal("STATUS_CODE_ERROR", childRow["SpanStatus"]);
        Assert.Equal("boom", childRow["SpanStatusMessage"]);
        Assert.Equal("GET", childRow["Method"]);
        Assert.Equal(3L, childRow["RetryCount"]);
        Assert.Equal("checkpoint", childRow["EventName"]);
        Assert.Equal("one", childRow["EventStage"]);

        var start = Assert.IsType<DateTime>(childRow["StartTime"]);
        var end = Assert.IsType<DateTime>(childRow["EndTime"]);
        Assert.True(end >= start);
        Assert.True(Math.Abs((DateTime.UtcNow - start).TotalHours) < 1, $"Unexpected StartTime {start:O}.");
    }

    public static async Task ExportAndVerifyMetricsAsync(
        KustoTestClient client,
        Action<AzureDataExplorerExporterOptions> configure,
        TimeSpan timeout,
        TimeSpan pollInterval)
    {
        var serviceName = NewServiceName();
        using var meter = new Meter(serviceName);

        using (Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(r => r.Clear().AddService(serviceName))
            .AddMeter(serviceName)
            .AddView("adx.it.latency", new ExplicitBucketHistogramConfiguration { Boundaries = [10, 100] })
            .AddAzureDataExplorerExporter(configure)
            .Build())
        {
            var counter = meter.CreateCounter<long>("adx.it.requests", unit: "{request}", description: "Requests");
            counter.Add(5, new KeyValuePair<string, object?>("route", "/home"));

            var histogram = meter.CreateHistogram<double>("adx.it.latency", unit: "ms", description: "Latency");
            histogram.Record(5);
            histogram.Record(50);
        }

        var rows = await client.WaitForRowsAsync(
            $$"""
            OTELMetrics
            | where tostring(ResourceAttributes['service.name']) == {{KustoTestClient.Quote(serviceName)}}
            | project MetricName, MetricType, MetricUnit, MetricDescription, MetricValue, Timestamp,
                Route = tostring(MetricAttributes['route']),
                Le = tostring(MetricAttributes['le'])
            """,
            expectedCount: 8,
            timeout,
            pollInterval,
            TestContext.Current.CancellationToken);

        var sum = Assert.Single(rows, r => (string?)r["MetricName"] == "adx.it.requests");
        Assert.Equal("Sum", sum["MetricType"]);
        Assert.Equal("{request}", sum["MetricUnit"]);
        Assert.Equal("Requests", sum["MetricDescription"]);
        Assert.Equal(5d, sum["MetricValue"]);
        Assert.Equal("/home", sum["Route"]);
        Assert.IsType<DateTime>(sum["Timestamp"]);

        double Value(string name) => (double)Assert.Single(rows, r => (string?)r["MetricName"] == name)["MetricValue"]!;

        Assert.Equal(2d, Value("adx.it.latency_count"));
        Assert.Equal(55d, Value("adx.it.latency_sum"));
        Assert.Equal(5d, Value("adx.it.latency_min"));
        Assert.Equal(50d, Value("adx.it.latency_max"));

        var buckets = rows
            .Where(r => (string?)r["MetricName"] == "adx.it.latency_bucket")
            .ToDictionary(r => (string)r["Le"]!, r => (double)r["MetricValue"]!);
        Assert.Equal(1d, buckets["10"]);
        Assert.Equal(2d, buckets["100"]);
        Assert.Equal(2d, buckets["+Inf"]);
        Assert.All(rows.Where(r => ((string)r["MetricName"]!).StartsWith("adx.it.latency", StringComparison.Ordinal)), r => Assert.Equal("Histogram", r["MetricType"]));
    }

    public static async Task ExportAndVerifyLogsAsync(
        KustoTestClient client,
        Action<AzureDataExplorerExporterOptions> configure,
        TimeSpan timeout,
        TimeSpan pollInterval)
    {
        var serviceName = NewServiceName();
        ActivityTraceId traceId;
        ActivitySpanId spanId;

        using (var serviceProvider = CreateLoggingServiceProvider(serviceName, configure))
        {
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AdxIntegration");

            using var activity = new Activity("log-parent").Start();
            traceId = activity.TraceId;
            spanId = activity.SpanId;

            logger.LogWarning(new EventId(7, "Greeting"), "Hello {Name}", "Kusto");
            logger.LogError(new InvalidOperationException("bad"), "Failed");
        }

        var rows = await client.WaitForRowsAsync(
            $$"""
            OTELLogs
            | where tostring(ResourceAttributes['service.name']) == {{KustoTestClient.Quote(serviceName)}}
            | project Timestamp, ObservedTimestamp, TraceID, SpanID, SeverityText, SeverityNumber, Body,
                Name = tostring(LogsAttributes['Name']),
                EventId = tolong(LogsAttributes['event.id']),
                ExceptionType = tostring(LogsAttributes['exception.type'])
            """,
            expectedCount: 2,
            timeout,
            pollInterval,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, rows.Count);

        var warning = Assert.Single(rows, r => (string?)r["SeverityText"] == "Warn");
        Assert.Equal(13, Convert.ToInt32(warning["SeverityNumber"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("Hello Kusto", warning["Body"]);
        Assert.Equal("Kusto", warning["Name"]);
        Assert.Equal(7L, warning["EventId"]);
        Assert.Equal(traceId.ToHexString(), warning["TraceID"]);
        Assert.Equal(spanId.ToHexString(), warning["SpanID"]);
        Assert.IsType<DateTime>(warning["Timestamp"]);
        Assert.Equal(warning["Timestamp"], warning["ObservedTimestamp"]);

        var error = Assert.Single(rows, r => (string?)r["SeverityText"] == "Error");
        Assert.Equal("Failed", error["Body"]);
        Assert.Equal(typeof(InvalidOperationException).FullName, error["ExceptionType"]);
    }

    public static ServiceProvider CreateLoggingServiceProvider(string serviceName, Action<AzureDataExplorerExporterOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace));
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.Clear().AddService(serviceName))
            .WithLogging(
                builder => builder.AddAzureDataExplorerExporter(configure),
                options => options.IncludeFormattedMessage = true);

        return services.BuildServiceProvider();
    }
}
