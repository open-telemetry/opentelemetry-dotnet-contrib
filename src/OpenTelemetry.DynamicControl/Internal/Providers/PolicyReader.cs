// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using OpenTelemetry.DynamicControl.Internal.Policies;

namespace OpenTelemetry.DynamicControl.Internal.Providers;

/// <summary>
/// Reads a policy payload value into a validated policy.
/// </summary>
internal abstract class PolicyReader
{
    /// <summary>
    /// Gets the payload key that carries this policy type.
    /// </summary>
    public abstract string PayloadKey { get; }

    /// <summary>
    /// Gets the <see cref="TelemetryPolicy.PolicyType"/> of the policies this
    /// reader produces.
    /// </summary>
    public abstract PolicyType PolicyType { get; }

    /// <summary>
    /// Gets the display name assigned to every policy this reader produces.
    /// </summary>
    public abstract string PolicyName { get; }

    /// <summary>
    /// Gets the identifier assigned to every policy this reader produces.
    /// </summary>
    public PolicyId PolicyId => new(this.PolicyType.Value);

    /// <summary>
    /// Reads one payload value.
    /// </summary>
    /// <param name="value">The value the payload associates with <see cref="PayloadKey"/>.</param>
    /// <returns>
    /// A result containing either the validated policy or its rejection.
    /// </returns>
    /// <remarks>
    /// Implementations should report unusable values as rejections rather than throwing.
    /// </remarks>
    public abstract PolicyReadResult Read(in JsonElement value);
}
