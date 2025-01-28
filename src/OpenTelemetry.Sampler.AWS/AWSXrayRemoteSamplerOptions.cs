// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.AWS;

namespace OpenTelemetry.Sampler.AWS;

/// <summary>
/// Options for configuring <see cref="AWSXRayRemoteSampler"/>.
/// </summary>
public class AWSXRayRemoteSamplerOptions
{
    /// <summary>
    /// Gets or sets the polling interval for configuration updates. If unset, defaults to 5 minutes.
    /// Must be positive.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the endpoint for the TCP proxy to connect to. This is the address to the port on the
    /// OpenTelemetry Collector configured for proxying X-Ray sampling requests. If unset, defaults to
    /// "http://localhost:2000".
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:2000";

    /// <inheritdoc cref="AWS.SemanticConventionVersion"/>
    public SemanticConventionVersion SemanticConventionVersion { get; set; } = AWSSemanticConventions.DefaultSemanticConventionVersion;
}
