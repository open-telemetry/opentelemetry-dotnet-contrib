// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests;

[Collection("AspNetCore")]
public class HttpInListenerTests
{
    public static TheoryData<string, string, int?> HostValues => new()
    {
        { "localhost", "localhost", null },
        { "localhost:5000", "localhost", 5000 },
        { "my-service:8080", "my-service", 8080 },
        { "my-service.example.com:12345", "my-service.example.com", 12345 },
        { "127.0.0.1", "127.0.0.1", null },
        { "127.0.0.1:443", "127.0.0.1", 443 },
        { "[::1]", "[::1]", null },
        { "[::1]:5000", "[::1]", 5000 },
        { "[2001:db8::1]:8443", "[2001:db8::1]", 8443 },
        { "xn--bcher-kva.example", "b\u00FCcher.example", null },
        { "xn--bcher-kva.example:8443", "b\u00FCcher.example", 8443 },
        { "b\u00FCcher.example:8443", "b\u00FCcher.example", 8443 },
        { "example.com:", "example.com", null },
        { "example.com:abc", "example.com", null },
        { ":8080", string.Empty, 8080 },
    };

    [Theory]
    [MemberData(nameof(HostValues))]
    public void OnStartActivitySetsServerAddressAndPort(string host, string expectedAddress, int? expectedPort)
    {
        Assert.SkipWhen(HttpInListener.Net11OrGreater, "ASP.NET Core 11+ sets server.address and server.port natively.");

        var listener = new HttpInListener(new AspNetCoreTraceInstrumentationOptions());

        // Repeat so that both the first (uncached) and subsequent (cached) requests are verified.
        for (var i = 0; i < 3; i++)
        {
            var context = CreateHttpContext("GET", host);
            using var activity = new Activity(HttpInListener.ActivityOperationName).Start();

            listener.OnStartActivity(activity, context);

            Assert.Equal(expectedAddress, activity.GetTagValue(SemanticConventions.AttributeServerAddress));
            Assert.Equal(expectedPort, activity.GetTagValue(SemanticConventions.AttributeServerPort));

            if (expectedPort is not null)
            {
                Assert.IsType<int>(activity.GetTagValue(SemanticConventions.AttributeServerPort));
            }
        }
    }

