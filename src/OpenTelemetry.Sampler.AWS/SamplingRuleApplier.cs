// <copyright file="SamplingRuleApplier.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Threading;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Sampler.AWS;

internal class SamplingRuleApplier
{
    public SamplingRuleApplier(string clientId, Clock clock, SamplingRule rule, Statistics? statistics)
    {
        this.ClientId = clientId;
        this.Clock = clock;
        this.Rule = rule;
        this.RuleName = this.Rule.RuleName;
        this.Statistics = statistics ?? new Statistics();

        if (rule.ReservoirSize > 0)
        {
            // Until calling GetSamplingTargets, the default is to borrow 1/s if reservoir size is
            // positive.
            this.ReservoirSampler = new ParentBasedSampler(new RateLimitingSampler(1, this.Clock));
            this.Borrowing = true;
        }
        else
        {
            // No reservoir sampling, we will always use the fixed rate.
            this.ReservoirSampler = new AlwaysOffSampler();
            this.Borrowing = false;
        }

        this.FixedRateSampler = new ParentBasedSampler(new TraceIdRatioBasedSampler(rule.FixedRate));

        // We either have no reservoir sampling or borrow until we get a quota so have no end time.
        this.ReservoirEndTime = DateTime.MaxValue;

        // We don't have a SamplingTarget so are ready to report a snapshot right away.
        this.NextSnapshotTime = this.Clock.Now();
    }

    private SamplingRuleApplier(
        string clientId,
        SamplingRule rule,
        Clock clock,
        Trace.Sampler reservoirSampler,
        Trace.Sampler fixedRateSampler,
        bool borrowing,
        Statistics statistics,
        DateTime reservoirEndTime,
        DateTime nextSnapshotTime)
    {
        this.ClientId = clientId;
        this.Rule = rule;
        this.RuleName = rule.RuleName;
        this.Clock = clock;
        this.ReservoirSampler = reservoirSampler;
        this.FixedRateSampler = fixedRateSampler;
        this.Borrowing = borrowing;
        this.Statistics = statistics;
        this.ReservoirEndTime = reservoirEndTime;
        this.NextSnapshotTime = nextSnapshotTime;
    }

    internal string ClientId { get; set; }

    internal SamplingRule Rule { get; set; }

    internal string RuleName { get; set; }

    internal Clock Clock { get; set; }

    internal Statistics Statistics { get; set; }

    internal Trace.Sampler ReservoirSampler { get; set; }

    internal Trace.Sampler FixedRateSampler { get; set; }

    internal bool Borrowing { get; set; }

    internal DateTime ReservoirEndTime { get; set; }

    internal DateTime NextSnapshotTime { get; set; }

    // check if this rule applier matches the request
    public bool Matches(SamplingParameters samplingParameters, Resource resource)
    {
        string? httpTarget = null;
        string? httpUrl = null;
        string? httpMethod = null;
        string? httpHost = null;

        if (samplingParameters.Tags is not null)
        {
            foreach (var tag in samplingParameters.Tags)
            {
                if (tag.Key.Equals(SemanticConventions.AttributeHttpTarget, StringComparison.Ordinal))
                {
                    httpTarget = (string?)tag.Value;
                }
                else if (tag.Key.Equals(SemanticConventions.AttributeHttpUrl, StringComparison.Ordinal))
                {
                    httpUrl = (string?)tag.Value;
                }
                else if (tag.Key.Equals(SemanticConventions.AttributeHttpMethod, StringComparison.Ordinal))
                {
                    httpMethod = (string?)tag.Value;
                }
                else if (tag.Key.Equals(SemanticConventions.AttributeHttpHost, StringComparison.Ordinal))
                {
                    httpHost = (string?)tag.Value;
                }
            }
        }

        // URL path may be in either http.target or http.url
        if (httpTarget == null && httpUrl != null)
        {
            int schemeEndIndex = httpUrl.IndexOf("://", StringComparison.Ordinal);

            // Per spec, http.url is always populated with scheme://host/target. If scheme doesn't
            // match, assume it's bad instrumentation and ignore.
            if (schemeEndIndex > 0)
            {
                int pathIndex = httpUrl.IndexOf('/', schemeEndIndex + "://".Length);
                if (pathIndex < 0)
                {
                    httpTarget = "/";
                }
                else
                {
                    httpTarget = httpUrl.Substring(pathIndex);
                }
            }
        }

        string serviceName = (string)resource.Attributes.FirstOrDefault(kvp =>
                kvp.Key.Equals("service.name", StringComparison.Ordinal)).Value;

        return Matcher.AttributeMatch(samplingParameters.Tags, this.Rule.Attributes) &&
               Matcher.WildcardMatch(httpTarget, this.Rule.UrlPath) &&
               Matcher.WildcardMatch(httpMethod, this.Rule.HttpMethod) &&
               Matcher.WildcardMatch(httpHost, this.Rule.Host) &&
               Matcher.WildcardMatch(serviceName, this.Rule.ServiceName) &&
               Matcher.WildcardMatch(GetServiceType(resource), this.Rule.ServiceType) &&
               Matcher.WildcardMatch(GetArn(in samplingParameters, resource), this.Rule.ResourceArn);
    }

