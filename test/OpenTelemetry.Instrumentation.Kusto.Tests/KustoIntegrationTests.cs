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
    [InlineData(".show version", true)]
    [InlineData(".show databases", true)]
    [InlineData("print number=42", true)]
    [InlineData(".show version", false)]
    [InlineData(".show databases", false)]
    [InlineData("print number=42", false)]
    public Task SuccessfulQueryTest(string query, bool recordQueryText)
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

        var kcsb = new KustoConnectionStringBuilder(this.fixture.DatabaseContainer.GetConnectionString());
        using var queryProvider = KustoClientFactory.CreateCslQueryProvider(kcsb);

        var crp = new ClientRequestProperties()
        {
            ClientRequestId = Convert.ToBase64String(Encoding.UTF8.GetBytes(query)),
        };

        var reader = queryProvider.ExecuteQuery("NetDefaultDB", query, crp);

        tracerProvider.ForceFlush();
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

        Assert.NotEmpty(metrics);
        var durationMetric = metrics.FirstOrDefault(m => m.Name == "db.client.operation.duration");
        Assert.NotNull(durationMetric);

        var countMetric = metrics.FirstOrDefault(m => m.Name == "db.client.operation.count");
        Assert.NotNull(countMetric);

        return Verify(activitySnapshot)
            .ScrubLinesWithReplace(line => line.Replace(kcsb.Hostname, "{Hostname}"))
            .ScrubLinesWithReplace(line => line.Replace(this.fixture.DatabaseContainer.GetMappedPublicPort().ToString(), "{Port}"))
            .UseDirectory("Snapshots")
            .UseParameters(query, recordQueryText);
    }
}
