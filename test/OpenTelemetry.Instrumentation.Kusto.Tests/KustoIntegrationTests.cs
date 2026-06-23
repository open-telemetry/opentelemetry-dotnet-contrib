// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using System.Diagnostics;
using System.Text;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using OpenTelemetry.Instrumentation.Kusto.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

[Trait("CategoryName", "KustoIntegrationTests")]
public sealed class KustoIntegrationTests : IClassFixture<KustoIntegrationTestsFixture>
{
    private readonly KustoIntegrationTestsFixture fixture;

    public KustoIntegrationTests(KustoIntegrationTestsFixture fixture)
    {
        this.fixture = fixture;
    }

    [EnabledOnDockerPlatformTheory(DockerPlatform.Linux)]
    [InlineData("print number=42", true)]
    [InlineData("print number=42", false)]
    public async Task SuccessfulQueryTest(string query, bool processQuery)
    {
        var activities = new List<Activity>();
        var metrics = new List<Metric>();

        var kcsb = this.fixture.ConnectionStringBuilder;

        // Dispose the providers before asserting so all telemetry is flushed first.
        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .AddKustoInstrumentation(options =>
            {
                options.RecordQueryText = processQuery;
                options.RecordQuerySummary = processQuery;
            })
            .Build())
        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddInMemoryExporter(metrics)
            .AddKustoInstrumentation(options =>
            {
                options.RecordQueryText = processQuery;
                options.RecordQuerySummary = processQuery;
            })
            .Build())
        {
            using var queryProvider = KustoClientFactory.CreateCslQueryProvider(kcsb);

            var crp = new ClientRequestProperties()
            {
                // Ensure a stable client ID for snapshots
                ClientRequestId = Convert.ToBase64String(Encoding.UTF8.GetBytes(query)),
            };

            using var reader = queryProvider.ExecuteQuery("NetDefaultDB", query, crp);
            reader.Consume();
        }

        await Verify(
            new
            {
                Activities = FilterActivities(activities),
                Metrics = FilterMetrics(metrics),
            })
            .ScrubHostname(kcsb.Hostname)
            .ScrubPort(this.fixture.DatabaseContainer.GetMappedPublicPort())
            .UseDirectory("Snapshots")
            .UseParameters(query, processQuery);
    }

    [EnabledOnDockerPlatformTheory(DockerPlatform.Linux)]
    [InlineData("print number=42")]
    public async Task TraceOnlyTest(string query)
    {
        var activities = new List<Activity>();

        var kcsb = this.fixture.ConnectionStringBuilder;

        // Dispose the provider before asserting so all telemetry is flushed first.
        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .AddKustoInstrumentation(options =>
            {
                options.RecordQueryText = true;
                options.RecordQuerySummary = false;
            })
            .Build())
        {
            using var queryProvider = KustoClientFactory.CreateCslQueryProvider(kcsb);

            var crp = new ClientRequestProperties()
            {
                // Ensure a stable client ID for snapshots
                ClientRequestId = Convert.ToBase64String(Encoding.UTF8.GetBytes(query)),
            };

            using var reader = queryProvider.ExecuteQuery("NetDefaultDB", query, crp);
            reader.Consume();
        }

        await Verify(
            new
            {
                Activities = FilterActivities(activities),
            })
            .ScrubHostname(kcsb.Hostname)
            .ScrubPort(this.fixture.DatabaseContainer.GetMappedPublicPort())
            .UseDirectory("Snapshots")
            .UseParameters(query);
    }

    [EnabledOnDockerPlatformTheory(DockerPlatform.Linux)]
    [InlineData("print number=42")]
    public async Task MetricsOnlyTest(string query)
    {
        var metrics = new List<Metric>();

        var kcsb = this.fixture.ConnectionStringBuilder;

        // Dispose the provider before asserting so all telemetry is flushed first.
        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddInMemoryExporter(metrics)
            .AddKustoInstrumentation()
            .Build())
        {
            using var queryProvider = KustoClientFactory.CreateCslQueryProvider(kcsb);

            var crp = new ClientRequestProperties()
            {
                // Ensure a stable client ID for snapshots
                ClientRequestId = Convert.ToBase64String(Encoding.UTF8.GetBytes(query)),
            };

            using var reader = queryProvider.ExecuteQuery("NetDefaultDB", query, crp);
            reader.Consume();
        }

        await Verify(
            new
            {
                Metrics = FilterMetrics(metrics),
            })
            .ScrubHostname(kcsb.Hostname)
            .ScrubPort(this.fixture.DatabaseContainer.GetMappedPublicPort())
            .UseDirectory("Snapshots")
            .UseParameters(query);
    }

    [EnabledOnDockerPlatformTheory(DockerPlatform.Linux)]
    [InlineData("InvalidTable | take 10", true)]
    [InlineData("InvalidTable | take 10", false)]
    public async Task FailedQueryTest(string query, bool processQuery)
    {
        var activities = new List<Activity>();
        var metrics = new List<Metric>();

        var kcsb = this.fixture.ConnectionStringBuilder;
        Exception exception;

        // Dispose the providers before asserting so all telemetry is flushed first.
        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .AddKustoInstrumentation(options =>
            {
                options.RecordQueryText = processQuery;
                options.RecordQuerySummary = processQuery;
            })
            .Build())
        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddInMemoryExporter(metrics)
            .AddKustoInstrumentation(options =>
            {
                options.RecordQueryText = processQuery;
                options.RecordQuerySummary = processQuery;
            })
            .Build())
        {
            using var queryProvider = KustoClientFactory.CreateCslQueryProvider(kcsb);

            var crp = new ClientRequestProperties()
            {
                // Ensure a stable client ID for snapshots
                ClientRequestId = Convert.ToBase64String(Encoding.UTF8.GetBytes(query)),
            };

            // Execute the query and expect an exception
            exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                using var reader = queryProvider.ExecuteQuery("NetDefaultDB", query, crp);
                reader.Consume();

                await Task.CompletedTask;
            });
        }

        // A failed query must record error.type on the duration metric, not only on the span, and exactly once
        // even if the failure is reported more than once.
        var durationMetric = metrics.Single(m => m.MeterName == KustoMetrics.MeterName && m.Name == "db.client.operation.duration");
        var durationTags = new List<KeyValuePair<string, object?>>();
        foreach (ref readonly var metricPoint in durationMetric.GetMetricPoints())
        {
            foreach (var tag in metricPoint.Tags)
            {
                durationTags.Add(tag);
            }
        }

        Assert.Equal(1, durationTags.Count(tag => tag.Key == "error.type"));

        await Verify(
            new
            {
                Activities = FilterActivities(activities),
                Metrics = FilterMetrics(metrics),
                Exception = new
                {
                    Type = exception.GetType().FullName,
                    HasMessage = !string.IsNullOrEmpty(exception.Message),
                },
            })
            .ScrubHostname(kcsb.Hostname)
            .ScrubPort(this.fixture.DatabaseContainer.GetMappedPublicPort())
            .UseDirectory("Snapshots")
            .UseParameters(query, processQuery);
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public void NoInstrumentationRegistered_NoEventsEmitted()
    {
        // Arrange
        var activities = new List<Activity>();
        var metrics = new List<Metric>();

        var kcsb = this.fixture.ConnectionStringBuilder;

        // Create providers WITHOUT Kusto instrumentation, disposing them before asserting.
        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .Build())
        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddInMemoryExporter(metrics)
            .Build())
        {
            using var queryProvider = KustoClientFactory.CreateCslQueryProvider(kcsb);

            var crp = new ClientRequestProperties()
            {
                ClientRequestId = "test-no-instrumentation",
            };

            // Act
            using var reader = queryProvider.ExecuteQuery("NetDefaultDB", "print number=42", crp);
            reader.Consume();
        }

        Assert.Empty(FilterActivities(activities));
        Assert.Empty(FilterMetrics(metrics));
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public void EnrichCallbackTest()
    {
        // Arrange
        var activities = new List<Activity>();

        // Query with comment for custom summary
        const string key = "otel-custom-summary=";
        const string summary = "MyOperation";
        var query = $"// {key}{summary}\nprint number=42";

        var kcsb = this.fixture.ConnectionStringBuilder;

        // Dispose the provider before asserting so all telemetry is flushed first.
        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .AddKustoInstrumentation(options =>
            {
                // Disable automatic summarization
                options.RecordQuerySummary = false;

                // Extract the comment from the query text and set the summary attribute manually
                options.Enrich = (activity, record) =>
                {
                    var message = record.Message.AsSpan();
                    var begin = message.IndexOf(key, StringComparison.Ordinal);

                    if (begin < 0)
                    {
                        return;
                    }

                    var summary = message.Slice(begin + key.Length);
                    var end = summary.IndexOfAny('\r', '\n');
                    if (end < 0)
                    {
                        end = summary.Length;
                    }

                    summary = summary.Slice(0, end).Trim();
                    var summaryString = summary.ToString();

                    activity.SetTag(SemanticConventions.AttributeDbQuerySummary, summaryString);
                    activity.DisplayName = summaryString;
                };
            })
            .Build())
        {
            using var queryProvider = KustoClientFactory.CreateCslQueryProvider(kcsb);

            var crp = new ClientRequestProperties()
            {
                ClientRequestId = "test-enrich-callback",
            };

            // Act
            using var reader = queryProvider.ExecuteQuery("NetDefaultDB", query, crp);
            reader.Consume();
        }

        // Assert
        var kustoActivities = activities
            .Where(activity => activity.Source == KustoActivitySource.ActivitySource)
            .ToList();

        Assert.Single(kustoActivities);
        var activity = kustoActivities[0];

        // Verify the custom summary was set by the Enrich callback
        var querySummaryTag = activity.TagObjects.SingleOrDefault(t => t.Key == SemanticConventions.AttributeDbQuerySummary);
        Assert.NotNull(querySummaryTag.Key);
        Assert.Equal(summary, querySummaryTag.Value);

        // Verify the display name was set to the custom summary
        Assert.Equal(summary, activity.DisplayName);
    }

    private static dynamic FilterActivities(IEnumerable<Activity> activities) =>
        activities
            .Where(activity => activity.Source == KustoActivitySource.ActivitySource)
            .Select(activity => new
            {
                ActivitySourceName = activity.Source.Name,
                activity.DisplayName,
                activity.Status,
                activity.StatusDescription,
                activity.TagObjects,
                activity.OperationName,
                activity.IdFormat,
            });

    private static dynamic FilterMetrics(IEnumerable<Metric> metrics) =>
        metrics
            .Where(metric => metric.MeterName == KustoMetrics.MeterName)
            .Select(metric => new
            {
                metric.MeterName,
                metric.Name,
                metric.Description,
                metric.MeterTags,
                metric.Unit,
                metric.Temporality,
                MetricPoints = GetMetricPointDimensions(metric),
            });

    // Capture only the dimensions (tags) of each metric point, not the duration values (sum/min/max), so the
    // snapshot locks in the metric's dimensions without being flaky on timing.
    private static List<Dictionary<string, object?>> GetMetricPointDimensions(Metric metric)
    {
        var points = new List<Dictionary<string, object?>>();

        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            var dimensions = new Dictionary<string, object?>();
            foreach (var tag in metricPoint.Tags)
            {
                dimensions[tag.Key] = tag.Value;
            }

            points.Add(dimensions);
        }

        return points;
    }
}
