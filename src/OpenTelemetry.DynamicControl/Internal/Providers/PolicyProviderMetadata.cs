// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Providers;

/// <summary>
/// Describes one configured policy provider: its identity, its kind, and its precedence
/// during aggregation.
/// </summary>
internal readonly struct PolicyProviderMetadata : IEquatable<PolicyProviderMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyProviderMetadata"/> struct.
    /// </summary>
    /// <param name="registrationId">The identity of the configured provider. Must not be <see cref="ProviderRegistrationId.Empty"/>.</param>
    /// <param name="kind">The kind of provider. Must not be <see cref="PolicyProviderKind.Unknown"/>.</param>
    /// <param name="priority">
    /// The aggregation precedence. Lower values win, matching the provider-priority
    /// convention from the Telemetry Policy OTEP (OpAmp=1, Http=2, File=3, Custom=1000).
    /// When omitted, the kind-derived default is used. Must be non-negative when supplied.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="registrationId"/> is <see cref="ProviderRegistrationId.Empty"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="kind"/> is <see cref="PolicyProviderKind.Unknown"/> or is
    /// not a defined <see cref="PolicyProviderKind"/> value, or when <paramref name="priority"/>
    /// is negative.
    /// </exception>
    public PolicyProviderMetadata(
        ProviderRegistrationId registrationId,
        PolicyProviderKind kind,
        int? priority = null)
    {
        Guard.ThrowIfDefault(registrationId);

        Guard.ThrowIfUndefinedOrDefault(kind);

        if (priority is { } priorityValue)
        {
            Guard.ThrowIfNegative(
                priorityValue,
                "The priority must be non-negative. Per the Telemetry Policy OTEP, lower values represent higher precedence (e.g. OpAmp=1, Http=2, File=3).",
                nameof(priority));
        }

        this.RegistrationId = registrationId;
        this.Kind = kind;
        this.Priority = priority ?? KindDefaultPriority(kind);
    }

    /// <summary>
    /// Gets the identity of the configured provider.
    /// </summary>
    public ProviderRegistrationId RegistrationId { get; }

    /// <summary>
    /// Gets the kind of provider.
    /// </summary>
    public PolicyProviderKind Kind { get; }

    /// <summary>
    /// Gets the precedence applied when several providers supply the same policy identity.
    /// </summary>
    /// <remarks>
    /// A lower value takes precedence over a higher one, matching the provider-priority
    /// convention from the Telemetry Policy OTEP (e.g. OpAmp=1, Http=2, File=3). Aggregation
    /// resolves equal priorities deterministically rather than by update order; those rules
    /// are defined with aggregation itself, not here.
    /// </remarks>
    public int Priority { get; }

    /// <summary>
    /// Determines whether two <see cref="PolicyProviderMetadata"/> instances describe the
    /// same provider in the same way.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns><see langword="true"/> if the values are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(PolicyProviderMetadata left, PolicyProviderMetadata right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="PolicyProviderMetadata"/> instances differ.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns><see langword="true"/> if the values are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(PolicyProviderMetadata left, PolicyProviderMetadata right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(PolicyProviderMetadata other)
        => this.RegistrationId.Equals(other.RegistrationId)
            && this.Kind == other.Kind
            && this.Priority == other.Priority;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PolicyProviderMetadata other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int hash;

#if NET || NETSTANDARD2_1_OR_GREATER
        hash = HashCode.Combine(this.RegistrationId, this.Kind, this.Priority);
#else
        unchecked
        {
            hash = (17 * 31) + this.RegistrationId.GetHashCode();
            hash = (hash * 31) + (int)this.Kind;
            hash = (hash * 31) + this.Priority;
        }
#endif

        return hash;
    }

    /// <summary>
    /// Returns a diagnostic representation of the provider metadata.
    /// </summary>
    /// <returns>The registration ID, kind, and priority, separated by forward slashes.</returns>
    public override string ToString()
        => this.RegistrationId.Value + "/" + this.Kind + "/" + this.Priority;

    private static int KindDefaultPriority(PolicyProviderKind kind) => kind switch
    {
        PolicyProviderKind.Custom => 1000,
        PolicyProviderKind.File => 3,
        PolicyProviderKind.Http => 2,
        PolicyProviderKind.OpAmp => 1,
        PolicyProviderKind.Unknown or _ =>
#if NET
            throw new System.Diagnostics.UnreachableException($"Unhandled {nameof(PolicyProviderKind)}: {kind}"),
#else
            throw new InvalidOperationException($"Unhandled {nameof(PolicyProviderKind)}: {kind}"),
#endif
    };
}
