// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Sampler.AWS.Tests;

public class SamplerPollingTests
{
    private const string RulesPath = "/GetSamplingRules";
    private const string TargetsPath = "/SamplingTargets";

    private const string NoSampling = "TraceIdRatioBasedSampler{0.000000}";
    private const string FullSampling = "TraceIdRatioBasedSampler{1.000000}";

    // Two catch-all rules. Until a sampling target arrives both have
    // FixedRate 0 and no reservoir, so every span is dropped.
    private const string RulesJson = """
        {
          "SamplingRuleRecords": [
            { "SamplingRule": { "RuleName": "Rule", "Priority": 1, "FixedRate": 0.0, "ReservoirSize": 0, "Host": "*", "HTTPMethod": "*", "ResourceARN": "*", "ServiceName": "*", "ServiceType": "*", "URLPath": "*", "Version": 1, "Attributes": {} } },
            { "SamplingRule": { "RuleName": "Default", "Priority": 10000, "FixedRate": 0.0, "ReservoirSize": 0, "Host": "*", "HTTPMethod": "*", "ResourceARN": "*", "ServiceName": "*", "ServiceType": "*", "URLPath": "*", "Version": 1, "Attributes": {} } }
          ]
        }
        """;

    // Operator configuration v1: sample everything, come back for new targets every second.
    private const string TargetsSampleAllJson = """
        {
          "SamplingTargetDocuments": [
            { "RuleName": "Rule", "FixedRate": 1.0, "Interval": 1 },
            { "RuleName": "Default", "FixedRate": 1.0, "Interval": 1 }
          ],
          "LastRuleModification": 0
        }
        """;

    // Operator configuration v2: sampling switched off (e.g. to stop a cost spike), come back every second.
    private const string TargetsSampleNoneJson = """
        {
          "SamplingTargetDocuments": [
            { "RuleName": "Rule", "FixedRate": 0.0, "Interval": 1 },
            { "RuleName": "Default", "FixedRate": 0.0, "Interval": 1 }
          ],
          "LastRuleModification": 0
        }
        """;

    // Malformed sampling target documents. The target of "Default" is valid in all of them.
    private const string TargetWithFixedRateAboveOneJson = """{ "SamplingTargetDocuments": [ { "RuleName": "Rule", "FixedRate": 1.5, "Interval": 1 }, { "RuleName": "Default", "FixedRate": 1.0, "Interval": 1 } ] }""";
    private const string TargetWithFixedRateBelowZeroJson = """{ "SamplingTargetDocuments": [ { "RuleName": "Rule", "FixedRate": -0.5, "Interval": 1 }, { "RuleName": "Default", "FixedRate": 1.0, "Interval": 1 } ] }""";
    private const string NullTargetDocumentJson = """{ "SamplingTargetDocuments": [ null, { "RuleName": "Rule", "FixedRate": 1.0, "Interval": 1 }, { "RuleName": "Default", "FixedRate": 1.0, "Interval": 1 } ] }""";
    private const string TargetWithHugeIntervalJson = """{ "SamplingTargetDocuments": [ { "RuleName": "Rule", "FixedRate": 1.0, "Interval": 9223372036854775807 }, { "RuleName": "Default", "FixedRate": 1.0, "Interval": 1 } ] }""";
    private const string TargetWithHugeReservoirQuotaTTLJson = """{ "SamplingTargetDocuments": [ { "RuleName": "Rule", "FixedRate": 1.0, "ReservoirQuota": 1, "ReservoirQuotaTTL": 1e300, "Interval": 1 }, { "RuleName": "Default", "FixedRate": 1.0, "Interval": 1 } ] }""";
    private const string HugeLastRuleModificationJson = """{ "SamplingTargetDocuments": [ { "RuleName": "Rule", "FixedRate": 1.0, "Interval": 1 }, { "RuleName": "Default", "FixedRate": 1.0, "Interval": 1 } ], "LastRuleModification": 1e300 }""";

