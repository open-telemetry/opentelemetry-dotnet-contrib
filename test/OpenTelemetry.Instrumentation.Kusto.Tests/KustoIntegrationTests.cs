// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
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
    [InlineData(".show version")]
    [InlineData(".show databases")]
    [InlineData("print number=42")]
    public Task SuccessfulQueryTest(string query)
    {
        var activities = new List<Activity>();
        var exportedMetrics = new List<Metric>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .AddKustoInstrumentation()
            .Build();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddInMemoryExporter(exportedMetrics)
            .AddMeter("Kusto.Client")
            .Build();

        var kcsb = new KustoConnectionStringBuilder(this.fixture.DatabaseContainer.GetConnectionString());
        using var queryProvider = KustoClientFactory.CreateCslQueryProvider(kcsb);

        var crp = new ClientRequestProperties()
        {
            ClientRequestId = Convert.ToBase64String(Encoding.UTF8.GetBytes(query)),
        };

        var reader = queryProvider.ExecuteQuery("NetDefaultDB", query, crp);

        meterProvider.ForceFlush();

        Assert.NotEmpty(activities);
        var activity = activities.FirstOrDefault(a =>
            a.OperationName.Contains("Query") ||
            a.OperationName.Contains("Management") ||
            a.OperationName.Contains("ExecuteQuery"));
        Assert.NotNull(activity);

        var activitySnapshot = new
        {
            activity.DisplayName,
            activity.Status,
            activity.StatusDescription,
            activity.Tags,
        };

        Assert.NotEmpty(exportedMetrics);
        var durationMetric = exportedMetrics.FirstOrDefault(m => m.Name == "db.client.operation.duration");
        Assert.NotNull(durationMetric);

        var countMetric = exportedMetrics.FirstOrDefault(m => m.Name == "db.client.operation.count");
        Assert.NotNull(countMetric);

        return Verify(activitySnapshot)
            .ScrubLinesWithReplace(line => line.Replace(kcsb.Hostname, "{Hostname}"))
            .ScrubLinesWithReplace(line => line.Replace(this.fixture.DatabaseContainer.GetMappedPublicPort().ToString(), "{Port}"))
            .UseDirectory("Snapshots")
            .UseParameters(query);
    }
}
