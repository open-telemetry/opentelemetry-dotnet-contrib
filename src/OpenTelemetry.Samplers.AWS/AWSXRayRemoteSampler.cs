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
using System.Threading;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Samplers.AWS;

/// <summary>
/// Remote sampler that gets sampling configuration from AWS X-Ray.
/// </summary>
public sealed class AWSXRayRemoteSampler : Sampler, IDisposable
{
    internal AWSXRayRemoteSampler(TimeSpan pollingInterval, string endpoint)
    {
        this.PollingInterval = pollingInterval;
        this.Endpoint = endpoint;
        this.Client = new AWSXRaySamplerClient(endpoint);

        // execute the first update right away
        this.RulePollerTimer = new Timer(this.GetAndUpdateSampler, null, TimeSpan.Zero, this.PollingInterval);
    }

    internal TimeSpan PollingInterval { get; }

    internal string Endpoint { get; }

    internal AWSXRaySamplerClient Client { get; }

    internal Timer RulePollerTimer { get; }

    /// <summary>
    /// Initializes a <see cref="AWSXRayRemoteSamplerBuilder"/> for the sampler.
    /// </summary>
    /// <returns>an instance of <see cref="AWSXRayRemoteSamplerBuilder"/>.</returns>
    public static AWSXRayRemoteSamplerBuilder Builder()
    {
        return new AWSXRayRemoteSamplerBuilder();
    }

    /// <inheritdoc/>
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        // TODO: add the actual functionality for sampling.
        throw new System.NotImplementedException();
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

    private async void GetAndUpdateSampler(object state)
    {
        await this.Client.GetSamplingRules().ConfigureAwait(false);

        // TODO: more functionality to be added.
    }
}
