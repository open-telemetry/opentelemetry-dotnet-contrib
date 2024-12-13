// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.APIGatewayEvents;
using OpenTelemetry.AWS;
using OpenTelemetry.Instrumentation.AWSLambda.Implementation;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWSLambda.Tests.Implementation;

/// <summary>
/// Tests for AWS Semantic Conversion via
/// <see cref="AWSLambdaInstrumentationOptions.SemanticConventionVersion"/>.
/// These tests verify that the switching mechanism works rather than explicitly verifying what the
/// semantic convention should be.
/// </summary>
#if NET9_0
[Collection("Sequential-.NET9")]
#else
[Collection("Sequential-.NET8")]
#endif
public sealed class AWSLambdaInstrumentationOptionsTests
{
    [Fact]
    public void CanUseSemanticConvention_V1_27_0()
    {
        var semanticVersion = SemanticConventionVersion.V1_27_0_Experimental;

        var expectedTags = new List<string>
        {
            "http.scheme",
            "http.target",
            "net.host.name",
            "net.host.port",
            "http.method",
        };

        this.CheckHttpTags(semanticVersion, expectedTags);
    }

    [Fact]
    public void CanUseSemanticConvention_V1_29_0()
    {
        var semanticVersion = SemanticConventionVersion.V1_29_0_Experimental;

        var expectedTags = new List<string>
        {
            "url.scheme",
            "url.path",
            "url.query",
            "server.address",
            "server.port",
            "http.request.method",
        };

        this.CheckHttpTags(semanticVersion, expectedTags);
    }

    private void CheckHttpTags(SemanticConventionVersion version, List<string> expectedTags)
    {
        var request = new APIGatewayProxyRequest
        {
            MultiValueHeaders = new Dictionary<string, IList<string>>
            {
                { "X-Forwarded-Proto", new List<string> { "https" } },
                { "Host", new List<string> { "localhost:1234" } },
            },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                HttpMethod = "GET",
                Path = "/path/test",
            },
        };

        var semanticConventions = new AWSSemanticConventions(version);

        var actualTags = AWSLambdaHttpUtils.GetHttpTags(semanticConventions, request);

        this.AssertContainsTags(expectedTags, actualTags);
    }

    private void AssertContainsTags<TActualValue>(List<string> expectedTags, IEnumerable<KeyValuePair<string, TActualValue>>? actualTags)
        where TActualValue : class
    {
        Assert.NotNull(actualTags);

        var keys = actualTags.Select(x => x.Key).ToList();

        foreach (var tag in expectedTags)
        {
            Assert.Contains(tag, keys);
        }
    }
}
