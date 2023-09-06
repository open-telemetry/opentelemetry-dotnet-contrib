// <copyright file="AWSXRayRemoteSamplerBuilder.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Resources;

namespace OpenTelemetry.Sampler.AWS;

/// <summary>
/// A builder for AWSXRayRemoteSampler.
/// </summary>
public class AWSXRayRemoteSamplerBuilder
{
    private const string DefaultEndpoint = "http://localhost:2000";

    private static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromMinutes(5);

    private Resource resource;
    private TimeSpan pollingInterval;
    private string endpoint;
    private Clock clock;

    internal AWSXRayRemoteSamplerBuilder(Resource resource)
    {
        this.resource = resource;
        this.pollingInterval = DefaultPollingInterval;
        this.endpoint = DefaultEndpoint;
        this.clock = Clock.GetDefault();
    }

    /// <summary>
    /// Sets the polling interval for configuration updates. If unset, defaults to 5 minutes.
    /// Must be positive.
    /// </summary>
    /// <param name="pollingInterval">the polling interval.</param>
    /// <returns>the same instance of <see cref="AWSXRayRemoteSamplerBuilder"/>.</returns>
    /// <exception cref="ArgumentException">if the argument is non-positive.</exception>
    public AWSXRayRemoteSamplerBuilder SetPollingInterval(TimeSpan pollingInterval)
    {
        if (pollingInterval < TimeSpan.Zero)
        {
            throw new ArgumentException("Polling interval must be non-negative.");
        }

        this.pollingInterval = pollingInterval;

        return this;
    }

    /// <summary>
    /// Sets the endpoint for the TCP proxy to connect to. This is the address to the port on the
    /// OpenTelemetry Collector configured for proxying X-Ray sampling requests. If unset, defaults to
    /// <see cref="DefaultEndpoint"/>.
    /// </summary>
    /// <param name="endpoint">the endpoint for the TCP proxy.</param>
    /// <returns>the same instance of <see cref="AWSXRayRemoteSamplerBuilder"/>.</returns>
    public AWSXRayRemoteSamplerBuilder SetEndpoint(string endpoint)
    {
        if (!string.IsNullOrEmpty(endpoint))
        {
            this.endpoint = endpoint;
        }

        return this;
    }

    /// <summary>
    /// Returns a <see cref="AWSXRayRemoteSampler"/> with configuration of this builder.
    /// </summary>
    /// <returns>an instance of <see cref="AWSXRayRemoteSampler"/>.</returns>
    public AWSXRayRemoteSampler Build()
    {
        return new AWSXRayRemoteSampler(this.resource, this.pollingInterval, this.endpoint, this.clock);
    }

    // This is intended for testing with a mock clock.
    // Should not be exposed to public.
    internal AWSXRayRemoteSamplerBuilder SetClock(Clock clock)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));

        return this;
    }
}