    // Invalid sampling rules, each of which is added to the rules of RulesJson.
    private const string RuleWithFixedRateAboveOneJson = """{ "RuleName": "Poison", "Priority": 5, "FixedRate": 2.0, "ReservoirSize": 0, "Host": "*", "HTTPMethod": "*", "ResourceARN": "*", "ServiceName": "*", "ServiceType": "*", "URLPath": "*", "Version": 1 }""";
    private const string RuleWithoutRuleNameJson = """{ "Priority": 5, "FixedRate": 0.5, "ReservoirSize": 0, "Host": "*", "HTTPMethod": "*", "ResourceARN": "*", "ServiceName": "*", "ServiceType": "*", "URLPath": "*", "Version": 1 }""";
    private const string RuleWithDuplicateRuleNameJson = """{ "RuleName": "Rule", "Priority": 5, "FixedRate": 0.5, "ReservoirSize": 0, "Host": "*", "HTTPMethod": "*", "ResourceARN": "*", "ServiceName": "*", "ServiceType": "*", "URLPath": "*", "Version": 1 }""";

    public static TheoryData<string, string?> MalformedTargetDocuments() => new()
    {
        // Control: no malformed document is ever served.
        { "control", null },

        // FixedRate outside [0, 1], which TraceIdRatioBasedSampler rejects.
        { "FixedRate > 1", TargetWithFixedRateAboveOneJson },
        { "FixedRate < 0", TargetWithFixedRateBelowZeroJson },

        // A null element in SamplingTargetDocuments.
        { "null target document", NullTargetDocumentJson },

        // An Interval that does not fit a DateTimeOffset, or that would exceed what Timer.Change accepts.
        { "huge Interval", TargetWithHugeIntervalJson },

        // Times that do not fit a DateTimeOffset.
        { "huge ReservoirQuotaTTL", TargetWithHugeReservoirQuotaTTLJson },
        { "huge LastRuleModification", HugeLastRuleModificationJson },
    };

    // The malformed target documents, and whether the target of "Rule" in them is valid.
    public static TheoryData<string, string, bool> MalformedTargetDocumentsAndWhetherRuleTargetIsValid() => new()
    {
        { "FixedRate > 1", TargetWithFixedRateAboveOneJson, false },
        { "FixedRate < 0", TargetWithFixedRateBelowZeroJson, false },
        { "null target document", NullTargetDocumentJson, true },
        { "huge Interval", TargetWithHugeIntervalJson, true },
        { "huge ReservoirQuotaTTL", TargetWithHugeReservoirQuotaTTLJson, false },
        { "huge LastRuleModification", HugeLastRuleModificationJson, true },
    };

    public static TheoryData<string, string?> MalformedRuleDocuments() => new()
    {
        // Control: no malformed document is ever served.
        { "control", null },
        { "new rule with FixedRate > 1", WithRule(RuleWithFixedRateAboveOneJson) },
        { "rule without RuleName", WithRule(RuleWithoutRuleNameJson) },
    };

    public static TheoryData<string, string> InvalidRuleDocuments() => new()
    {
        { "new rule with FixedRate > 1", WithRule(RuleWithFixedRateAboveOneJson) },
        { "rule without RuleName", WithRule(RuleWithoutRuleNameJson) },
        { "rule with the RuleName of another rule", WithRule(RuleWithDuplicateRuleNameJson) },
    };

    [Theory]
    [MemberData(nameof(MalformedTargetDocuments))]
    public async Task MalformedTargetDocumentDoesNotStopTargetPolling(string description, string? malformedTargetsJson)
    {
        Assert.NotNull(description);

        var endpoint = new ScriptedXRayEndpoint();
        endpoint.SetResponse(RulesPath, RulesJson);

        using var server = TestHttpServer.RunServer(endpoint.Handle, out var baseAddress);

        var sampler = AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build())
            .SetPollingInterval(TimeSpan.FromMinutes(5))
            .SetEndpoint(baseAddress.ToString().TrimEnd('/'))
            .Build();

        using var remoteSampler = GetRemoteSampler(sampler);

        // The rules poll runs immediately when the sampler is created.
        await WaitUntilAsync(() => remoteSampler.RulesCache.RuleAppliers.Count == 2, TimeSpan.FromSeconds(20));
        Assert.Equal(SamplingDecision.Drop, DoSample(sampler));

