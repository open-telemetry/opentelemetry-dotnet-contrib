// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using OpenTelemetry.Instrumentation.AWSLambda.Implementation;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWSLambda.Tests.Implementation;

[Collection("TracerProviderDependent")]
public class AWSLambdaHttpUtilsTests
{
    [Fact]
    public void GetHttpTags_APIGatewayProxyRequest_ReturnsCorrectTags()
    {
        var request = new APIGatewayProxyRequest
        {
            MultiValueHeaders = new Dictionary<string, IList<string>>
            {
                { "X-Forwarded-Proto", new List<string> { "https" } },
                { "Host", new List<string> { "localhost:1234" } },
            },
            MultiValueQueryStringParameters = new Dictionary<string, IList<string>>
            {
#pragma warning disable CA1861 // Avoid constant arrays as arguments
                { "q1", new[] { "value1" } },
#pragma warning restore CA1861 // Avoid constant arrays as arguments
            },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                HttpMethod = "GET",
                Path = "/path/test",
            },
        };

        var actualTags = AWSLambdaHttpUtils.GetHttpTags(request);

        var expectedTags = new Dictionary<string, object>
        {
            { "http.scheme", "https" },
            { "http.target", "/path/test?q1=value1" },
            { "net.host.name", "localhost" },
            { "net.host.port", 1234 },
            { "http.method", "GET" },
        };

        AssertTags(expectedTags, actualTags);
    }

    [Fact]
    public void GetHttpTags_APIGatewayProxyRequestWithEmptyContext_ReturnsTagsFromRequest()
    {
        var request = new APIGatewayProxyRequest
        {
            MultiValueQueryStringParameters = new Dictionary<string, IList<string>>
            {
#pragma warning disable CA1861 // Avoid constant arrays as arguments
                { "q1", new[] { "value1" } },
#pragma warning restore CA1861 // Avoid constant arrays as arguments
            },
            HttpMethod = "POST",
            Path = "/path/test",
        };

        var actualTags = AWSLambdaHttpUtils.GetHttpTags(request);

        var expectedTags = new Dictionary<string, object>
        {
            { "http.method", "POST" },
            { "http.target", "/path/test?q1=value1" },
        };

        AssertTags(expectedTags, actualTags);
    }

    [Fact]
    public void GetHttpTags_APIGatewayProxyRequestWithMultiValueHeader_UsesLastValue()
    {
        var request = new APIGatewayProxyRequest
        {
            MultiValueHeaders = new Dictionary<string, IList<string>>
            {
                { "X-Forwarded-Proto", new List<string> { "https", "http" } },
                { "Host", new List<string> { "localhost:1234", "myhost:432" } },
            },
        };

        var actualTags = AWSLambdaHttpUtils.GetHttpTags(request);

        var expectedTags = new Dictionary<string, object>
        {
            { "http.target", string.Empty },
            { "http.scheme", "http" },
            { "net.host.name", "myhost" },
            { "net.host.port", 432 },
        };

        AssertTags(expectedTags, actualTags);
    }

    [Fact]
    public void GetHttpTags_APIGatewayHttpApiV2ProxyRequest_ReturnsCorrectTags()
    {
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Headers = new Dictionary<string, string>
            {
                { "X-Forwarded-Proto",  "https" },
                { "Host", "localhost:1234" },
            },
            RawPath = "/path/test",
            RawQueryString = "q1=value1",
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                {
                    Method = "GET",
                },
            },
        };

        var actualTags = AWSLambdaHttpUtils.GetHttpTags(request);

        var expectedTags = new Dictionary<string, object>
        {
            { "http.scheme", "https" },
            { "http.target", "/path/test?q1=value1" },
            { "net.host.name", "localhost" },
            { "net.host.port", 1234 },
            { "http.method", "GET" },
        };

        AssertTags(expectedTags, actualTags);
    }

    [Fact]
    public void GetHttpTags_APIGatewayHttpApiV2ProxyRequestWithMultiValueHeader_UsesLastValue()
    {
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Headers = new Dictionary<string, string>
            {
                { "X-Forwarded-Proto", "https,http" },
                { "Host", "localhost:1234,myhost:432" },
            },
        };

        var actualTags = AWSLambdaHttpUtils.GetHttpTags(request);

        var expectedTags = new Dictionary<string, object>
        {
            { "http.target", string.Empty },
            { "http.scheme", "http" },
            { "net.host.name", "myhost" },
            { "net.host.port", 432 },
        };

        AssertTags(expectedTags, actualTags);
    }

    [Fact]
    public void SetHttpTagsFromResult_APIGatewayProxyResponse_SetsCorrectTags()
    {
        var response = new APIGatewayProxyResponse
        {
            StatusCode = 200,
        };

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("TestActivitySource")
            .Build();

        using var testActivitySource = new ActivitySource("TestActivitySource");
        using var activity = testActivitySource.StartActivity("TestActivity");

        AWSLambdaHttpUtils.SetHttpTagsFromResult(activity, response);

        var expectedTags = new Dictionary<string, object>
        {
            { "http.status_code", 200 },
        };

        Assert.NotNull(activity);

        var actualTags = activity.TagObjects
            .Select(kvp => new KeyValuePair<string, object>(kvp.Key, kvp.Value!));
        AssertTags(expectedTags, actualTags);
    }

    [Fact]
    public void SetHttpTagsFromResult_APIGatewayHttpApiV2ProxyResponse_SetsCorrectTags()
    {
        var response = new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 200,
        };

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("TestActivitySource")
            .Build();

        using var testActivitySource = new ActivitySource("TestActivitySource");
        using var activity = testActivitySource.StartActivity("TestActivity");

        AWSLambdaHttpUtils.SetHttpTagsFromResult(activity, response);

        var expectedTags = new Dictionary<string, object>
        {
            { "http.status_code", 200 },
        };

        var actualTags = activity?.TagObjects
            .Select(kvp => new KeyValuePair<string, object>(kvp.Key, kvp.Value ?? new object()));

        AssertTags(expectedTags, actualTags);
    }

    [Theory]
    [InlineData(null, null, null, null)]
    [InlineData("", "", "", null)]
    [InlineData(null, "localhost:4321", "localhost", 4321)]
    [InlineData(null, "localhost:1a", "localhost", null)]
    [InlineData(null, "localhost", "localhost", null)]
    [InlineData("http", "localhost", "localhost", 80)]
    [InlineData("https", "localhost", "localhost", 443)]
    public void GetHostAndPort_HostHeader_ReturnsCorrectHostAndPort(string? httpSchema, string? hostHeader, string? expectedHost, int? expectedPort)
    {
        (var host, var port) = AWSLambdaHttpUtils.GetHostAndPort(httpSchema, hostHeader);

        Assert.Equal(expectedHost, host);
        Assert.Equal(expectedPort, port);
    }

    [Theory]
    [InlineData(null, "")]
