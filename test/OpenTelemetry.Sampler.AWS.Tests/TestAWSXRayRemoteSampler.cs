// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace OpenTelemetry.Sampler.AWS.Tests;

public class TestAWSXRayRemoteSampler
{
    [Fact]
    public void TestSamplerWithConfiguration()
    {
        var pollingInterval = TimeSpan.FromSeconds(5);
        var endpoint = "http://localhost:3000";
        var parentBasedSampler = AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build())
            .SetPollingInterval(pollingInterval)
            .SetEndpoint(endpoint)
            .Build();

        var rootSamplerFieldInfo = typeof(ParentBasedSampler).GetField("rootSampler", BindingFlags.NonPublic | BindingFlags.Instance);

        var xraySampler = (AWSXRayRemoteSampler?)rootSamplerFieldInfo?.GetValue(parentBasedSampler);

        Assert.Equal(pollingInterval, xraySampler?.PollingInterval);
        Assert.Equal(endpoint, xraySampler?.Endpoint);
        Assert.NotNull(xraySampler?.RulePollerTimer);
        Assert.NotNull(xraySampler?.Client);
    }

    [Fact]
    public void TestSamplerWithDefaults()
    {
        var parentBasedSampler = AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build()).Build();

        var rootSamplerFieldInfo = typeof(ParentBasedSampler).GetField("rootSampler", BindingFlags.NonPublic | BindingFlags.Instance);

        var xraySampler = (AWSXRayRemoteSampler?)rootSamplerFieldInfo?.GetValue(parentBasedSampler);

        Assert.Equal(TimeSpan.FromMinutes(5), xraySampler?.PollingInterval);
        Assert.Equal("http://localhost:2000", xraySampler?.Endpoint);
        Assert.NotNull(xraySampler?.RulePollerTimer);
        Assert.NotNull(xraySampler?.Client);
    }

    [Fact(Skip = "Flaky test. Related issue: https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/1219")]
    public void TestSamplerUpdateAndSample()
    {
        // setup mock server
        var clock = new TestClock();
        var mockServer = WireMockServer.Start();

        // create sampler
        var sampler = AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build())
            .SetPollingInterval(TimeSpan.FromMilliseconds(10))
            .SetEndpoint(mockServer.Url!)
            .SetClock(clock)
            .Build();

        // the sampler will use fallback sampler until rules are fetched.
        Assert.Equal(SamplingDecision.RecordAndSample, this.DoSample(sampler, "cat-service"));

        // GetSamplingRules mock response
        mockServer
            .Given(Request.Create().WithPath("/GetSamplingRules").UsingPost())
            .RespondWith(
                Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(File.ReadAllText("Data/GetSamplingRulesResponseOptionalFields.json")));

        // rules will be polled in 10 milliseconds
        Thread.Sleep(2000);

        // sampler will drop because rule has 0 reservoir and 0 fixed rate
        Assert.Equal(SamplingDecision.Drop, this.DoSample(sampler, "cat-service"));

        // GetSamplingTargets mock response
        mockServer
            .Given(Request.Create().WithPath("/SamplingTargets").UsingPost())
            .RespondWith(
                Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(File.ReadAllText("Data/GetSamplingTargetsResponseOptionalFields.json")));

        // targets will be polled in 10 seconds
        Thread.Sleep(13000);

        // sampler will always sampler since target has 100% fixed rate
        Assert.Equal(SamplingDecision.RecordAndSample, this.DoSample(sampler, "cat-service"));
        Assert.Equal(SamplingDecision.RecordAndSample, this.DoSample(sampler, "cat-service"));
        Assert.Equal(SamplingDecision.RecordAndSample, this.DoSample(sampler, "cat-service"));

        mockServer.Stop();
    }

    private SamplingDecision DoSample(Trace.Sampler sampler, string serviceName)
    {
        var samplingParams = new SamplingParameters(
            default,
            ActivityTraceId.CreateRandom(),
            "myActivityName",
            ActivityKind.Server,
            [new("test", serviceName)],
            null);

        return sampler.ShouldSample(samplingParams).Decision;
    }
}
