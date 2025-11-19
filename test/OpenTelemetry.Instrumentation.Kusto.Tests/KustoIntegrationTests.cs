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
    public async Task SuccessfulQueryTest(string query, bool recordQueryText)
    {
        var activities = new List<Activity>();
        var metrics = new List<Metric>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .AddKustoInstrumentation(options => options.RecordQueryText = recordQueryText)
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
                activity.Tags,
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
            .ScrubLinesWithReplace(line => line.Replace(kcsb.Hostname, "{Hostname}"))
            .ScrubLinesWithReplace(line => line.Replace(this.fixture.DatabaseContainer.GetMappedPublicPort().ToString(), "{Port}"))
            .UseDirectory("Snapshots")
            .UseParameters(query, recordQueryText);
    }

    [EnabledOnDockerPlatformTheory(DockerPlatform.Linux)]
    [InlineData("InvalidTable | take 10", true)]
    [InlineData("InvalidTable | take 10", false)]
    public async Task FailedQueryTest(string query, bool recordQueryText)
    {
        var activities = new List<Activity>();
        var metrics = new List<Metric>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .AddKustoInstrumentation(options => options.RecordQueryText = recordQueryText)
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

        Debugger.Break();

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
                activity.Tags,
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
                    Type = exception.GetType().Name,
                    HasMessage = !string.IsNullOrEmpty(exception.Message),
                },
            })
            .ScrubLinesWithReplace(line => line.Replace(kcsb.Hostname, "{Hostname}"))
            .ScrubLinesWithReplace(line => line.Replace(this.fixture.DatabaseContainer.GetMappedPublicPort().ToString(), "{Port}"))
            .UseDirectory("Snapshots")
            .UseParameters(query, recordQueryText);
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
}
