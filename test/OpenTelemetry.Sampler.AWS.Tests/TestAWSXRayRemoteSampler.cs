// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

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

        using var xraySampler = GetRemoteSampler(parentBasedSampler);

        Assert.Equal(pollingInterval, xraySampler.PollingInterval);
        Assert.Equal(endpoint, xraySampler.Endpoint);
        Assert.NotNull(xraySampler.RulePollerTimer);
        Assert.NotNull(xraySampler.Client);
    }

    [Fact]
    public void TestSamplerWithDefaults()
    {
        var parentBasedSampler = AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build()).Build();

        using var xraySampler = GetRemoteSampler(parentBasedSampler);

        Assert.Equal(TimeSpan.FromMinutes(5), xraySampler.PollingInterval);
        Assert.Equal("http://localhost:2000", xraySampler.Endpoint);
        Assert.NotNull(xraySampler.RulePollerTimer);
        Assert.NotNull(xraySampler.Client);
    }

    [Fact]
    public async Task TestSamplerUpdateAndSample()
    {
        // setup mock server
        var clock = new TestClock();
        var requestHandler = new MockServerRequestHandler();

        using var mockServer = TestHttpServer.RunServer(
            requestHandler.Handle,
            out var endpoint);

        // create sampler
        var sampler = AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build())
            .SetPollingInterval(TimeSpan.FromMilliseconds(10))
            .SetEndpoint(endpoint.ToString().TrimEnd('/'))
            .SetClock(clock)
            .Build();

        using var remoteSampler = GetRemoteSampler(sampler);

        // the sampler will use fallback sampler until rules are fetched.
        Assert.Equal(SamplingDecision.RecordAndSample, this.DoSample(sampler, "cat-service"));

        // GetSamplingRules mock response
        requestHandler.SetResponse("/GetSamplingRules", File.ReadAllText("Data/GetSamplingRulesResponseOptionalFields.json"));

        // Wait until the rules have genuinely been loaded (rather than polling for a Drop decision):
        // while no rules are loaded yet, ShouldSample falls back to FallbackSampler, whose fixed-rate
        // component (5%) can also legitimately return Drop most of the time. That makes "decision ==
        // Drop" an unreliable signal for "the new rule has been applied" - it can pass by coincidence
        // via the fallback sampler before the rule poll has completed even once, and this ambiguity
        // can then also make the very next while-loop iteration succeed on a similarly spurious
        // fallback-based RecordAndSample instead of a genuine target update.
        await this.AssertRulesLoadedAsync(remoteSampler, TestContext.Current.CancellationToken);

        // sampler will drop because rule has 0 reservoir and 0 fixed rate. Poll for the updated
        // decision instead of asserting immediately after a fixed delay, since the sampler's
        // background polling task (every 10ms) may not have picked up the new rule within a fixed
        // time window under CI load.
        await this.AssertEventuallySamplesAsync(sampler, "cat-service", SamplingDecision.Drop, TestContext.Current.CancellationToken);

        // GetSamplingTargets mock response
        requestHandler.SetResponse("/SamplingTargets", File.ReadAllText("Data/GetSamplingTargetsResponseOptionalFields.json"));

        var decision = SamplingDecision.Drop;
        var expected = SamplingDecision.RecordAndSample;

        // targets should be polled in 10 seconds
        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
        {
            while (!cts.IsCancellationRequested)
            {
                decision = this.DoSample(sampler, "cat-service");

                if (decision == expected)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
            }
        }

        // sampler will always sample since target has 100% fixed rate. Confirm this holds
        // consistently rather than asserting a single sample immediately after the loop above
        // breaks, since that instant can otherwise coincide with an in-flight rule/target poll.
        for (var i = 0; i < 3; i++)
        {
            await this.AssertEventuallySamplesAsync(sampler, "cat-service", expected, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task TestSamplerUpdateTargetsWithMissingTargetDocumentsDoesNotThrow()
    {
        var clock = new TestClock();
        var requestHandler = new MockServerRequestHandler();

        using var mockServer = TestHttpServer.RunServer(
            requestHandler.Handle,
            out var endpoint);

        var parentBasedSampler = AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build())
            .SetPollingInterval(TimeSpan.FromMilliseconds(10))
            .SetEndpoint(endpoint.ToString().TrimEnd('/'))
            .SetClock(clock)
            .Build();

        using var sampler = GetRemoteSampler(parentBasedSampler);

        requestHandler.SetResponse("/SamplingTargets", "{\"LastRuleModification\":1530920505.0}");

        await sampler.GetAndUpdateTargetsAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TestFailedRulesPollDoesNotWipeCachedRules()
    {
        var clock = new TestClock();
        var requestHandler = new MockServerRequestHandler();

        using var mockServer = TestHttpServer.RunServer(
            requestHandler.Handle,
            out var endpoint);

        var parentBasedSampler = AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build())
            .SetPollingInterval(TimeSpan.FromMilliseconds(10))
            .SetEndpoint(endpoint.ToString().TrimEnd('/'))
            .SetClock(clock)
            .Build();

        using var sampler = GetRemoteSampler(parentBasedSampler);

        // First poll succeeds and loads two real rules.
        requestHandler.SetResponse("/GetSamplingRules", File.ReadAllText("Data/GetSamplingRulesResponseOptionalFields.json"));
        await sampler.GetAndUpdateRulesAsync(CancellationToken.None);

        Assert.Equal(2, sampler.RulesCache.RuleAppliers.Count);

        // Apply a target to one of the rules, as a real target poll would.
        var targets = new Dictionary<string, SamplingTargetDocument>
        {
            {
                "Test",
                new SamplingTargetDocument
                {
                    FixedRate = 1.0,
                    RuleName = "Test",
                }
            },
        };
        sampler.RulesCache.UpdateTargets(targets);

        var appliedApplier = sampler.RulesCache.RuleAppliers.Single(r => r.RuleName == "Test");
        Assert.Equal("TraceIdRatioBasedSampler{1.000000}", appliedApplier.FixedRateSampler.Description);

        // Simulate a transient failure on the next rules poll: an unparsable response.
        requestHandler.SetResponse("/GetSamplingRules", "notJson");
        await sampler.GetAndUpdateRulesAsync(CancellationToken.None);

        // The cache must retain the previously loaded rules, including the applied
        // target, rather than being wiped to zero rules by the failed poll.
        Assert.Equal(2, sampler.RulesCache.RuleAppliers.Count);
        var applierAfterFailedPoll = sampler.RulesCache.RuleAppliers.Single(r => r.RuleName == "Test");
        Assert.Equal("TraceIdRatioBasedSampler{1.000000}", applierAfterFailedPoll.FixedRateSampler.Description);
    }

    [Fact]
    public async Task ExecutePollAsyncDoesNotBlockCaller()
    {
        using var sampler = GetRemoteSampler(AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build()).Build());

        var pollStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePoll = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var executePollAsyncMethod = typeof(AWSXRayRemoteSampler).GetMethod("ExecutePollAsync", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(executePollAsyncMethod);

        Task PollAsync(CancellationToken cancellationToken)
        {
            pollStarted.TrySetResult(true);
            cancellationToken.Register(() => releasePoll.TrySetCanceled(cancellationToken));
            return releasePoll.Task;
        }

        var stopwatch = Stopwatch.StartNew();
        var executePollTask = sampler.ExecutePollAsync(PollAsync);
        stopwatch.Stop();

        await pollStarted.Task;
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Expected ExecutePollAsync to return without waiting for the poll to finish, but it took {stopwatch.Elapsed}.");

        releasePoll.TrySetResult(true);
        await executePollTask;
    }

    private static AWSXRayRemoteSampler GetRemoteSampler(Trace.Sampler sampler)
    {
        var rootSamplerFieldInfo = typeof(ParentBasedSampler).GetField("rootSampler", BindingFlags.NonPublic | BindingFlags.Instance);
        var remoteSampler = (AWSXRayRemoteSampler?)rootSamplerFieldInfo?.GetValue(sampler);

        return remoteSampler ?? throw new InvalidOperationException("Unable to get AWSXRayRemoteSampler from ParentBasedSampler.");
    }

    private async Task AssertEventuallySamplesAsync(
        Trace.Sampler sampler,
        string serviceName,
        SamplingDecision expected,
        CancellationToken cancellationToken)
    {
        var decision = this.DoSample(sampler, serviceName);
        if (decision == expected)
        {
            return;
        }

        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);

            decision = this.DoSample(sampler, serviceName);
            if (decision == expected)
            {
                return;
            }
        }

        Assert.Equal(expected, decision);
    }

    private async Task AssertRulesLoadedAsync(AWSXRayRemoteSampler remoteSampler, CancellationToken cancellationToken)
    {
        if (remoteSampler.RulesCache.RuleAppliers.Count > 0)
        {
            return;
        }

        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);

            if (remoteSampler.RulesCache.RuleAppliers.Count > 0)
            {
                return;
            }
        }

        Assert.NotEmpty(remoteSampler.RulesCache.RuleAppliers);
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