    public SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        Interlocked.Increment(ref this.Statistics.RequestCount);
        bool reservoirExpired = this.Clock.Now() >= this.ReservoirEndTime;
        SamplingResult result = !reservoirExpired
            ? this.ReservoirSampler.ShouldSample(in samplingParameters)
            : new SamplingResult(SamplingDecision.Drop);

        if (result.Decision != SamplingDecision.Drop)
        {
            if (this.Borrowing)
            {
                Interlocked.Increment(ref this.Statistics.BorrowCount);
            }

            Interlocked.Increment(ref this.Statistics.SampleCount);

            return result;
        }

        result = this.FixedRateSampler.ShouldSample(samplingParameters);
        if (result.Decision != SamplingDecision.Drop)
        {
            Interlocked.Increment(ref this.Statistics.SampleCount);
        }

        return result;
    }

    // take the snapshot and reset the statistics.
    public SamplingStatisticsDocument Snapshot(DateTime now)
    {
        double timestamp = this.Clock.ToDouble(now);

        long matchedRequests = Interlocked.Exchange(ref this.Statistics.RequestCount, 0L);
        long sampledRequests = Interlocked.Exchange(ref this.Statistics.SampleCount, 0L);
        long borrowedRequests = Interlocked.Exchange(ref this.Statistics.BorrowCount, 0L);

        SamplingStatisticsDocument statiscticsDocument = new SamplingStatisticsDocument(
            this.ClientId,
            this.RuleName,
            matchedRequests,
            sampledRequests,
            borrowedRequests,
            timestamp);

        return statiscticsDocument;
    }

    public SamplingRuleApplier WithTarget(SamplingTargetDocument target, DateTime now)
    {
        Trace.Sampler newFixedRateSampler = target.FixedRate != null
            ? new ParentBasedSampler(new TraceIdRatioBasedSampler(target.FixedRate.Value))
            : this.FixedRateSampler;

        Trace.Sampler newReservoirSampler = new AlwaysOffSampler();
        DateTime newReservoirEndTime = DateTime.MaxValue;
        if (target.ReservoirQuota != null && target.ReservoirQuotaTTL != null)
        {
            if (target.ReservoirQuota > 0)
            {
                newReservoirSampler = new ParentBasedSampler(new RateLimitingSampler(target.ReservoirQuota.Value, this.Clock));
            }
            else
            {
                newReservoirSampler = new AlwaysOffSampler();
            }

            newReservoirEndTime = this.Clock.ToDateTime(target.ReservoirQuotaTTL.Value);
        }

        DateTime newNextSnapshotTime = target.Interval != null
            ? now.AddSeconds(target.Interval.Value)
            : now.Add(AWSXRayRemoteSampler.DefaultTargetInterval);

        return new SamplingRuleApplier(
            this.ClientId,
            this.Rule,
            this.Clock,
            newReservoirSampler,
            newFixedRateSampler,
            false, // no need for borrow
            this.Statistics,
            newReservoirEndTime,
            newNextSnapshotTime);
    }

    private static string GetServiceType(Resource resource)
    {
        string cloudPlatform = (string)resource.Attributes.FirstOrDefault(kvp =>
            kvp.Key.Equals("cloud.platform", StringComparison.Ordinal)).Value;

        if (cloudPlatform == null)
        {
            return string.Empty;
        }

        return Matcher.XRayCloudPlatform.TryGetValue(cloudPlatform, out string? value) ? value : string.Empty;
    }

    private static string GetArn(in SamplingParameters samplingParameters, Resource resource)
    {
        // currently the aws resource detectors only capture ARNs for ECS and Lambda environments.
        string? arn = (string?)resource.Attributes.FirstOrDefault(kvp =>
            kvp.Key.Equals("aws.ecs.container.arn", StringComparison.Ordinal)).Value;

        if (arn != null)
        {
            return arn;
        }

        if (GetServiceType(resource).Equals("AWS::Lambda::Function", StringComparison.Ordinal))
        {
            arn = (string?)samplingParameters.Tags?.FirstOrDefault(kvp => kvp.Key.Equals("faas.id", StringComparison.Ordinal)).Value;

            if (arn != null)
            {
                return arn;
            }
        }

        return string.Empty;
    }
}
