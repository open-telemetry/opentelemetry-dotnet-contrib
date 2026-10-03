// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Internal.Tests;

public class RequestDataHelperTests
{
    public static TheoryData<Version, string> MappingVersionProtocolToVersionData => new()
    {
        { new Version(1, 0), "1.0" },
        { new Version(1, 1), "1.1" },
        { new Version(2, 0), "2" },
        { new Version(3, 0), "3" },
        { new Version(7, 6, 5), "7.6.5" },
    };

    [Theory]
    [InlineData("CONNECT", "CONNECT")]
    [InlineData("DELETE", "DELETE")]
    [InlineData("GET", "GET")]
    [InlineData("HEAD", "HEAD")]
    [InlineData("OPTIONS", "OPTIONS")]
    [InlineData("PATCH", "PATCH")]
    [InlineData("POST", "POST")]
    [InlineData("PUT", "PUT")]
#if NET9_0
    [InlineData("QUERY", "_OTHER")]
#else
    [InlineData("QUERY", "QUERY")]
#endif
    [InlineData("TRACE", "TRACE")]
    [InlineData("get", "GET")]
    [InlineData("invalid", "_OTHER")]
    public void MethodMappingWorksForKnownMethods(string method, string expected)
    {
        var requestHelper = new RequestDataHelper(configureByHttpKnownMethodsEnvironmentalVariable: true);
        var actual = requestHelper.GetNormalizedHttpMethod(method);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("CONNECT", "_OTHER")]
    [InlineData("DELETE", "_OTHER")]
    [InlineData("GET", "GET")]
    [InlineData("HEAD", "_OTHER")]
    [InlineData("OPTIONS", "_OTHER")]
    [InlineData("PATCH", "_OTHER")]
    [InlineData("POST", "POST")]
    [InlineData("PUT", "_OTHER")]
    [InlineData("QUERY", "_OTHER")]
    [InlineData("TRACE", "_OTHER")]
    [InlineData("get", "GET")]
    [InlineData("post", "POST")]
    [InlineData("invalid", "_OTHER")]
    public void MethodMappingWorksForEnvironmentVariables(string method, string expected)
    {
        using (EnvironmentVariableScope.Create("OTEL_INSTRUMENTATION_HTTP_KNOWN_METHODS", "GET,POST"))
        {
            var requestHelper = new RequestDataHelper(configureByHttpKnownMethodsEnvironmentalVariable: true);
            var actual = requestHelper.GetNormalizedHttpMethod(method);
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void MethodMappingWorksIfEnvironmentalVariableConfigurationIsDisabled()
    {
        using (EnvironmentVariableScope.Create("OTEL_INSTRUMENTATION_HTTP_KNOWN_METHODS", "GET,POST"))
        {
            var requestHelper = new RequestDataHelper(configureByHttpKnownMethodsEnvironmentalVariable: false);
            var actual = requestHelper.GetNormalizedHttpMethod("CONNECT");
            Assert.Equal("CONNECT", actual);
        }
    }

    [Theory]
    [InlineData("GET", null, "GET")]
    [InlineData("GET", "", "GET")]
    [InlineData("get", null, "GET")]
    [InlineData("CUSTOM", null, "HTTP")]
    [InlineData("CUSTOM", "", "HTTP")]
    [InlineData("_OTHER", null, "HTTP")]
    [InlineData("GET", "/", "GET /")]
    [InlineData("GET", "api/users/{id}", "GET api/users/{id}")]
    [InlineData("get", "api/users/{id}", "GET api/users/{id}")]
    [InlineData("Get", "api/users/{id}", "GET api/users/{id}")]
    [InlineData("POST", "/orders/{id}", "POST /orders/{id}")]
    [InlineData("PUT", "/orders/{id}", "PUT /orders/{id}")]
    [InlineData("DELETE", "/orders/{id}", "DELETE /orders/{id}")]
    [InlineData("HEAD", "/orders/{id}", "HEAD /orders/{id}")]
    [InlineData("OPTIONS", "/orders/{id}", "OPTIONS /orders/{id}")]
    [InlineData("TRACE", "/orders/{id}", "TRACE /orders/{id}")]
    [InlineData("PATCH", "/orders/{id}", "PATCH /orders/{id}")]
    [InlineData("CONNECT", "/orders/{id}", "CONNECT /orders/{id}")]
#if NET9_0
    [InlineData("QUERY", "/orders/{id}", "HTTP /orders/{id}")]
#else
    [InlineData("QUERY", "/orders/{id}", "QUERY /orders/{id}")]
#endif
    [InlineData("CUSTOM", "/orders/{id}", "HTTP /orders/{id}")]
    [InlineData("_OTHER", "/orders/{id}", "HTTP /orders/{id}")]
    [InlineData("HTTP", "/orders/{id}", "HTTP /orders/{id}")]
    [InlineData("GET", "api/v1/customers/{customerId}/orders/{orderId}/items/{itemId}", "GET api/v1/customers/{customerId}/orders/{orderId}/items/{itemId}")]
    public void GetActivityDisplayNameReturnsExpectedValue(string method, string? route, string expected)
    {
        var requestHelper = new RequestDataHelper(configureByHttpKnownMethodsEnvironmentalVariable: false);

        // Repeat so that both the first (uncached) and subsequent (cached) values are verified.
        for (var i = 0; i < 2; i++)
        {
            var actual = requestHelper.GetActivityDisplayName(method, route);

            Assert.Equal(expected, actual);
        }
    }

    [Theory]
    [InlineData("GET", null, "GET")]
    [InlineData("get", "/orders/{id}", "GET /orders/{id}")]
    [InlineData("post", "/orders/{id}", "post /orders/{id}")]
    [InlineData("CUSTOM", "/orders/{id}", "Custom /orders/{id}")]
    [InlineData("custom", null, "Custom")]
    [InlineData("_other", "/orders/{id}", "HTTP /orders/{id}")]
    [InlineData("PUT", "/orders/{id}", "HTTP /orders/{id}")]
    [InlineData("PUT", null, "HTTP")]
    public void GetActivityDisplayNameReturnsExpectedValueForCustomKnownMethods(string method, string? route, string expected)
    {
        using (EnvironmentVariableScope.Create("OTEL_INSTRUMENTATION_HTTP_KNOWN_METHODS", "GET,post,Custom,_OTHER"))
        {
            var requestHelper = new RequestDataHelper(configureByHttpKnownMethodsEnvironmentalVariable: true);

            Assert.True(requestHelper.HasCustomKnownMethods);

            for (var i = 0; i < 2; i++)
            {
                var actual = requestHelper.GetActivityDisplayName(method, route);

                Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    public async Task GetActivityDisplayNameIsThreadSafe()
    {
        var requestHelper = new RequestDataHelper(configureByHttpKnownMethodsEnvironmentalVariable: false);

        string[] methods = ["GET", "get", "POST", "CUSTOM", "DELETE"];
        var routes = Enumerable.Range(0, 32).Select(i => $"api/items/{i}/{{id}}").ToArray();

        var tasks = Enumerable.Range(0, Environment.ProcessorCount * 2).Select(t => Task.Run(
            () =>
            {
                for (var i = 0; i < 5_000; i++)
                {
                    var method = methods[(i + t) % methods.Length];
                    var route = routes[((i * 7) + t) % routes.Length];
                    var prefix = method switch
                    {
                        "get" => "GET",
                        "CUSTOM" => "HTTP",
                        _ => method,
                    };

                    var actual = requestHelper.GetActivityDisplayName(method, route);

                    if (!string.Equals(actual, $"{prefix} {route}", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"Unexpected display name '{actual}' for {method} {route}.");
                    }
                }
            },
            TestContext.Current.CancellationToken));

        await Task.WhenAll(tasks);
    }

    [Theory]
    [InlineData("GET", "GET", "GET", null)]
    [InlineData("get", "GET", "GET", "get")]
    [InlineData("CUSTOM", "HTTP", "_OTHER", "CUSTOM")]
    [InlineData("_OTHER", "HTTP", "_OTHER", null)]
    public void SetActivityDisplayNameAndHttpMethodTagSetsExpectedValues(string method, string expectedDisplayName, string expectedMethod, string? expectedOriginalMethod)
    {
        var requestHelper = new RequestDataHelper(configureByHttpKnownMethodsEnvironmentalVariable: false);
        using var activity = new Activity("operation");

        requestHelper.SetActivityDisplayNameAndHttpMethodTag(activity, method);

        Assert.Equal(expectedDisplayName, activity.DisplayName);
        Assert.Equal(expectedMethod, activity.GetTagItem(SemanticConventions.AttributeHttpRequestMethod));
        Assert.Equal(expectedOriginalMethod, activity.GetTagItem(SemanticConventions.AttributeHttpRequestMethodOriginal));

        // The combined method must be equivalent to setting the display name and the method tag separately.
        using var separate = new Activity("operation");

        requestHelper.SetActivityDisplayName(separate, method);
        requestHelper.SetHttpMethodTag(separate, method);

        Assert.Equal(separate.DisplayName, activity.DisplayName);
        Assert.Equal(separate.TagObjects, activity.TagObjects);
    }

#if NET
    [Fact]
    public void GetActivityDisplayNameCachesRouteDisplayNames()
    {
        var requestHelper = new RequestDataHelper(configureByHttpKnownMethodsEnvironmentalVariable: false);

        var first = requestHelper.GetActivityDisplayName("GET", "/orders/{id}");
        var second = requestHelper.GetActivityDisplayName("GET", "/orders/{id}");

        Assert.Same(first, second);
    }

    [Fact]
    public void GetActivityDisplayNameCachesRouteDisplayNamesPerNormalizedMethod()
    {
        var requestHelper = new RequestDataHelper(configureByHttpKnownMethodsEnvironmentalVariable: false);

        var upper = requestHelper.GetActivityDisplayName("GET", "/orders/{id}");
        var lower = requestHelper.GetActivityDisplayName("get", "/orders/{id}");
        var post = requestHelper.GetActivityDisplayName("POST", "/orders/{id}");
        var custom1 = requestHelper.GetActivityDisplayName("CUSTOM", "/orders/{id}");
        var custom2 = requestHelper.GetActivityDisplayName("OTHER", "/orders/{id}");

        Assert.Same(upper, lower);
        Assert.Same(custom1, custom2);
        Assert.Equal("GET /orders/{id}", upper);
        Assert.Equal("POST /orders/{id}", post);
        Assert.Equal("HTTP /orders/{id}", custom1);
    }
#endif

    [Theory]
    [InlineData("HTTP/1.0", "1.0")]
    [InlineData("HTTP/1.1", "1.1")]
    [InlineData("HTTP/2", "2")]
    [InlineData("HTTP/3", "3")]
    [InlineData("Unknown", "Unknown")]
    public void MappingProtocolToVersion(string protocolVersion, string expected)
    {
        var actual = RequestDataHelper.GetHttpProtocolVersion(protocolVersion);
        Assert.Equal(expected, actual);
    }

    [Theory]
#pragma warning disable xUnit1044 // Avoid using TheoryData type arguments that are not serializable
    [MemberData(nameof(MappingVersionProtocolToVersionData))]
#pragma warning restore xUnit1044 // Avoid using TheoryData type arguments that are not serializable
    public void MappingVersionProtocolToVersion(Version protocolVersion, string expected)
    {
        var actual = RequestDataHelper.GetHttpProtocolVersion(protocolVersion);
        Assert.Equal(expected, actual);
    }
}
