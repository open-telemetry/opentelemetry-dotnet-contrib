// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using OpenTelemetry.Instrumentation.AspNet.Implementation;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNet.Tests;

public class RequestMethodHelperTests : IDisposable
{
    [Theory]
    [InlineData("GET", "GET")]
    [InlineData("POST", "POST")]
    [InlineData("PUT", "PUT")]
    [InlineData("DELETE", "DELETE")]
    [InlineData("HEAD", "HEAD")]
    [InlineData("OPTIONS", "OPTIONS")]
    [InlineData("TRACE", "TRACE")]
    [InlineData("PATCH", "PATCH")]
    [InlineData("CONNECT", "CONNECT")]
    [InlineData("get", "GET")]
    [InlineData("invalid", "_OTHER")]
    public void MethodMappingWorksForKnownMethods(string method, string expected)
    {
        var requestHelper = new RequestMethodHelper();
        var actual = requestHelper.GetNormalizedHttpMethod(method);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("GET", "GET")]
    [InlineData("POST", "POST")]
    [InlineData("PUT", "_OTHER")]
    [InlineData("DELETE", "_OTHER")]
    [InlineData("HEAD", "_OTHER")]
    [InlineData("OPTIONS", "_OTHER")]
    [InlineData("TRACE", "_OTHER")]
    [InlineData("PATCH", "_OTHER")]
    [InlineData("CONNECT", "_OTHER")]
    [InlineData("get", "GET")]
    [InlineData("post", "POST")]
    [InlineData("invalid", "_OTHER")]
    public void MethodMappingWorksForEnvironmentVariables(string method, string expected)
    {
        Environment.SetEnvironmentVariable("OTEL_INSTRUMENTATION_HTTP_KNOWN_METHODS", "GET,POST");
        var requestHelper = new RequestMethodHelper();
        var actual = requestHelper.GetNormalizedHttpMethod(method);
        Assert.Equal(expected, actual);
    }

    public void Dispose()
    {
        // Clean up after tests that set environment variables.
        Environment.SetEnvironmentVariable("OTEL_INSTRUMENTATION_HTTP_KNOWN_METHODS", null);
    }
}
