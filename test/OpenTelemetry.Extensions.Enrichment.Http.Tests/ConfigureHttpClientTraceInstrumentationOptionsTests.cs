// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.Http;
using Xunit;

namespace OpenTelemetry.Extensions.Enrichment.Http.Tests;

public class ConfigureHttpClientTraceInstrumentationOptionsTests
{
    [Fact]
    public void WhenEnricherAdded_RegistersOptions()
    {
        var services = new ServiceCollection();
        services.TryAddHttpClientTraceEnricher<TestEnricher>();
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<HttpClientTraceInstrumentationOptions>>().Value;

        Assert.NotNull(options.EnrichWithHttpRequestMessage);
        Assert.NotNull(options.EnrichWithHttpResponseMessage);
        Assert.NotNull(options.EnrichWithException);
    }

    [Fact]
    public void OptionsDelegates_InvokeEnricher()
    {
        var services = new ServiceCollection();
        services.TryAddHttpClientTraceEnricher<TestEnricher>();
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<HttpClientTraceInstrumentationOptions>>().Value;
        var enricher = provider.GetServices<HttpClientTraceEnricher>().OfType<TestEnricher>().Single();

        using var activity = new Activity("test").Start();
        var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        var ex = new InvalidOperationException("boom");

        options.EnrichWithHttpRequestMessage!(activity, request);
        options.EnrichWithHttpResponseMessage!(activity, response);
        options.EnrichWithException!(activity, ex);
        activity.Stop();

        Assert.Equal(1, enricher.RequestCount);
        Assert.Equal(1, enricher.ResponseCount);
        Assert.Equal(1, enricher.ExceptionCount);

        // Verify tags set via bag
        Assert.Contains(activity.TagObjects, t => t.Key == "test.request" && (string?)t.Value == "ok");
        Assert.Contains(activity.TagObjects, t => t.Key == "test.response" && (int?)t.Value == 200);
        Assert.Contains(activity.TagObjects, t => t.Key == "test.exception" && (string?)t.Value == "InvalidOperationException");
    }
}
