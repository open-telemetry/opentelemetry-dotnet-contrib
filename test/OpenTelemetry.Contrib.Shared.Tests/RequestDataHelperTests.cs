// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Trace;
using Xunit;

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
    [InlineData("POST", "/orders/{id}", "POST /orders/{id}")]
    [InlineData("CUSTOM", "/orders/{id}", "HTTP /orders/{id}")]
    public void GetActivityDisplayNameReturnsExpectedValue(string method, string? route, string expected)
    {
        var requestHelper = new RequestDataHelper(configureByHttpKnownMethodsEnvironmentalVariable: false);

        var actual = requestHelper.GetActivityDisplayName(method, route);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("GET", "GET", null)]
    [InlineData("CUSTOM", "HTTP", "CUSTOM")]
    public void SetActivityDisplayNameAndHttpMethodTagSetsExpectedValues(string method, string expectedDisplayName, string? expectedOriginalMethod)
    {
        var requestHelper = new RequestDataHelper(configureByHttpKnownMethodsEnvironmentalVariable: false);
        using var activity = new Activity("operation");

        requestHelper.SetActivityDisplayNameAndHttpMethodTag(activity, method);

        Assert.Equal(expectedDisplayName, activity.DisplayName);
        Assert.Equal(expectedDisplayName == "HTTP" ? "_OTHER" : method, activity.GetTagItem(SemanticConventions.AttributeHttpRequestMethod));
        Assert.Equal(expectedOriginalMethod, activity.GetTagItem(SemanticConventions.AttributeHttpRequestMethodOriginal));
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
#endif

    [Theory]
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