    [Fact]
    public void OnStartActivityDoesNotSetServerAddressWhenNoHost()
    {
        Assert.SkipWhen(HttpInListener.Net11OrGreater, "ASP.NET Core 11+ sets server.address and server.port natively.");

        var listener = new HttpInListener(new AspNetCoreTraceInstrumentationOptions());
        var context = CreateHttpContext("GET", host: null);
        using var activity = new Activity(HttpInListener.ActivityOperationName).Start();

        listener.OnStartActivity(activity, context);

        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeServerAddress));
        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeServerPort));
    }

    [Theory]
    [InlineData("GET", "GET", "GET", null)]
    [InlineData("get", "GET", "GET", "get")]
    [InlineData("POST", "POST", "POST", null)]
    [InlineData("Delete", "DELETE", "DELETE", "Delete")]
    [InlineData("CONNECT", "CONNECT", "CONNECT", null)]
    [InlineData("FOO", "HTTP", "_OTHER", "FOO")]
    [InlineData("_OTHER", "HTTP", "_OTHER", null)]
    public void OnStartActivitySetsDisplayNameAndHttpMethod(string method, string expectedDisplayName, string expectedMethod, string? expectedOriginalMethod)
    {
        Assert.SkipWhen(HttpInListener.Net11OrGreater, "ASP.NET Core 11+ sets the display name and http.request.method natively.");

        var listener = new HttpInListener(new AspNetCoreTraceInstrumentationOptions());
        var context = CreateHttpContext(method, "localhost:5000");
        using var activity = new Activity(HttpInListener.ActivityOperationName).Start();

        listener.OnStartActivity(activity, context);

        Assert.Equal(expectedDisplayName, activity.DisplayName);
        Assert.Equal(expectedMethod, activity.GetTagValue(SemanticConventions.AttributeHttpRequestMethod));
        Assert.Equal(expectedOriginalMethod, activity.GetTagValue(SemanticConventions.AttributeHttpRequestMethodOriginal));
    }

    [Theory]
    [MemberData(nameof(HostValues))]
    public void ServerHostCacheReturnsSameValuesAsHostString(string value, string expectedAddress, int? expectedPort)
    {
        var host = CreateHttpContext("GET", value).Request.Host;

        for (var i = 0; i < 3; i++)
        {
            var actual = ServerHostCache.Get(host);

            Assert.Equal(host.Value, actual.Value);
            Assert.Equal(host.Host, actual.Address);
            Assert.Equal(host.Port, actual.Port);
            Assert.Equal(expectedAddress, actual.Address);
            Assert.Equal(expectedPort, actual.Port);
        }
    }

    [Fact]
    public void ServerHostCacheReusesEntries()
    {
        var host = CreateHttpContext("GET", "my-service:8080").Request.Host;

        // The cache is shared, so a test running concurrently could evict the entry between two calls.
        var reused = false;
        for (var i = 0; i < 10 && !reused; i++)
        {
            reused = ReferenceEquals(ServerHostCache.Get(host), ServerHostCache.Get(host));
        }

        Assert.True(reused);
    }

    [Fact]
    public void ServerHostCacheIsBoundedAndCorrectWhenEntriesAreEvicted()
    {
        var hosts = Enumerable.Range(0, ServerHostCache.Capacity * 4)
            .Select(i => CreateHttpContext("GET", $"tenant-{i}.example.com:{8000 + i}").Request.Host)
            .ToArray();

        for (var iteration = 0; iteration < 3; iteration++)
        {
            for (var i = 0; i < hosts.Length; i++)
            {
                var actual = ServerHostCache.Get(hosts[i]);

                Assert.Equal($"tenant-{i}.example.com", actual.Address);
                Assert.Equal(8000 + i, actual.Port);
            }
        }
    }

    [Fact]
    public async Task ServerHostCacheIsThreadSafe()
    {
        var hosts = Enumerable.Range(0, ServerHostCache.Capacity + 2)
            .Select(i => CreateHttpContext("GET", i % 2 == 0 ? $"[2001:db8::{i}]:{9000 + i}" : $"host-{i}").Request.Host)
            .ToArray();

        var tasks = Enumerable.Range(0, Environment.ProcessorCount * 2).Select(t => Task.Run(
            () =>
            {
                for (var i = 0; i < 10_000; i++)
                {
                    var host = hosts[(i + t) % hosts.Length];
                    var actual = ServerHostCache.Get(host);

                    if (!string.Equals(actual.Address, host.Host, StringComparison.Ordinal) || !Equals(actual.Port, host.Port))
                    {
                        throw new InvalidOperationException($"Expected {host.Host}/{host.Port} for {host.Value} but got {actual.Address}/{actual.Port}.");
                    }
                }
            },
            TestContext.Current.CancellationToken));

        await Task.WhenAll(tasks);
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore.Hosting.HttpRequestIn", true)]
    [InlineData("Microsoft.AspNetCore.Hosting.HttpRequestIn.Start", true)]
    [InlineData("Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop", true)]
    [InlineData("Microsoft.AspNetCore.Diagnostics.UnhandledException", true)]
    [InlineData("Microsoft.AspNetCore.Hosting.UnhandledException", true)]
    [InlineData("", false)]
    [InlineData("Microsoft.AspNetCore", false)]
    [InlineData("Microsoft.AspNetCore.Hosting.BeginRequest", false)]
    [InlineData("Microsoft.AspNetCore.Hosting.EndRequest", false)]
    [InlineData("Microsoft.AspNetCore.Routing.EndpointMatched", false)]
    [InlineData("Microsoft.AspNetCore.Mvc.BeforeAction", false)]
    [InlineData("Microsoft.AspNetCore.Hosting.HttpRequestIn.Start ", false)]
    [InlineData("microsoft.aspnetcore.hosting.httprequestin.start", false)]
    [InlineData("Microsoft.AspNetCore.Hosting.HttpRequestIn.Stopped", false)]
    public void IsDiagnosticSourceEventEnabledMatchesHandledEvents(string eventName, bool expected)
    {
        Assert.Equal(expected, AspNetCoreInstrumentation.IsDiagnosticSourceEventEnabled(eventName));
        Assert.Equal(expected, AspNetCoreInstrumentation.IsEnabled(eventName, null, null));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(404)]
    [InlineData(599)]
    [InlineData(600)]
    [InlineData(999)]
    public void GetBoxedStatusCodeReturnsBoxedInt(int statusCode)
    {
        var actual = TelemetryHelper.GetBoxedStatusCode(statusCode);

        Assert.IsType<int>(actual);
        Assert.Equal(statusCode, actual);
        Assert.Equal(statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture), TelemetryHelper.GetStatusCodeString(statusCode));
    }

    private static DefaultHttpContext CreateHttpContext(string method, string? host)
    {
        var context = new DefaultHttpContext();

        context.Request.Method = method;
        context.Request.Scheme = "http";
        context.Request.Path = "/api/values";
        context.Request.Protocol = "HTTP/1.1";

        if (host is not null)
        {
            context.Request.Headers.Host = host;
        }

        return context;
    }
}
