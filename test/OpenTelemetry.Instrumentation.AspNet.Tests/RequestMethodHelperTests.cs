// <copyright file="RequestMethodHelperTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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
        var actual = requestHelper.TryGetMethod(method, out var outMethod) ? outMethod : "_OTHER";
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
        var actual = requestHelper.TryGetMethod(method, out var outMethod) ? outMethod : "_OTHER";
        Assert.Equal(expected, actual);
    }

    public void Dispose()
    {
        // Clean up after tests that set environment variables.
        Environment.SetEnvironmentVariable("OTEL_INSTRUMENTATION_HTTP_KNOWN_METHODS", null);
    }
}