        // Operator configuration v1 ("sample everything"). Fire the target poller now rather than waiting
        // for its initial 10 second delay; this is exactly what the timer does when that delay elapses.
        endpoint.SetResponse(TargetsPath, TargetsSampleAllJson);
        remoteSampler.TargetPollerTimer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        await WaitUntilAsync(() => DoSample(sampler) == SamplingDecision.RecordAndSample, TimeSpan.FromSeconds(20));

        if (malformedTargetsJson != null)
        {
            // The next target poll (about 1 second later, as requested by Interval=1) gets the malformed document.
            endpoint.SetResponse(TargetsPath, malformedTargetsJson);
            await WaitUntilAsync(() => endpoint.HasServed(TargetsPath, malformedTargetsJson), TimeSpan.FromSeconds(20));
        }

        var targetPollsBeforeV2 = endpoint.CountServed(TargetsPath);

        // The endpoint recovers and the operator switches sampling off (v2).
        endpoint.SetResponse(TargetsPath, TargetsSampleNoneJson);

        // The target poller keeps polling and applies v2. When the target of a rule was skipped, the next poll
        // happens after the default interval of 10 seconds.
        await WaitUntilAsync(() => DoSample(sampler) == SamplingDecision.Drop, TimeSpan.FromSeconds(25));
        await WaitUntilAsync(() => endpoint.CountServed(TargetsPath) >= targetPollsBeforeV2 + 2, TimeSpan.FromSeconds(25));
    }

    [Theory]
    [MemberData(nameof(MalformedTargetDocumentsAndWhetherRuleTargetIsValid))]
    public async Task MalformedTargetDocumentOnlySkipsInvalidTargets(string description, string malformedTargetsJson, bool ruleTargetIsValid)
    {
        Assert.NotNull(description);

        var endpoint = new ScriptedXRayEndpoint();
        endpoint.SetResponse(RulesPath, RulesJson);
        endpoint.SetResponse(TargetsPath, TargetsSampleNoneJson);

        using var server = TestHttpServer.RunServer(endpoint.Handle, out var baseAddress);
        using var remoteSampler = await CreateSamplerWithoutPollersAsync(baseAddress);

        await remoteSampler.GetAndUpdateTargetsAsync(CancellationToken.None);

        Assert.Equal(NoSampling, GetFixedRateSampler(remoteSampler, "Rule"));
        Assert.Equal(NoSampling, GetFixedRateSampler(remoteSampler, "Default"));

        endpoint.SetResponse(TargetsPath, malformedTargetsJson);
        await remoteSampler.GetAndUpdateTargetsAsync(CancellationToken.None);

        Assert.Equal(FullSampling, GetFixedRateSampler(remoteSampler, "Default"));
        Assert.Equal(ruleTargetIsValid ? FullSampling : NoSampling, GetFixedRateSampler(remoteSampler, "Rule"));

        var latestNextSnapshotTime = DateTimeOffset.UtcNow + AWSXRayRemoteSampler.MaxTargetInterval;
        Assert.All(remoteSampler.RulesCache.RuleAppliers, applier => Assert.True(applier.NextSnapshotTime <= latestNextSnapshotTime, $"{applier.RuleName} asks for its next target at {applier.NextSnapshotTime}."));
    }

    [Fact]
    public async Task FailedTargetPollIsRescheduled()
    {
        var clock = new FaultInjectingClock();
        var endpoint = new ScriptedXRayEndpoint();
        endpoint.SetResponse(RulesPath, RulesJson);
        endpoint.SetResponse(TargetsPath, TargetsSampleAllJson.Replace("\"LastRuleModification\": 0", "\"LastRuleModification\": 1"));

        using var server = TestHttpServer.RunServer(endpoint.Handle, out var baseAddress);
        using var remoteSampler = await CreateSamplerWithoutPollersAsync(baseAddress, clock);

        var rescheduled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        remoteSampler.TargetPollerTimer.Dispose();
        remoteSampler.TargetPollerTimer = new Timer(_ => rescheduled.TrySetResult(true), null, Timeout.Infinite, Timeout.Infinite);

        // Make the poll fail after it has applied the targets, when it converts their LastRuleModification.
        clock.FailToDateTime = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => remoteSampler.GetAndUpdateTargetsAsync(CancellationToken.None));

        // The next poll is due about a second later, as the targets ask.
        await WithTimeout(rescheduled.Task, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task FailedRulePollIsRescheduled()
    {
        var clock = new FaultInjectingClock();
        var endpoint = new ScriptedXRayEndpoint();
        endpoint.SetResponse(RulesPath, RulesJson);

        using var server = TestHttpServer.RunServer(endpoint.Handle, out var baseAddress);
        using var remoteSampler = await CreateSamplerWithoutPollersAsync(baseAddress, clock);

        var rescheduled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        remoteSampler.RulePollerTimer.Dispose();
        remoteSampler.RulePollerTimer = new Timer(_ => rescheduled.TrySetResult(true), null, Timeout.Infinite, Timeout.Infinite);
        remoteSampler.PollingInterval = TimeSpan.FromMilliseconds(50);
        remoteSampler.RulePollerJitter = TimeSpan.Zero;

        // Make the poll fail when the rules cache records the time of the update.
        clock.FailNow = true;

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => remoteSampler.GetAndUpdateRulesAsync(CancellationToken.None));
        }
        finally
        {
            clock.FailNow = false;
        }

        await WithTimeout(rescheduled.Task, TimeSpan.FromSeconds(10));
    }

    [Theory]
    [MemberData(nameof(MalformedRuleDocuments))]
    public async Task MalformedRuleDocumentDoesNotStopRulePolling(string description, string? malformedRulesJson)
    {
        Assert.NotNull(description);

        var endpoint = new ScriptedXRayEndpoint();
        endpoint.SetResponse(RulesPath, RulesJson);

        using var server = TestHttpServer.RunServer(endpoint.Handle, out var baseAddress);

        var sampler = AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build())
            .SetPollingInterval(TimeSpan.FromMilliseconds(50))
            .SetEndpoint(baseAddress.ToString().TrimEnd('/'))
            .Build();

        using var remoteSampler = GetRemoteSampler(sampler);
        remoteSampler.RulePollerJitter = TimeSpan.Zero;

        // The rules poller is alive and polling every ~50ms.
        await WaitUntilAsync(() => endpoint.CountServed(RulesPath) >= 3, TimeSpan.FromSeconds(20));

        if (malformedRulesJson != null)
        {
            endpoint.SetResponse(RulesPath, malformedRulesJson);
            await WaitUntilAsync(() => endpoint.HasServed(RulesPath, malformedRulesJson), TimeSpan.FromSeconds(20));
        }

        // The endpoint recovers.
        endpoint.SetResponse(RulesPath, RulesJson);
        var rulePollsBeforeRecovery = endpoint.CountServed(RulesPath);

        // The rules poller keeps polling.
        await WaitUntilAsync(() => endpoint.CountServed(RulesPath) >= rulePollsBeforeRecovery + 3, TimeSpan.FromSeconds(20));
        Assert.Equal(2, remoteSampler.RulesCache.RuleAppliers.Count);
    }

    [Theory]
    [MemberData(nameof(InvalidRuleDocuments))]
    public async Task MalformedRuleDocumentOnlySkipsInvalidRules(string description, string malformedRulesJson)
    {
        Assert.NotNull(description);

        var endpoint = new ScriptedXRayEndpoint();
        endpoint.SetResponse(RulesPath, RulesJson);

        using var server = TestHttpServer.RunServer(endpoint.Handle, out var baseAddress);
        using var remoteSampler = await CreateSamplerWithoutPollersAsync(baseAddress);

        endpoint.SetResponse(RulesPath, malformedRulesJson);
        await remoteSampler.GetAndUpdateRulesAsync(CancellationToken.None);

        // The rules of RulesJson, and not the invalid rule (nor a second rule with the same RuleName).
        string[] expectedRuleNames = ["Rule", "Default"];
        int[] expectedPriorities = [1, 10000];
        Assert.Equal(expectedRuleNames, remoteSampler.RulesCache.RuleAppliers.Select(applier => applier.RuleName));
        Assert.Equal(expectedPriorities, remoteSampler.RulesCache.RuleAppliers.Select(applier => applier.Rule.Priority));
    }

    [Fact]
    public async Task StalledResponseBodyDoesNotFreezePollers()
    {
        using var endpoint = new StallingHttpEndpoint(RulesPath);

        var sampler = AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build())
            .SetPollingInterval(TimeSpan.FromMilliseconds(100))
            .SetEndpoint(endpoint.BaseAddress)
            .Build();

        var remoteSampler = GetRemoteSampler(sampler);
        remoteSampler.RulePollerJitter = TimeSpan.Zero;

        try
        {
            try
            {
                // The first rules poll (fired by the constructor) receives headers and then a stalled body.
                await WithTimeout(endpoint.Stalled, TimeSpan.FromSeconds(20));

                // Fire the target poller now rather than after its initial 10 second delay.
                remoteSampler.TargetPollerTimer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);

                // Without the endpoint ever finishing the body, the stalled rules poll times out (after 10 seconds),
                // the target poller gets its turn, and the rules poller polls again.
                await WaitUntilAsync(
                    () => endpoint.CountRequests(TargetsPath) >= 1 && endpoint.CountRequests(RulesPath) >= 2,
                    TimeSpan.FromSeconds(20));
            }
            finally
            {
                // Tear down the stalled connections.
                endpoint.ReleaseStalledResponses();
            }
        }
        finally
        {
            await WithTimeout(Task.Run(remoteSampler.Dispose, TestContext.Current.CancellationToken), TimeSpan.FromSeconds(20));
        }
    }

    [Fact]
    public async Task RequestTimeoutAlsoBoundsReadingTheResponseBody()
    {
        using var endpoint = new StallingHttpEndpoint(RulesPath);
        using var client = new AWSXRaySamplerClient(endpoint.BaseAddress);

        // Get the HttpClient through reflection (the test project does not reference System.Net.Http on .NET Framework).
        var httpClientField = typeof(AWSXRaySamplerClient).GetField("httpClient", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(httpClientField);
        var httpClient = httpClientField.GetValue(client);
        Assert.NotNull(httpClient);
        var timeoutProperty = httpClient.GetType().GetProperty("Timeout");
        Assert.NotNull(timeoutProperty);

        var timeout = Assert.IsType<TimeSpan>(timeoutProperty.GetValue(httpClient));
        Assert.True(timeout <= TimeSpan.FromSeconds(10), $"The timeout of requests is {timeout}.");

        // Lower the timeout before the first request, to keep the test short.
        timeoutProperty.SetValue(httpClient, TimeSpan.FromSeconds(1));

        var getRules = client.GetSamplingRules(CancellationToken.None);

        try
        {
            await WithTimeout(endpoint.Stalled, TimeSpan.FromSeconds(20));

            // The request fails (as a failed poll) although the endpoint neither sends the rest of the body nor
            // closes the connection.
            await WithTimeout(getRules, TimeSpan.FromSeconds(10));
            Assert.Null(await getRules);
        }
        finally
        {
            endpoint.ReleaseStalledResponses();
        }
    }

    // RulesJson with one more sampling rule.
    private static string WithRule(string samplingRuleJson) =>
        RulesJson.Replace("\"SamplingRuleRecords\": [", $"\"SamplingRuleRecords\": [ {{ \"SamplingRule\": {samplingRuleJson} }},");

    private static AWSXRayRemoteSampler GetRemoteSampler(Trace.Sampler sampler)
    {
        var rootSamplerFieldInfo = typeof(ParentBasedSampler).GetField("rootSampler", BindingFlags.NonPublic | BindingFlags.Instance);
        var remoteSampler = (AWSXRayRemoteSampler?)rootSamplerFieldInfo?.GetValue(sampler);

        return remoteSampler ?? throw new InvalidOperationException("Unable to get AWSXRayRemoteSampler from ParentBasedSampler.");
    }

    private static async Task<AWSXRayRemoteSampler> CreateSamplerWithoutPollersAsync(Uri baseAddress, Clock? clock = null)
    {
        var builder = AWSXRayRemoteSampler.Builder(ResourceBuilder.CreateEmpty().Build())
            .SetEndpoint(baseAddress.ToString().TrimEnd('/'));

        if (clock != null)
        {
            builder.SetClock(clock);
        }

        var remoteSampler = GetRemoteSampler(builder.Build());

        // The target poller first fires after 10 seconds, the rule poller right away.
        remoteSampler.TargetPollerTimer.Dispose();
        remoteSampler.TargetPollerTimer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);

        await WaitUntilAsync(() => remoteSampler.RulesCache.RuleAppliers.Count == 2, TimeSpan.FromSeconds(20));

        // Wait until the first rule poll has finished, including rescheduling itself: polls hold the poller lock.
        await remoteSampler.ExecutePollAsync(_ => Task.CompletedTask);

        remoteSampler.RulePollerTimer.Dispose();
        remoteSampler.RulePollerTimer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);

        return remoteSampler;
    }

    private static string GetFixedRateSampler(AWSXRayRemoteSampler remoteSampler, string ruleName) =>
        remoteSampler.RulesCache.RuleAppliers.Single(applier => applier.RuleName == ruleName).FixedRateSampler.Description;

    private static SamplingDecision DoSample(Trace.Sampler sampler)
    {
        var samplingParameters = new SamplingParameters(
            default,
            ActivityTraceId.CreateRandom(),
            "GET /orders",
            ActivityKind.Server,
            [new("url.path", "/orders"), new("http.request.method", "GET")],
            null);

        return sampler.ShouldSample(samplingParameters).Decision;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!condition())
        {
            if (stopwatch.Elapsed > timeout)
            {
                Assert.Fail($"Condition was not met within {timeout}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }
    }

    private static async Task WithTimeout(Task task, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout, TestContext.Current.CancellationToken));

        if (!ReferenceEquals(completed, task))
        {
            Assert.Fail($"Task did not complete within {timeout}.");
        }

        await task;
    }

    /// <summary>
    /// A clock that tells the real time, and that can be made to throw, to make a poll fail part-way through.
    /// </summary>
    private sealed class FaultInjectingClock : Clock
    {
        private static readonly DateTimeOffset EpochStart = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public bool FailNow { get; set; }

        public bool FailToDateTime { get; set; }

        public override DateTimeOffset Now() =>
            this.FailNow ? throw new InvalidOperationException("Injected failure.") : DateTimeOffset.UtcNow;

        public override long NowInMilliSeconds() =>
            (long)(DateTimeOffset.UtcNow - EpochStart).TotalMilliseconds;

        public override DateTimeOffset ToDateTime(double seconds) =>
            this.FailToDateTime ? throw new InvalidOperationException("Injected failure.") : EpochStart.AddSeconds(seconds);

        public override double ToDouble(DateTimeOffset dateTime) =>
            Math.Round((dateTime - EpochStart).TotalMilliseconds, 0) / 1000.0;
    }

    /// <summary>
    /// A scripted X-Ray sampling endpoint that records which response body it served for every request.
    /// </summary>
    private sealed class ScriptedXRayEndpoint
    {
        private readonly ConcurrentDictionary<string, string> responses = new();
        private readonly ConcurrentQueue<KeyValuePair<string, string?>> served = new();

        public void SetResponse(string path, string body) => this.responses[path] = body;

        public int CountServed(string path) => this.served.Count(x => x.Key == path);

        public bool HasServed(string path, string body) => this.served.Any(x => x.Key == path && x.Value == body);

        public void Handle(HttpListenerContext context)
        {
            var path = context.Request.Url?.AbsolutePath ?? string.Empty;

            using (var reader = new StreamReader(context.Request.InputStream))
            {
                _ = reader.ReadToEnd();
            }

            if (this.responses.TryGetValue(path, out var body))
            {
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            else
            {
                context.Response.StatusCode = 404;
            }

            this.served.Enqueue(new(path, body));
        }
    }

    /// <summary>
    /// A raw TCP HTTP/1.1 server. Requests to the stall path get a 200 response whose headers promise a body
    /// that never arrives (the connection is simply held open); all other requests get a 404.
    /// </summary>
    private sealed class StallingHttpEndpoint : IDisposable
    {
        private const int MaxConnections = 1_000;
        private const int MaxRequestHeadLength = 64 * 1024;

        private static readonly string[] LineSeparator = ["\r\n"];

        private readonly TcpListener listener;
        private readonly string stallPath;
        private readonly ConcurrentQueue<string> requestPaths = new();
        private readonly TaskCompletionSource<bool> stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim release = new(false);
        private readonly Thread acceptThread;

        public StallingHttpEndpoint(string stallPath)
        {
            this.stallPath = stallPath;
            this.listener = new TcpListener(IPAddress.Loopback, 0);
            this.listener.Start();
            this.BaseAddress = $"http://127.0.0.1:{((IPEndPoint)this.listener.LocalEndpoint).Port}";
            this.acceptThread = new Thread(this.AcceptLoop) { IsBackground = true };
            this.acceptThread.Start();
        }

        public string BaseAddress { get; }

        public Task Stalled => this.stalled.Task;

        public int CountRequests(string path) => this.requestPaths.Count(p => p == path);

        public void ReleaseStalledResponses() => this.release.Set();

        public void Dispose()
        {
            this.release.Set();
            this.listener.Stop();
            this.acceptThread.Join(TimeSpan.FromSeconds(10));
        }

        private static string ReadRequest(NetworkStream stream)
        {
            var headerBytes = new List<byte>();
            var buffer = new byte[1];

            while (!EndsWithHeaderTerminator(headerBytes))
            {
                if (headerBytes.Count >= MaxRequestHeadLength || stream.Read(buffer, 0, 1) == 0)
                {
                    return string.Empty;
                }

                headerBytes.Add(buffer[0]);
            }

            var headers = Encoding.ASCII.GetString([.. headerBytes]);
            var lines = headers.Split(LineSeparator, StringSplitOptions.None);
            var requestLineParts = lines[0].Split(' ');
            var path = requestLineParts.Length > 1 ? requestLineParts[1] : string.Empty;

            var contentLength = 0;
            foreach (var line in lines)
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    contentLength = int.Parse(line.Substring("Content-Length:".Length).Trim(), System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            var body = new byte[contentLength];
            var read = 0;
            while (read < contentLength)
            {
                var n = stream.Read(body, read, contentLength - read);
                if (n == 0)
                {
                    break;
                }

                read += n;
            }

            return path;
        }

        private static bool EndsWithHeaderTerminator(List<byte> bytes) =>
            bytes.Count >= 4 &&
            bytes[bytes.Count - 4] == '\r' &&
            bytes[bytes.Count - 3] == '\n' &&
            bytes[bytes.Count - 2] == '\r' &&
            bytes[bytes.Count - 1] == '\n';

        private void AcceptLoop()
        {
            for (var connections = 0; connections < MaxConnections; connections++)
            {
                TcpClient client;
                try
                {
                    client = this.listener.AcceptTcpClient();
                }
                catch (Exception)
                {
                    // Listener stopped.
                    return;
                }

                var connectionThread = new Thread(() => this.HandleConnection(client)) { IsBackground = true };
                connectionThread.Start();
            }
        }

        private void HandleConnection(TcpClient client)
        {
            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    var path = ReadRequest(stream);
                    this.requestPaths.Enqueue(path);

                    if (path == this.stallPath && !this.release.IsSet)
                    {
                        // Promise a 4 KiB JSON body, send only its first byte, then go silent.
                        var headers = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 4096\r\n\r\n{");
                        stream.Write(headers, 0, headers.Length);
                        stream.Flush();
                        this.stalled.TrySetResult(true);

                        this.release.Wait();

                        // Reset the connection so that a client still waiting for the body fails immediately.
                        client.Client.LingerState = new LingerOption(true, 0);
                        return;
                    }

                    var notFound = Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                    stream.Write(notFound, 0, notFound.Length);
                    stream.Flush();
                }
            }
            catch (Exception)
            {
                // Client went away; irrelevant for the test.
            }
        }
    }
}
