// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests;

[Collection("AspNetCore")]
public sealed class EndToEndTests
    : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> factory;
    private TracerProvider? tracerProvider;

    public EndToEndTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpRequestActivityIsCorrectWithFeatureSwitch(bool isEnabled)
    {
        bool? originalValue = null;

        if (AppContext.TryGetSwitch("Microsoft.AspNetCore.Hosting.SuppressActivityOpenTelemetryData", out var existingValue))
        {
            originalValue = existingValue;
        }

        AppContext.SetSwitch("Microsoft.AspNetCore.Hosting.SuppressActivityOpenTelemetryData", isEnabled);

        try
        {
            var exportedItems = new List<Activity>();

            void ConfigureTestServices(IServiceCollection services)
            {
                this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreInstrumentation()
                    .AddInMemoryExporter(exportedItems)
                    .Build();
            }

            // Arrange
            using var client = this.factory
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(ConfigureTestServices);
                    builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
                })
                .CreateClient();

            client.DefaultRequestHeaders.UserAgent.Add(new("OpenTelemetry.Instrumentation.AspNetCore.Tests", "1.0"));

            _ = await client.GetStringAsync(new Uri("/ping", UriKind.Relative));

            WaitForActivityExport(exportedItems, 1);

            var activity = Assert.Single(exportedItems);

            ValidateAspNetCoreActivity(activity, "/ping");

            Assert.Equal("GET /ping", activity.DisplayName);
            Assert.Equal("GET", activity.GetTagValue(SemanticConventions.AttributeHttpRequestMethod));
            Assert.Equal("localhost", activity.GetTagValue(SemanticConventions.AttributeServerAddress));
            Assert.Equal("OpenTelemetry.Instrumentation.AspNetCore.Tests/1.0", activity.GetTagValue(SemanticConventions.AttributeUserAgentOriginal));
            Assert.Equal("http", activity.GetTagValue(SemanticConventions.AttributeUrlScheme));
            Assert.Equal("/ping", activity.GetTagValue(SemanticConventions.AttributeUrlPath));
        }
        finally
        {
            if (originalValue is { } previousValue)
            {
                AppContext.SetSwitch("Microsoft.AspNetCore.Hosting.SuppressActivityOpenTelemetryData", previousValue);
            }
        }
    }

    public void Dispose()
        => this.tracerProvider?.Dispose();

    private static void WaitForActivityExport(List<Activity> exportedItems, int count)
        => Assert.True(
            SpinWait.SpinUntil(
            () =>
            {
                // We need to let End callback execute as it is executed AFTER response was returned.
                // In unit tests environment there may be a lot of parallel unit tests executed, so
                // giving some breathing room for the End callback to complete
                Thread.Sleep(10);
                return exportedItems.Count >= count;
            },
            TimeSpan.FromSeconds(5)),
            $"Actual: {exportedItems.Count} Expected: {count}");

    private static void ValidateAspNetCoreActivity(Activity activityToValidate, string expectedHttpPath)
    {
        Assert.Equal(ActivityKind.Server, activityToValidate.Kind);
        Assert.Equal(HttpInListener.AspNetCoreActivitySourceName, activityToValidate.Source.Name);
        Assert.NotNull(activityToValidate.Source.Version);
        Assert.Empty(activityToValidate.Source.Version);
        Assert.Equal(expectedHttpPath, activityToValidate.GetTagValue(SemanticConventions.AttributeUrlPath) as string);
    }
}
