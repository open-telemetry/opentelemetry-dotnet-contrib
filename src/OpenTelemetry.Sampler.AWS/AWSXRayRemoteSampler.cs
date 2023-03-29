// <copyright file="AWSXRayRemoteSampler.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Threading;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Sampler.AWS;

/// <summary>
/// Remote sampler that gets sampling configuration from AWS X-Ray.
/// </summary>
public sealed class AWSXRayRemoteSampler : Trace.Sampler, IDisposable
{
    internal AWSXRayRemoteSampler(Resource resource, TimeSpan pollingInterval, string endpoint)
    {
        this.Resource = resource;
        this.PollingInterval = pollingInterval;
        this.Endpoint = endpoint;
        this.Client = new AWSXRaySamplerClient(endpoint);

        // execute the first update right away
        this.RulePollerTimer = new Timer(this.GetAndUpdateSampler, null, TimeSpan.Zero, this.PollingInterval);
        this.RulesCache = new RulesCache();
        this.FallbackSampler = new FallbackSampler();
    }

    internal Resource Resource { get; }

    internal TimeSpan PollingInterval { get; }

    internal string Endpoint { get; }

    internal AWSXRaySamplerClient Client { get; }

    internal Timer RulePollerTimer { get; }

    private RulesCache RulesCache { get; }

    private Trace.Sampler FallbackSampler { get; }

    /// <summary>
    /// Initializes a <see cref="AWSXRayRemoteSamplerBuilder"/> for the sampler.
    /// </summary>
    /// <param name="resource">an instance of <see cref="Resources.Resource"/>
    /// to identify the service attributes for sampling. This resource should
    /// be the same as what the OpenTelemetry SDK is configured with.</param>
    /// <returns>an instance of <see cref="AWSXRayRemoteSamplerBuilder"/>.</returns>
    public static AWSXRayRemoteSamplerBuilder Builder(Resource resource)
    {
        return new AWSXRayRemoteSamplerBuilder(resource);
    }

    /// <inheritdoc/>
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        if (this.RulesCache.Expired())
        {
            return this.FallbackSampler.ShouldSample(samplingParameters);
        }

        SamplingRule? matchedRule = this.RulesCache.MatchRule(samplingParameters, this.Resource);

        // ideally this check shouldn't be required
        // since the default rule must have matched.
        if (matchedRule != null)
        {
            return matchedRule.Sample(samplingParameters);
        }

        // and we shouldn't have reached here if the default rule is present.
        return this.FallbackSampler.ShouldSample(samplingParameters);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.RulePollerTimer?.Dispose();
            this.Client?.Dispose();
        }
    }

    private async void GetAndUpdateSampler(object? state)
    {
        List<SamplingRule> rules = await this.Client.GetSamplingRules().ConfigureAwait(false);

        this.RulesCache.UpdateRules(rules);
    }
}