#pragma warning disable CA1861 // Avoid constant arrays as arguments
    [InlineData(new string[] { }, "")]
    [InlineData(new[] { "value1" }, "?name=value1")]
    [InlineData(new[] { "value$a" }, "?name=value%24a")]
    [InlineData(new[] { "value 1" }, "?name=value+1")]
    [InlineData(new[] { "value1", "value2" }, "?name=value1&name=value2")]
#pragma warning restore CA1861 // Avoid constant arrays as arguments
    public void GetQueryString_APIGatewayProxyRequest_CorrectQueryString(IList<string>? values, string expectedQueryString)
    {
        var request = new APIGatewayProxyRequest();
        if (values != null)
        {
            request.MultiValueQueryStringParameters = new Dictionary<string, IList<string>>
            {
                { "name", values },
            };
        }

        var queryString = AWSLambdaHttpUtils.GetQueryString(request);

        Assert.Equal(expectedQueryString, queryString);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("name=value1", "?name=value1")]
    [InlineData("sdckj9_+", "?sdckj9_+")]
    public void GetQueryString_APIGatewayHttpApiV2ProxyRequest_CorrectQueryString(string? rawQueryString, string expectedQueryString)
    {
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            RawQueryString = rawQueryString,
        };

        var queryString = AWSLambdaHttpUtils.GetQueryString(request);

        Assert.Equal(expectedQueryString, queryString);
    }

    private static void AssertTags<TActualValue>(IReadOnlyDictionary<string, object> expectedTags, IEnumerable<KeyValuePair<string, TActualValue>>? actualTags)
        where TActualValue : class
    {
        Assert.NotNull(actualTags);
        Assert.Equal(expectedTags.Count, actualTags.Count());
        foreach (var tag in expectedTags)
        {
            Assert.Contains(new KeyValuePair<string, TActualValue>(tag.Key, (TActualValue)tag.Value), actualTags);
        }
    }
}
