// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.AzureDataExplorer.Tests;

public class AzureDataExplorerTraceExporterTests
{
    [Fact]
    public void ExportsSpansUsingCollectorSchema()
    {
        var sourceName = Guid.NewGuid().ToString("N");
        var ingestor = new TestIngestor();
        using var source = new ActivitySource(sourceName, "1.2.3");
        var linkedContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService("my-service").AddAttributes([new("host.name", "host-1")]))
            .AddSource(sourceName)
            .AddAzureDataExplorerExporter(o =>
            {
                TestIngestor.Configure(o, ingestor);
                o.TracesTableName = "Spans";
                o.TracesTableJsonMapping = "span_mapping";
            })
            .Build())
        {
            using (source.StartActivity("parent", ActivityKind.Server))
            {
                using var childActivity = source.StartActivity(
                    ActivityKind.Client,
                    tags: [new("http.method", "GET"), new("scope.name", "overridden")],
                    links: [new ActivityLink(linkedContext, new ActivityTagsCollection { ["link.attr"] = 1 })],
                    name: "child");
                childActivity!.AddEvent(new ActivityEvent("event-1", new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero), new ActivityTagsCollection { ["event.attr"] = true }));
                childActivity.SetStatus(ActivityStatusCode.Error, "boom");
            }

            tracerProvider.ForceFlush();
        }

        Assert.Equal("Spans", ingestor.TableName);
        Assert.Equal("span_mapping", ingestor.Mapping);
        Assert.Equal(1, ingestor.DisposeCount);

        var rows = ingestor.Rows.ToList();
        Assert.Equal(2, rows.Count);

        var child = rows[0];
        var parent = rows[1];

        Assert.Equal("child", child.GetProperty("SpanName").GetString());
        Assert.Equal(parent.GetProperty("TraceID").GetString(), child.GetProperty("TraceID").GetString());
        Assert.Equal(parent.GetProperty("SpanID").GetString(), child.GetProperty("ParentID").GetString());
        Assert.Equal(string.Empty, parent.GetProperty("ParentID").GetString());
        Assert.Equal(32, child.GetProperty("TraceID").GetString()!.Length);
        Assert.Equal(16, child.GetProperty("SpanID").GetString()!.Length);
        Assert.Equal("STATUS_CODE_ERROR", child.GetProperty("SpanStatus").GetString());
        Assert.Equal("boom", child.GetProperty("SpanStatusMessage").GetString());
        Assert.Equal("SPAN_KIND_CLIENT", child.GetProperty("SpanKind").GetString());
        Assert.Equal("SPAN_KIND_SERVER", parent.GetProperty("SpanKind").GetString());
        Assert.Equal("STATUS_CODE_UNSET", parent.GetProperty("SpanStatus").GetString());
        Assert.EndsWith("Z", child.GetProperty("StartTime").GetString(), StringComparison.Ordinal);
        Assert.EndsWith("Z", child.GetProperty("EndTime").GetString(), StringComparison.Ordinal);

        var resource = child.GetProperty("ResourceAttributes");
        Assert.Equal("my-service", resource.GetProperty("service.name").GetString());
        Assert.Equal("host-1", resource.GetProperty("host.name").GetString());

        var attributes = child.GetProperty("TraceAttributes");
        Assert.Equal("GET", attributes.GetProperty("http.method").GetString());

        // Scope attributes override span attributes, as in the Collector exporter.
        Assert.Equal(sourceName, attributes.GetProperty("scope.name").GetString());
        Assert.Equal("1.2.3", attributes.GetProperty("scope.version").GetString());

        var spanEvent = Assert.Single(child.GetProperty("Events").EnumerateArray());
        Assert.Equal("event-1", spanEvent.GetProperty("EventName").GetString());
        Assert.Equal("2024-01-02T03:04:05Z", spanEvent.GetProperty("Timestamp").GetString());
        Assert.True(spanEvent.GetProperty("EventAttributes").GetProperty("event.attr").GetBoolean());

        var link = Assert.Single(child.GetProperty("Links").EnumerateArray());
        Assert.Equal(linkedContext.TraceId.ToHexString(), link.GetProperty("TraceID").GetString());
        Assert.Equal(linkedContext.SpanId.ToHexString(), link.GetProperty("SpanID").GetString());
        Assert.Equal(1, link.GetProperty("SpanLinkAttributes").GetProperty("link.attr").GetInt32());

        Assert.Empty(parent.GetProperty("Events").EnumerateArray());
        Assert.Empty(parent.GetProperty("Links").EnumerateArray());
    }

    [Theory]
    [InlineData("ERROR", "legacy failure", "STATUS_CODE_ERROR", "legacy failure")]
    [InlineData("OK", null, "STATUS_CODE_OK", "")]
    public void LegacyStatusTagsAreMapped(string code, string? description, string expectedCode, string expectedMessage)
    {
        using var activity = new Activity("legacy");
        activity.SetTag("otel.status_code", code);
        activity.SetTag("otel.status_description", description);

        var (statusCode, statusMessage) = AdxTraceSerializer.GetStatus(activity);

        Assert.Equal(expectedCode, statusCode);
        Assert.Equal(expectedMessage, statusMessage);
    }

    [Fact]
    public void NamedOptionsAreUsed()
    {
        var sourceName = Guid.NewGuid().ToString("N");
        var ingestor = new TestIngestor();
        using var source = new ActivitySource(sourceName);

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .ConfigureServices(services => services.Configure<AzureDataExplorerExporterOptions>("adx", o => o.TracesTableName = "NamedSpans"))
            .AddAzureDataExplorerExporter("adx", o => TestIngestor.Configure(o, ingestor))
            .Build())
        {
            source.StartActivity("span")?.Dispose();
        }

        Assert.Equal("NamedSpans", ingestor.TableName);
        Assert.Single(ingestor.Rows);
    }
}
