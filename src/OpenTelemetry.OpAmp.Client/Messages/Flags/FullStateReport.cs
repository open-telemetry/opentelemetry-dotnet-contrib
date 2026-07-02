// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Messages.Flags;

/// <summary>
/// Contains all parts that are necessary to restore the full state in the server.
/// </summary>
public sealed class FullStateReport
{
    /// <summary>
    /// Gets or sets the effective configuration.
    /// </summary>
    public IEnumerable<EffectiveConfigFile>? EffectiveConfigFiles { get; set; }

    /// <summary>
    /// Gets or sets the custom capabilities.
    /// </summary>
    public IEnumerable<string>? CustomCapabilities { get; set; }

    /// <summary>
    /// Gets or sets the last remote config status.
    /// </summary>
    public RemoteConfigStatusReport? RemoteConfigStatus { get; set; }
}
