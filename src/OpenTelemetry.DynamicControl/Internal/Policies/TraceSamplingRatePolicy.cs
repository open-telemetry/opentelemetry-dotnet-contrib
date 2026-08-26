// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace OpenTelemetry.DynamicControl.Internal.Policies;

/// <summary>
/// Represents a validated trace-sampling-rate policy.
/// Corresponds to the <c>trace-sampling</c> policy type in the Telemetry Policy OTEP.
/// </summary>
internal sealed class TraceSamplingRatePolicy : TelemetryPolicy
{
    /// <summary>
    /// The <see cref="TelemetryPolicy.PolicyType"/> value for this policy type.
    /// </summary>
    internal const string PolicyTypeName = "trace-sampling";

    private TraceSamplingRatePolicy(string id, string name, double samplingProbability)
        : base(id, name)
    {
        this.SamplingProbability = samplingProbability;
    }

    /// <inheritdoc/>
    public override string PolicyType => PolicyTypeName;

    /// <summary>
    /// Gets the desired sampling probability in the range [0, 1] inclusive,
    /// where 0 means drop all spans and 1 means record all spans.
    /// </summary>
    public double SamplingProbability { get; }

    /// <summary>
    /// Attempts to create a validated <see cref="TraceSamplingRatePolicy"/>.
    /// </summary>
    /// <param name="id">The provider-assigned policy identifier. Must not be null or whitespace.</param>
    /// <param name="name">The human-readable policy name. Must not be null or whitespace.</param>
    /// <param name="samplingProbability">
    /// The desired sampling probability. Must be a finite value in [0, 1] inclusive.
    /// </param>
    /// <param name="policy">
    /// When this method returns <see langword="true"/>, the newly created policy; otherwise <see langword="null"/>.
    /// </param>
    /// <param name="error">
    /// When this method returns <see langword="false"/>, a message describing why validation failed;
    /// otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if all arguments are valid and <paramref name="policy"/> was created;
    /// <see langword="false"/> otherwise.
    /// </returns>
    public static bool TryCreate(
        string? id,
        string? name,
        double samplingProbability,
        [NotNullWhen(true)] out TraceSamplingRatePolicy? policy,
        [NotNullWhen(false)] out string? error)
    {
        if (id is not { Length: > 0 } || string.IsNullOrWhiteSpace(id))
        {
            policy = null;
            error = "The policy ID is required.";
            return false;
        }

        if (name is not { Length: > 0 } || string.IsNullOrWhiteSpace(name))
        {
            policy = null;
            error = "The policy name is required.";
            return false;
        }

        if (double.IsNaN(samplingProbability)
            || double.IsInfinity(samplingProbability)
            || samplingProbability < 0
            || samplingProbability > 1)
        {
            policy = null;
            error = "The sampling probability must be a finite value from 0 through 1, inclusive.";
            return false;
        }

        // Normalize -0.0 to +0.0. Both are equal by IEEE 754 value, but distinct by
        // bit representation. Normalizing ensures a canonical representation.
        if (BitConverter.DoubleToInt64Bits(samplingProbability) < 0)
        {
            samplingProbability = 0.0;
        }

        policy = new TraceSamplingRatePolicy(id, name, samplingProbability);
        error = null;
        return true;
    }
}
