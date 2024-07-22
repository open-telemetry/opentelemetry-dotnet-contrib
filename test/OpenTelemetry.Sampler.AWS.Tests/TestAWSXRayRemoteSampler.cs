// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
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
        TimeSpan pollingInterval = TimeSpan.FromSeconds(5);
        string endpoint = "http://localhost:3000";

        AWSXRayRemoteSampler sampler = AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build())
            .SetPollingInterval(pollingInterval)
            .SetEndpoint(endpoint)
            .Build();

        Assert.Equal(pollingInterval, sampler.PollingInterval);
        Assert.Equal(endpoint, sampler.Endpoint);
        Assert.NotNull(sampler.RulePollerTimer);
        Assert.NotNull(sampler.Client);
    }

    [Fact]
    public void TestSamplerWithDefaults()
    {
        AWSXRayRemoteSampler sampler = AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build()).Build();

        Assert.Equal(TimeSpan.FromMinutes(5), sampler.PollingInterval);
        Assert.Equal("http://localhost:2000", sampler.Endpoint);
        Assert.NotNull(sampler.RulePollerTimer);
        Assert.NotNull(sampler.Client);
    }

    [Fact(Skip = "Flaky test. Related issue: https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/1219")]
    public void TestSamplerUpdateAndSample()
    {
        // setup mock server
        TestClock clock = new TestClock();
        WireMockServer mockServer = WireMockServer.Start();

        // create sampler
        AWSXRayRemoteSampler sampler = AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build())
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
            new List<KeyValuePair<string, object?>>
            {
                new("test", serviceName),
            },
            null);

        return sampler.ShouldSample(samplingParams).Decision;
    }
}
