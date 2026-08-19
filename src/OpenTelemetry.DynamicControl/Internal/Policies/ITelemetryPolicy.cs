// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.DynamicControl.Internal.Policies;

/// <summary>
/// Represents a resolved telemetry policy as defined by the Telemetry Policy OTEP.
/// A policy carries an identity (<see cref="Id"/>, <see cref="Name"/>) and a
/// <see cref="PolicyType"/> discriminator that determines which concrete
/// implementation handles it (e.g. <c>trace-sampling</c>).
/// </summary>
internal interface ITelemetryPolicy
{
    /// <summary>Gets the opaque, provider-assigned identifier for this policy instance.</summary>
    string Id { get; }

    /// <summary>Gets the human-readable display name for this policy instance.</summary>
    string Name { get; }

    /// <summary>
    /// Gets the policy type discriminator (e.g. <c>trace-sampling</c>).
    /// Implementers register against this string to receive matching policies.
    /// </summary>
    string PolicyType { get; }
}
