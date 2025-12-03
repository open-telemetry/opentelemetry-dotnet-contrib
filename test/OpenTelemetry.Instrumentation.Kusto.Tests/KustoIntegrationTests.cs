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
using Xunit;

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

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .AddKustoInstrumentation(options =>
            {
                options.RecordQueryText = processQuery;
                options.RecordQuerySummary = processQuery;
            })
            .Build();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddInMemoryExporter(metrics)
            .AddKustoInstrumentation()
            .Build();

        var kcsb = this.fixture.ConnectionStringBuilder;
        using var queryProvider = KustoClientFactory.CreateCslQueryProvider(kcsb);

        var crp = new ClientRequestProperties()
        {
            // Ensure a stable client ID for snapshots
            ClientRequestId = Convert.ToBase64String(Encoding.UTF8.GetBytes(query)),
        };

        using var reader = queryProvider.ExecuteQuery("NetDefaultDB", query, crp);
        reader.Consume();

        tracerProvider.ForceFlush();
        meterProvider.ForceFlush();

        var activitySnapshots = activities
            .Where(activity => activity.Source == KustoActivitySourceHelper.ActivitySource)
            .Select(activity => new
            {
                activity.DisplayName,
                activity.Source.Name,
                activity.Status,
                activity.StatusDescription,
                activity.TagObjects,
                activity.OperationName,
                activity.IdFormat,
            });

        var metricSnapshots = metrics
            .Where(metric => metric.MeterName == KustoActivitySourceHelper.MeterName)
            .Select(metric => new
            {
                metric.Name,
                metric.Description,
                metric.MeterTags,
                metric.Unit,
                metric.Temporality,
            });

        await Verify(
            new
            {
                Activities = activitySnapshots,
                Metrics = metricSnapshots,
            })
            .ScrubHostname(kcsb.Hostname)
            .ScrubPort(this.fixture.DatabaseContainer.GetMappedPublicPort())
            .UseDirectory("Snapshots")
            .UseParameters(query, processQuery);
    }

    [EnabledOnDockerPlatformTheory(DockerPlatform.Linux)]
    [InlineData("InvalidTable | take 10", true)]
    [InlineData("InvalidTable | take 10", false)]
    public async Task FailedQueryTest(string query, bool processQuery)
    {
        var activities = new List<Activity>();
        var metrics = new List<Metric>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .AddKustoInstrumentation(options =>
            {
                options.RecordQueryText = processQuery;
                options.RecordQuerySummary = processQuery;
            })
            .Build();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddInMemoryExporter(metrics)
            .AddKustoInstrumentation()
            .Build();

        var kcsb = this.fixture.ConnectionStringBuilder;
        using var queryProvider = KustoClientFactory.CreateCslQueryProvider(kcsb);

        var crp = new ClientRequestProperties()
        {
            // Ensure a stable client ID for snapshots
            ClientRequestId = Convert.ToBase64String(Encoding.UTF8.GetBytes(query)),
        };

        // Execute the query and expect an exception
        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            using var reader = queryProvider.ExecuteQuery("NetDefaultDB", query, crp);
            reader.Consume();

            await Task.CompletedTask;
        });

        tracerProvider.ForceFlush();
        meterProvider.ForceFlush();

        var activitySnapshots = activities
            .Where(activity => activity.Source == KustoActivitySourceHelper.ActivitySource)
            .Select(activity => new
            {
                activity.DisplayName,
                activity.Source.Name,
                activity.Status,
                activity.StatusDescription,
                activity.TagObjects,
                activity.OperationName,
                activity.IdFormat,
            });

        var metricSnapshots = metrics
            .Where(metric => metric.MeterName == KustoActivitySourceHelper.MeterName)
            .Select(metric => new
            {
                metric.Name,
                metric.Description,
                metric.MeterTags,
                metric.Unit,
                metric.Temporality,
            });

        await Verify(
            new
            {
                Activities = activitySnapshots,
                Metrics = metricSnapshots,
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

        // Create providers WITHOUT Kusto instrumentation
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .Build();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddInMemoryExporter(metrics)
            .Build();

        var kcsb = this.fixture.ConnectionStringBuilder;
        using var queryProvider = KustoClientFactory.CreateCslQueryProvider(kcsb);

        var crp = new ClientRequestProperties()
        {
            ClientRequestId = "test-no-instrumentation",
        };

        // Act
        using var reader = queryProvider.ExecuteQuery("NetDefaultDB", "print number=42", crp);
        reader.Consume();

        tracerProvider.ForceFlush();
        meterProvider.ForceFlush();

        // Assert - No Kusto activities or metrics should be emitted
        var kustoActivities = activities
            .Where(activity => activity.Source == KustoActivitySourceHelper.ActivitySource)
            .ToList();

        var kustoMetrics = metrics
            .Where(metric => metric.MeterName == KustoActivitySourceHelper.MeterName)
            .ToList();

        Assert.Empty(kustoActivities);
        Assert.Empty(kustoMetrics);
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

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
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
            .Build();

        var kcsb = this.fixture.ConnectionStringBuilder;
        using var queryProvider = KustoClientFactory.CreateCslQueryProvider(kcsb);

        var crp = new ClientRequestProperties()
        {
            ClientRequestId = "test-enrich-callback",
        };

        // Act
        using var reader = queryProvider.ExecuteQuery("NetDefaultDB", query, crp);
        reader.Consume();

        tracerProvider.ForceFlush();

        // Assert
        var kustoActivities = activities
            .Where(activity => activity.Source == KustoActivitySourceHelper.ActivitySource)
            .ToList();

        Assert.Single(kustoActivities);
        var activity = kustoActivities[0];

        // Verify the custom summary was set by the Enrich callback
        var querySummaryTag = activity.Tags.SingleOrDefault(t => t.Key == SemanticConventions.AttributeDbQuerySummary);
        Assert.NotNull(querySummaryTag.Key);
        Assert.Equal(summary, querySummaryTag.Value);

        // Verify the display name was set to the custom summary
        Assert.Equal(summary, activity.DisplayName);
    }
}
