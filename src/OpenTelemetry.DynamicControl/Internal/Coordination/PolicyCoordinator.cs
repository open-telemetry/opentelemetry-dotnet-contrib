// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Runtime.InteropServices;
using OpenTelemetry.DynamicControl.Internal.Diagnostics;
using OpenTelemetry.DynamicControl.Internal.Providers;
using OpenTelemetry.DynamicControl.Internal.Store;
using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Coordination;

/// <summary>
/// Fetches policy payloads from all registered providers and submits the resulting
/// validated policy sets to the policy store.
/// </summary>
/// <remarks>
/// <para>
/// Providers are registered once at construction; the set is fixed for the lifetime of
/// the coordinator. <see cref="RefreshAsync"/> is the single entry point: it fetches
/// every provider in registration order, parses each payload, and submits the validated
/// set to the store.
/// </para>
/// <para>
/// Fetch failures, malformed payloads, and individual entry rejections are recorded
/// and do not abort the remaining providers. Callers must await each refresh before
/// starting another.
/// </para>
/// </remarks>
internal sealed class PolicyCoordinator
{
    private readonly PolicyStore store;
    private readonly ImmutableArray<ITelemetryPolicyProvider> providers;
    private readonly ImmutableArray<PolicyProviderMetadata> metadata;

    private long sequence;
    private int refreshing;

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyCoordinator"/> class.
    /// </summary>
    /// <param name="store">The store that receives validated policy sets.</param>
    /// <param name="providers">
    /// The ordered set of policy providers. Processed in registration order on each refresh.
    /// Must not contain null elements or two providers sharing the same
    /// <see cref="ProviderRegistrationId"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="store"/> or <paramref name="providers"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="providers"/> contains a null element, a provider has default
    /// metadata, or two providers share the same <see cref="ProviderRegistrationId"/>.
    /// </exception>
    public PolicyCoordinator(PolicyStore store, IEnumerable<ITelemetryPolicyProvider> providers)
    {
        Guard.ThrowIfNull(store);
        Guard.ThrowIfNull(providers);

        this.store = store;

        var providerArray = providers.ToArray();
        var metadataArray = new PolicyProviderMetadata[providerArray.Length];
        var seenIds = new HashSet<ProviderRegistrationId>();

        for (var i = 0; i < providerArray.Length; i++)
        {
            var provider = providerArray[i]
                ?? throw new ArgumentException($"The providers list must not contain null elements (null at index {i}).", nameof(providers));

            var metadata = provider.Metadata;

            if (metadata == default)
            {
                throw new ArgumentException("A provider must not have default metadata.", nameof(providers));
            }

            if (!seenIds.Add(metadata.RegistrationId))
            {
                throw new ArgumentException(
                    $"Duplicate provider registration ID '{metadata.RegistrationId.Value}'. Each provider must have a unique ProviderRegistrationId.",
                    nameof(providers));
            }

            metadataArray[i] = metadata;
        }

        this.providers = ImmutableCollectionsMarshal.AsImmutableArray(providerArray);
        this.metadata = ImmutableCollectionsMarshal.AsImmutableArray(metadataArray);
    }

    /// <summary>
    /// Fetches the current payload from every registered provider and submits the
    /// validated policy set for each to the store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Providers are fetched sequentially in registration order. Concurrent or reentrant
    /// refresh calls are rejected without fetching any providers.
    /// </para>
    /// <para>
    /// Resubmissions carrying the same non-empty version token are suppressed. Payloads
    /// without a version token are applied on every refresh.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">
    /// A token that cancels the refresh. Cancellation stops before the next provider's
    /// fetch; work already submitted to the store remains committed.
    /// </param>
    /// <returns>A task that completes when the refresh finishes.</returns>
    /// <exception cref="InvalidOperationException">A refresh is already in progress.</exception>
    /// <exception cref="OperationCanceledException">The caller's <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(ref this.refreshing, 1, 0) != 0)
        {
            throw new InvalidOperationException("A policy refresh is already in progress.");
        }

        try
        {
            for (var i = 0; i < this.providers.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var provider = this.providers[i];
                var metadata = this.metadata[i];
                var registrationId = metadata.RegistrationId.Value;
                PolicyProviderPayload? payload;

                try
                {
                    payload = await provider.FetchAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // A transport can fail with any exception; isolate it so other providers refresh.
                    DynamicControlEventSource.Log.PolicyProviderFetchException(registrationId, ex);
                    continue;
                }

                // No payload is not a retraction; a decoded empty payload is.
                if (payload is null)
                {
                    continue;
                }

                // Stamp receipt before parsing, rather than ordering submissions by parse completion.
                var sequence = Interlocked.Increment(ref this.sequence);
                var version = payload.Version;
                PolicyPayloadParseResult result;

                try
                {
                    // The parser reads Content synchronously and does not retain a
                    // reference to it once Parse returns, so the pooled buffer backing
                    // it can be released as soon as parsing completes.
                    result = JsonKeyValuePolicyParser.Parse(payload.Content);
                }
                finally
                {
                    payload.Dispose();
                }

                if (result.IsMalformed)
                {
                    DynamicControlEventSource.Log.PolicyPayloadMalformed(registrationId, result.Error!);
                    continue;
                }

                foreach (var rejection in result.Rejections)
                {
                    DynamicControlEventSource.Log.PolicyEntryRejected(
                        registrationId,
                        rejection.Location.ToString(),
                        rejection.Reason.ToString(),
                        rejection.Message);
                }

                if (!result.IgnoredKeys.IsEmpty)
                {
                    DynamicControlEventSource.Log.PolicyPayloadKeysIgnored(registrationId, result.IgnoredKeys.Length);
                }

                // Validated registration and parser output make failure a coordinator invariant violation.
                if (!PolicyProviderSnapshot.TryCreate(
                        metadata,
                        sequence,
                        version,
                        result.Policies,
                        out var snapshot,
                        out var error))
                {
                    DynamicControlEventSource.Log.PolicyProviderSubmissionRejected(registrationId, error);
                    continue;
                }

                var update = this.store.ReplaceProvider(snapshot);

                switch (update.Status)
                {
                    case PolicyStoreUpdateStatus.Applied:
                        DynamicControlEventSource.Log.PolicyProviderSubmissionApplied(
                            registrationId,
                            snapshot.Sequence,
                            update.Revision,
                            snapshot.Policies.Length);
                        break;
                    case PolicyStoreUpdateStatus.SuppressedUnchangedVersion:
                        DynamicControlEventSource.Log.PolicyProviderSubmissionSuppressed(registrationId, snapshot.Sequence);
                        break;
                    default:
                        DynamicControlEventSource.Log.PolicyProviderSubmissionRejected(registrationId, update.Status.ToString());
                        break;
                }
            }
        }
        finally
        {
            Volatile.Write(ref this.refreshing, 0);
        }
    }
}
