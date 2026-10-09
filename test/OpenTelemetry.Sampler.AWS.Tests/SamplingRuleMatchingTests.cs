// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;
using System.Text;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Sampler.AWS.Tests;

public class SamplingRuleMatchingTests(ITestOutputHelper output)
{
    private const string NeverTraceHealthChecksRulesJson = """
        {
          "SamplingRuleRecords": [
            { "SamplingRule": { "RuleName": "NeverTraceHealthChecks", "Priority": 1, "FixedRate": 0.0, "ReservoirSize": 0, "Host": "*", "HTTPMethod": "*", "ResourceARN": "*", "ServiceName": "*", "ServiceType": "*", "URLPath": "/health*", "Version": 1, "Attributes": {} } },
            { "SamplingRule": { "RuleName": "Default", "Priority": 10000, "FixedRate": 1.0, "ReservoirSize": 0, "Host": "*", "HTTPMethod": "*", "ResourceARN": "*", "ServiceName": "*", "ServiceType": "*", "URLPath": "*", "Version": 1, "Attributes": {} } }
          ]
        }
        """;

    private static readonly string[] PerResourceGlobs =
    [
        "/api/*/users/*/orders/*",
        "/api/*/users/*/invoices/*",
        "/api/*/users/*/payments/*",
        "/api/*/users/*/refunds/*",
        "/api/*/users/*/addresses/*",
    ];

    private readonly ITestOutputHelper output = output;

    [Fact]
    public async Task GlobMustMatchTheWholeValue()
    {
        var requestHandler = new MockServerRequestHandler("/GetSamplingRules", NeverTraceHealthChecksRulesJson);
        using var server = TestHttpServer.RunServer(requestHandler.Handle, out var baseAddress);

        var sampler = AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build())
            .SetEndpoint(baseAddress.ToString().TrimEnd('/'))
            .Build();

        using var remoteSampler = GetRemoteSampler(sampler);
        await WaitForRulesAsync(remoteSampler, expectedRuleCount: 2);

        Assert.Equal(SamplingDecision.Drop, DoSample(sampler, "/health"));
        Assert.Equal(SamplingDecision.Drop, DoSample(sampler, "/health/ready"));
        Assert.Equal(SamplingDecision.RecordAndSample, DoSample(sampler, "/api/users/42/orders"));
        Assert.Equal(SamplingDecision.RecordAndSample, DoSample(sampler, "/api/users/health/orders"));
        Assert.Equal(SamplingDecision.RecordAndSample, DoSample(sampler, "/api/admin/delete-all-users/healthy"));
        Assert.Equal(SamplingDecision.RecordAndSample, DoSample(sampler, "/api/insurance/health-plans"));
    }

    [Fact]
    public async Task LongPathIsMatchedQuicklyAgainstMultiWildcardGlobs()
    {
        var requestHandler = new MockServerRequestHandler("/GetSamplingRules", BuildPerResourceRulesJson());
        using var server = TestHttpServer.RunServer(requestHandler.Handle, out var baseAddress);

        var sampler = AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build())
            .SetEndpoint(baseAddress.ToString().TrimEnd('/'))
            .Build();

        using var remoteSampler = GetRemoteSampler(sampler);
        await WaitForRulesAsync(remoteSampler, expectedRuleCount: PerResourceGlobs.Length + 1);

        var control = Stopwatch.StartNew();
        Assert.Equal(SamplingDecision.RecordAndSample, DoSample(sampler, "/api/tenants/7/users/42/profile"));
        control.Stop();

        var path = new StringBuilder();
        while (path.Length + "/api//users/".Length <= 8000)
        {
            path.Append("/api//users/");
        }

        var crafted = Stopwatch.StartNew();
        var decision = DoSample(sampler, path.ToString());
        crafted.Stop();

        this.output.WriteLine($"ShouldSample for a normal path: {control.Elapsed}; for a {path.Length} character path with {PerResourceGlobs.Length} multi-wildcard rules: {crafted.Elapsed}.");

        Assert.Equal(SamplingDecision.RecordAndSample, decision);
        Assert.True(control.Elapsed < TimeSpan.FromSeconds(1), $"ShouldSample took {control.Elapsed} for a normal path.");
        Assert.True(crafted.Elapsed < TimeSpan.FromSeconds(1), $"ShouldSample took {crafted.Elapsed} for a {path.Length} character path.");
    }

    private static string BuildPerResourceRulesJson()
    {
        var records = new StringBuilder();

        for (var i = 1; i <= PerResourceGlobs.Length; i++)
        {
            records.Append(
                $$"""
                { "SamplingRule": { "RuleName": "Rule{{i}}", "Priority": {{i}}, "FixedRate": 0.0, "ReservoirSize": 0, "Host": "*", "HTTPMethod": "*", "ResourceARN": "*", "ServiceName": "*", "ServiceType": "*", "URLPath": "{{PerResourceGlobs[i - 1]}}", "Version": 1, "Attributes": {} } },
                """);
        }

        records.Append(
            """
            { "SamplingRule": { "RuleName": "Default", "Priority": 10000, "FixedRate": 1.0, "ReservoirSize": 0, "Host": "*", "HTTPMethod": "*", "ResourceARN": "*", "ServiceName": "*", "ServiceType": "*", "URLPath": "*", "Version": 1, "Attributes": {} } }
            """);

        return $$"""{ "SamplingRuleRecords": [ {{records}} ] }""";
    }

    private static AWSXRayRemoteSampler GetRemoteSampler(Trace.Sampler sampler)
    {
        var rootSamplerFieldInfo = typeof(ParentBasedSampler).GetField("rootSampler", BindingFlags.NonPublic | BindingFlags.Instance);
        var remoteSampler = (AWSXRayRemoteSampler?)rootSamplerFieldInfo?.GetValue(sampler);

        return remoteSampler ?? throw new InvalidOperationException("Unable to get AWSXRayRemoteSampler from ParentBasedSampler.");
    }

    private static async Task WaitForRulesAsync(AWSXRayRemoteSampler remoteSampler, int expectedRuleCount)
    {
        var stopwatch = Stopwatch.StartNew();

        while (remoteSampler.RulesCache.RuleAppliers.Count != expectedRuleCount)
        {
            if (stopwatch.Elapsed > TimeSpan.FromSeconds(20))
            {
                Assert.Fail("Sampling rules were not loaded.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }
    }

    private static SamplingDecision DoSample(Trace.Sampler sampler, string urlPath)
    {
        var samplingParameters = new SamplingParameters(
            default,
            ActivityTraceId.CreateRandom(),
            "testfunction",
            ActivityKind.Server,
            [new("url.path", urlPath), new("http.request.method", "GET")],
            null);

        return sampler.ShouldSample(samplingParameters).Decision;
    }
}
