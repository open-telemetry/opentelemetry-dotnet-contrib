// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Http.Tests;

public class HttpClientInstrumentationTests
{
    [Theory]
    [InlineData("System.Net.Http.Request")]
    [InlineData("System.Net.Http.Response")]
    public void IsEnabledFilterExcludesDeprecatedEvents(string eventName)
    {
        var isEnabled = HttpClientInstrumentation.CreateIsEnabledFilter(new());
        Assert.False(isEnabled(eventName, null, null));
    }

    [Theory]
    [InlineData("System.Net.Http.HttpRequestOut.Start")]
    [InlineData("System.Net.Http.Exception")]
    [InlineData("System.Net.Http.Unknown")]
    [InlineData("System.Net.Http.Request.Start")]
    [InlineData("")]
    public void IsEnabledFilterIncludesEvents(string eventName)
    {
        var isEnabled = HttpClientInstrumentation.CreateIsEnabledFilter(new());
        Assert.True(isEnabled(eventName, null, null));
    }

    [Fact]
    public void IsEnabledFilterExcludesActivityOnNet7OrGreater()
    {
        // On .NET 7+ the activity is created by the System.Net.Http ActivitySource,
        // so the fallback Activity created by DiagnosticsHandler must not be enabled.
        var isEnabled = HttpClientInstrumentation.CreateIsEnabledFilter(new());
        var expected = Environment.Version.Major < 7;

        Assert.Equal(expected, isEnabled("System.Net.Http.HttpRequestOut", null, null));
    }

    [Fact]
    public void IsEnabledFilterIncludesStopEventIfNeeded()
    {
        var options = new HttpClientTraceInstrumentationOptions();
        var isEnabled = HttpClientInstrumentation.CreateIsEnabledFilter(options);

        // On .NET 10+ the runtime sets the response tags itself, so the
        // Stop event is only needed if the response needs to be enriched.
        var expected = Environment.Version.Major < 10;

        Assert.Equal(expected, isEnabled("System.Net.Http.HttpRequestOut.Stop", null, null));

        // The options are read each time the filter is invoked
        options.EnrichWithHttpResponseMessage = static (_, _) => { };

        Assert.True(isEnabled("System.Net.Http.HttpRequestOut.Stop", null, null));
    }

    [Fact]
    public void IsEnabledFilterIncludesStopEventIfResponseEnrichmentConfigured()
    {
        var options = new HttpClientTraceInstrumentationOptions()
        {
            EnrichWithHttpResponseMessage = static (_, _) => { },
        };

        var isEnabled = HttpClientInstrumentation.CreateIsEnabledFilter(options);

        Assert.True(isEnabled("System.Net.Http.HttpRequestOut.Stop", null, null));
    }
}
