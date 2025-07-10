// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.Host;

/// <summary>
/// Provides options for configuring the host resource detector.
/// </summary>
public class HostDetectorOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the MAC addresses of the host are to be included as a resource attributes.
    /// </summary>
    public bool IncludeMac { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the IP addresses of the host are to be included as a resource attributes.
    /// </summary>
    public bool IncludeIP { get; set; }
}
