// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.DynamicControl.Internal.Providers;

/// <summary>
/// Supplies a complete policy payload from one configured source.
/// </summary>
/// <remarks>
/// Implementations are registered once at coordinator construction. The coordinator
/// calls <see cref="FetchAsync"/> sequentially. A provider instance must belong to
/// only one coordinator and must not be fetched independently while registered.
/// </remarks>
internal interface ITelemetryPolicyProvider
{
    /// <summary>
    /// Gets the metadata that fully describes this provider: its identity, kind, and
    /// aggregation priority.
    /// </summary>
    /// <remarks>
    /// The coordinator reads and validates this value once at registration. Subsequent
    /// changes do not change the registered identity, kind, or priority.
    /// </remarks>
    PolicyProviderMetadata Metadata { get; }

    /// <summary>
    /// Fetches the current complete policy payload from this provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns <see langword="null"/> when the provider has no payload to offer yet.
    /// For example, a file that does not exist or a connection that has not yet delivered
    /// its first message. A <see langword="null"/> return is not retraction and is not
    /// change detection: the previously accepted snapshot for this provider, if any,
    /// remains current.
    /// </para>
    /// <para>
    /// Throws when a transport failure prevents the payload from being retrieved. The
    /// caller catches, records the failure, and retains the last accepted snapshot for
    /// this provider, so throwing must not be used to signal that no payload is
    /// available.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">
    /// A token that can interrupt an in-flight fetch. When cancelled, this method
    /// should throw <see cref="OperationCanceledException"/>.
    /// </param>
    /// <returns>
    /// The current payload, or <see langword="null"/> if the provider has nothing to offer yet.
    /// </returns>
    Task<PolicyProviderPayload?> FetchAsync(CancellationToken cancellationToken);
}
