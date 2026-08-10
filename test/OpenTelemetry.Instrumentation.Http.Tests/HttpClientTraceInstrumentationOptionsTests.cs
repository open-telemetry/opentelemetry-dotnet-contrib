// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Instrumentation.Http.Tests;

public class HttpClientTraceInstrumentationOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(",")]
    public void ShouldNotAssignSensitiveQueryParametersWhenEnvironmentVariableIsEmpty(string? value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_DOTNET_EXPERIMENTAL_HTTPCLIENT_SENSITIVE_QUERY_PARAMETERS"] = value })
            .Build();
        var options = new HttpClientTraceInstrumentationOptions(configuration);
        Assert.Null(options.SensitiveQueryParameters);
    }

    [Theory]
    [InlineData("sig", new[] { "sig" })]
    [InlineData("sig,c", new[] { "sig", "c" })]
    [InlineData("sig,,c", new[] { "sig", "c" })]
    public void ShouldAssignSensitiveQueryParametersFromEnvironmentVariable(string value, string[] expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_DOTNET_EXPERIMENTAL_HTTPCLIENT_SENSITIVE_QUERY_PARAMETERS"] = value })
            .Build();
        var options = new HttpClientTraceInstrumentationOptions(configuration);

        Assert.NotNull(options.SensitiveQueryParameters);
        Assert.Equal(expected.Length, options.SensitiveQueryParameters.Length);
        Assert.All(expected, key => Assert.Contains(key, options.SensitiveQueryParameters));
    }

    [Fact]
    public void ShouldMatchSensitiveQueryParametersCaseSensitively()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_DOTNET_EXPERIMENTAL_HTTPCLIENT_SENSITIVE_QUERY_PARAMETERS"] = "sig" })
            .Build();
        var options = new HttpClientTraceInstrumentationOptions(configuration);

        Assert.NotNull(options.SensitiveQueryParameters);
        Assert.Contains("sig", options.SensitiveQueryParameters);
        Assert.DoesNotContain("SIG", options.SensitiveQueryParameters);
    }
}
