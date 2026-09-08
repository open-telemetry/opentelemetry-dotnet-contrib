// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Providers;

/// <summary>
/// Represents either a validated policy or a policy-value rejection.
/// </summary>
internal sealed class PolicyReadResult
{
    private readonly TelemetryPolicy? policy;

    private PolicyReadResult(PolicyRejectionReason reason, TelemetryPolicy? policy, string? error)
    {
        this.Reason = reason;
        this.policy = policy;
        this.Error = error;
    }

    /// <summary>
    /// Gets the rejection category, or <see cref="PolicyRejectionReason.None"/> on success.
    /// </summary>
    public PolicyRejectionReason Reason { get; }

    /// <summary>
    /// Gets the rejection message, or <see langword="null"/> on success.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="policy">The validated policy.</param>
    /// <returns>A successful result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="policy"/> is null.</exception>
    public static PolicyReadResult Success(TelemetryPolicy policy)
    {
        Guard.ThrowIfNull(policy);

        return new(PolicyRejectionReason.None, policy, null);
    }

    /// <summary>
    /// Creates a rejected result.
    /// </summary>
    /// <param name="reason">The category of failure. Must not be <see cref="PolicyRejectionReason.None"/>.</param>
    /// <param name="error">A description of the failure. Must not be null or whitespace.</param>
    /// <returns>A rejected result.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="reason"/> is <see cref="PolicyRejectionReason.None"/>, or
    /// when <paramref name="error"/> is null, empty, or whitespace.
    /// </exception>
    public static PolicyReadResult Reject(PolicyRejectionReason reason, string error)
    {
        if (reason == PolicyRejectionReason.None)
        {
            throw new ArgumentException("A rejection must state a reason other than 'None'.", nameof(reason));
        }

        Guard.ThrowIfNullOrWhitespace(error);

        return new(reason, null, error);
    }

    /// <summary>
    /// Gets the validated policy, when present.
    /// </summary>
    /// <param name="policy">
    /// When this method returns <see langword="true"/>, the validated policy; otherwise
    /// <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the value was read successfully; otherwise <see langword="false"/>.
    /// </returns>
    public bool TryGetPolicy([NotNullWhen(true)] out TelemetryPolicy? policy)
    {
        policy = this.policy;
        return policy is not null;
    }
}
