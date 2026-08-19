// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Sources;

/// <summary>
/// Describes one configured policy source: its identity, its kind, and its precedence
/// during aggregation.
/// </summary>
internal readonly struct PolicySourceMetadata : IEquatable<PolicySourceMetadata>
{
    // The precedence applied to a source that does not specify one.
    // Lower values represent higher precedence, so a source that does not opt into
    // explicit ordering is assigned the numerically highest value,
    // ensuring it cannot outrank any explicitly prioritized source.
    private const int DefaultPriority = int.MaxValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicySourceMetadata"/> struct.
    /// </summary>
    /// <param name="registrationId">The identity of the configured source. Must not be <see cref="SourceRegistrationId.None"/>.</param>
    /// <param name="kind">The kind of source. Must not be <see cref="PolicySourceKind.Unknown"/>.</param>
    /// <param name="priority">
    /// The aggregation precedence. Lower values win, matching the provider-priority
    /// convention from the Telemetry Policy OTEP (e.g. OpAmp=1, Http=2, File=3). Must be
    /// non-negative.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="registrationId"/> is <see cref="SourceRegistrationId.None"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="kind"/> is <see cref="PolicySourceKind.Unknown"/> or is
    /// not a defined <see cref="PolicySourceKind"/> value, or when <paramref name="priority"/>
    /// is negative.
    /// </exception>
    public PolicySourceMetadata(
        SourceRegistrationId registrationId,
        PolicySourceKind kind,
        int priority = DefaultPriority)
    {
        Guard.ThrowIfDefault(registrationId);

        Guard.ThrowIfUndefinedOrDefault(kind);

        Guard.ThrowIfNegative(
            priority,
            "The priority must be non-negative. Per the Telemetry Policy OTEP, lower values represent higher precedence (e.g. OpAmp=1, Http=2, File=3).",
            nameof(priority));

        this.RegistrationId = registrationId;
        this.Kind = kind;
        this.Priority = priority;
    }

    /// <summary>
    /// Gets the identity of the configured source.
    /// </summary>
    public SourceRegistrationId RegistrationId { get; }

    /// <summary>
    /// Gets the kind of source.
    /// </summary>
    public PolicySourceKind Kind { get; }

    /// <summary>
    /// Gets the precedence applied when several sources supply the same policy identity.
    /// </summary>
    /// <remarks>
    /// A lower value takes precedence over a higher one, matching the provider-priority
    /// convention from the Telemetry Policy OTEP (e.g. OpAmp=1, Http=2, File=3). Aggregation
    /// resolves equal priorities deterministically rather than by update order; those rules
    /// are defined with aggregation itself, not here.
    /// </remarks>
    public int Priority { get; }

    /// <summary>
    /// Determines whether two <see cref="PolicySourceMetadata"/> instances describe the
    /// same source in the same way.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns><see langword="true"/> if the values are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(PolicySourceMetadata left, PolicySourceMetadata right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="PolicySourceMetadata"/> instances differ.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns><see langword="true"/> if the values are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(PolicySourceMetadata left, PolicySourceMetadata right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(PolicySourceMetadata other)
        => this.RegistrationId.Equals(other.RegistrationId)
            && this.Kind == other.Kind
            && this.Priority == other.Priority;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PolicySourceMetadata other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
#if NET || NETSTANDARD2_1_OR_GREATER
        return HashCode.Combine(this.RegistrationId, this.Kind, this.Priority);
#else
        unchecked
        {
            var hash = (17 * 31) + this.RegistrationId.GetHashCode();
            hash = (hash * 31) + (int)this.Kind;
            return (hash * 31) + this.Priority;
        }
#endif
    }

    /// <summary>
    /// Returns a diagnostic representation of the source metadata.
    /// </summary>
    /// <returns>The registration ID, kind, and priority, separated by forward slashes.</returns>
    public override string ToString()
        => this.RegistrationId.Value + "/" + this.Kind + "/" + this.Priority;
}
