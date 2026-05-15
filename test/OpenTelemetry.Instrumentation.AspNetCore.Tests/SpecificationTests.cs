// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit.Abstractions;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests;

public sealed class SpecificationTests(WeaverFixture fixture, ITestOutputHelper outputHelper)
    : WeaverSpecificationTests(fixture, outputHelper)
{
    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task Telemetry_Conforms_To_Semantic_Conventions()
    {
        // Arrange
        var activities = new List<Activity>();
        var metrics = new List<Metric>();

        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();

        builder.Services
            .AddOpenTelemetry()
            .WithMetrics((builder) =>
            {
                builder.AddAspNetCoreInstrumentation()
                        .AddInMemoryExporter(metrics);
            })
            .WithTracing((builder) =>
            {
                builder.AddAspNetCoreInstrumentation()
                       .AddInMemoryExporter(activities);
            });

        using var app = builder.Build();

        app.MapGet("ping", () => "pong");

        await app.StartAsync();

        using (var httpClient = app.GetTestClient())
        {
            _ = await httpClient.GetStringAsync(new Uri("/ping", UriKind.Relative));
        }

        app.Services.GetRequiredService<MeterProvider>().ForceFlush();

        // Act and Assert
        await this.AssertTelemetryConformsToSemanticConventions(
            (activities, metrics),
            HttpInListener.SemanticConventionsVersion);
    }
}
