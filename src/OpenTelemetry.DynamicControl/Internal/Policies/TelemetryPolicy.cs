// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Policies;

/// <summary>
/// Base type for all resolved telemetry policies.
/// A policy carries an identity (<see cref="Id"/>, <see cref="Name"/>) and a
/// <see cref="PolicyType"/> discriminator that determines which concrete
/// implementation handles it (e.g. <c>trace-sampling</c>).
/// </summary>
/// <remarks>
/// <para>
/// Derived types must declare every property as <c>get</c>-only, keep every field
/// <c>readonly</c>, and accept values exclusively through the constructor or a static
/// factory method. The policy store relies on this invariant to safely share instances
/// across snapshots and readers without synchronization.
/// </para>
/// <para>
/// This type deliberately does not override <see cref="object.Equals(object?)"/> or
/// <see cref="object.GetHashCode()"/>. Policies compare by reference. The store detects
/// change through <c>PolicySourceVersion</c> and never compares policy content.
/// </para>
/// </remarks>
internal abstract class TelemetryPolicy
{
    protected TelemetryPolicy(PolicyId id, string name)
    {
        Guard.ThrowIfDefault(id);
        this.Id = id;
        this.Name = name;
    }

    /// <summary>
    /// Gets the provider-assigned identifier for this policy instance.
    /// </summary>
    public PolicyId Id { get; }

    /// <summary>
    /// Gets the human-readable display name for this policy instance.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the policy type discriminator (e.g. <c>trace-sampling</c>).
    /// Implementers register against this string to receive matching policies.
    /// </summary>
    public abstract PolicyType PolicyType { get; }
}
