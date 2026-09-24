// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using System.Text;
using OpenTelemetry.DynamicControl.Internal.Coordination;
using OpenTelemetry.DynamicControl.Internal.Diagnostics;
using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Providers;
using OpenTelemetry.DynamicControl.Internal.Store;
using OpenTelemetry.Tests;

namespace OpenTelemetry.DynamicControl.Tests;

[Collection(DynamicControlEventSourceCollectionDefinition.Name)]
public class PolicyCoordinatorTests
{
    [Fact]
    public void Constructor_NullStore_Throws()
        => Assert.Throws<ArgumentNullException>("store", () => new PolicyCoordinator(null!, []));

    [Fact]
    public void Constructor_NullProviders_Throws()
        => Assert.Throws<ArgumentNullException>("providers", () => new PolicyCoordinator(new PolicyStore(), null!));

    [Fact]
    public void Constructor_NullProviderElement_Throws()
    {
        using var store = new PolicyStore();
        var ex = Assert.Throws<ArgumentException>(() =>
            new PolicyCoordinator(store, [null!]));

        Assert.Contains("providers", ex.ParamName);
    }

    [Fact]
    public void Constructor_DuplicateRegistrationId_Throws()
    {
        using var store = new PolicyStore();
        var p1 = new FakeProvider("duplicate-id", PolicyProviderKind.OpAmp);
        var p2 = new FakeProvider("duplicate-id", PolicyProviderKind.File);

        var ex = Assert.Throws<ArgumentException>(() => new PolicyCoordinator(store, [p1, p2]));

        Assert.Contains("providers", ex.ParamName);
        Assert.Contains("duplicate-id", ex.Message);
    }

    [Fact]
    public void Constructor_DefaultMetadata_Throws()
    {
        using var store = new PolicyStore();
        var provider = new FakeProvider("p1", PolicyProviderKind.File)
        {
            CurrentMetadata = default,
        };

        Assert.Throws<ArgumentException>("providers", () => new PolicyCoordinator(store, [provider]));
    }

    [Fact]
    public async Task RefreshAsync_MetadataChangedAfterConstruction_UsesRegisteredMetadata()
    {
        using var store = new PolicyStore();
        var first = new FakeProvider("first", PolicyProviderKind.File, Json("""{"sampling_rate": 0.1}"""));
        var second = new FakeProvider("second", PolicyProviderKind.OpAmp, Json("""{"sampling_rate": 0.9}"""));
        var registeredMetadata = second.CurrentMetadata;
        var coordinator = new PolicyCoordinator(store, [first, second]);
        second.CurrentMetadata = first.CurrentMetadata;

        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.True(store.Current.TryGetProvider(new ProviderRegistrationId("first"), out var firstSnapshot), "Provider 'first' should be present in the store.");
        Assert.True(store.Current.TryGetProvider(new ProviderRegistrationId("second"), out var secondSnapshot), "Provider 'second' should be present in the store.");
        Assert.Equal(registeredMetadata, secondSnapshot.Metadata);
        Assert.Equal(0.1, Assert.IsType<TraceSamplingRatePolicy>(Assert.Single(firstSnapshot.Policies)).SamplingProbability);
        Assert.Equal(0.9, Assert.IsType<TraceSamplingRatePolicy>(Assert.Single(secondSnapshot.Policies)).SamplingProbability);
        Assert.Equal(1, first.MetadataReads);
        Assert.Equal(1, second.MetadataReads);
    }

    [Fact]
    public async Task RefreshAsync_MetadataGetterUnavailableAfterFetchFailure_OtherProviderStillUpdates()
    {
        using var store = new PolicyStore();
        using var listener = new InMemoryEventListener(DynamicControlEventSource.Log);
        var failed = new FakeProvider("failed", PolicyProviderKind.File);
        failed.FetchAsyncCallback = async _ =>
        {
            await Task.Yield();
            failed.MetadataException = new InvalidOperationException("metadata unavailable");
            throw new IOException("transport failure");
        };
        var healthy = new FakeProvider("healthy", PolicyProviderKind.File, Json("""{"sampling_rate": 0.5}"""));
        var coordinator = new PolicyCoordinator(store, [failed, healthy]);

        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.True(store.Current.TryGetProvider(new ProviderRegistrationId("healthy"), out _), "Provider 'healthy' should be present in the store.");
        var fetchFailed = Assert.Single(listener.Events, e => e.EventId == 2);
        Assert.Equal("failed", fetchFailed.Payload![0]);
        Assert.Contains("transport failure", Assert.IsType<string>(fetchFailed.Payload[1]));
        Assert.Equal(1, failed.MetadataReads);

        failed.FetchAsyncCallback = null;
        failed.NextPayload = Json("""{"sampling_rate": 0.8}""");
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.True(store.Current.TryGetProvider(new ProviderRegistrationId("failed"), out _), "Provider 'failed' should be present in the store after recovering.");
        Assert.Equal(1, failed.MetadataReads);
    }

    [Fact]
    public async Task Constructor_ZeroProviders_IsLegal_StoreUntouched()
    {
        using var store = new PolicyStore();
        var coordinator = new PolicyCoordinator(store, []);

        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Same(PolicyStoreSnapshot.Empty, store.Current);
    }

    [Fact]
    public async Task RefreshAsync_ValidPayload_StoreContainsPolicies_RevisionBumped()
    {
        using var store = new PolicyStore();
        var provider = new FakeProvider(
            "p1",
            PolicyProviderKind.File,
            Json("""{"sampling_rate": 0.5}"""));
        var coordinator = new PolicyCoordinator(store, [provider]);

        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, store.Current.Revision);
        var found = store.Current.TryGetProvider(new ProviderRegistrationId("p1"), out var snapshot);
        Assert.True(found, "Provider p1 should be present in the store.");
        Assert.Single(snapshot!.Policies);
    }

    [Fact]
    public async Task RefreshAsync_MalformedPayloadAfterSuccessfulRefresh_PreviousSnapshotRetained()
    {
        using var store = new PolicyStore();
        var provider = new FakeProvider(
            "p1",
            PolicyProviderKind.File,
            Json("""{"sampling_rate": 0.5}"""));
        var coordinator = new PolicyCoordinator(store, [provider]);
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        var revisionAfterFirst = store.Current.Revision;

        // Corrupt bytes cannot be decoded. The first snapshot must survive.
        provider.NextPayload = new PolicyProviderPayload([0xFF, 0xFE, 0x00]);
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(revisionAfterFirst, store.Current.Revision);
        store.Current.TryGetProvider(new ProviderRegistrationId("p1"), out var snapshot);
        Assert.Single(snapshot!.Policies);
    }

    [Fact]
    public async Task RefreshAsync_MixedEntries_OnlyValidSetCommitted_InvalidPolicyRemoved()
    {
        using var store = new PolicyStore();

        var provider = new FakeProvider(
            "p1",
            PolicyProviderKind.File,
            Json("""{"sampling_rate": 0.5, "log_level": "Information"}"""));
        var coordinator = new PolicyCoordinator(store, [provider]);
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);
        store.Current.TryGetProvider(new ProviderRegistrationId("p1"), out var snap1);
        Assert.Equal(2, snap1!.Policies.Length);

        provider.NextPayload = Json("""{"sampling_rate": "not-a-number", "log_level": "Error"}""");
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        store.Current.TryGetProvider(new ProviderRegistrationId("p1"), out var snap2);
        Assert.NotNull(snap2);
        var logLevel = Assert.IsType<LogLevelPolicy>(Assert.Single(snap2.Policies));
        Assert.Equal(DiagnosticLogLevel.Error, logLevel.MinimumLevel);
    }

    [Fact]
    public async Task RefreshAsync_PolicyOmittedFromReplacement_PreviousPolicyRemoved()
    {
        using var store = new PolicyStore();
        var provider = new FakeProvider(
            "p1",
            PolicyProviderKind.File,
            Json("""{"sampling_rate": 0.5, "log_level": "Information"}"""));
        var coordinator = new PolicyCoordinator(store, [provider]);
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.True(store.Current.TryGetProvider(new ProviderRegistrationId("p1"), out var before), "Provider 'p1' should be present in the store before the update.");
        Assert.Equal(2, before.Policies.Length);

        provider.NextPayload = Json("""{"log_level": "Error"}""");
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.True(store.Current.TryGetProvider(new ProviderRegistrationId("p1"), out var after), "Provider 'p1' should be present in the store after the update.");
        var logLevel = Assert.IsType<LogLevelPolicy>(Assert.Single(after.Policies));
        Assert.Equal(DiagnosticLogLevel.Error, logLevel.MinimumLevel);
    }

    [Fact]
    public async Task RefreshAsync_InvalidUtf8InString_RetractsRejectedPolicy()
    {
        using var store = new PolicyStore();
        var provider = new FakeProvider("p1", PolicyProviderKind.File, Json("""{"sampling_rate": 0.5}"""));
        var coordinator = new PolicyCoordinator(store, [provider]);
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);
        var previousRevision = store.Current.Revision;
        byte[] content =
        [
            .. Encoding.UTF8.GetBytes("{\"sampling_rate\":\""),
            0xC3, 0x28,
            .. Encoding.UTF8.GetBytes("\"}"),
        ];
        provider.NextPayload = new PolicyProviderPayload(content);

        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(previousRevision + 1, store.Current.Revision);
        Assert.True(store.Current.TryGetProvider(new ProviderRegistrationId("p1"), out var snapshot), "Provider 'p1' should be present in the store.");
        Assert.Empty(snapshot.Policies);
    }

    [Fact]
    public async Task RefreshAsync_AllEntriesInvalid_EmptySnapshotCommitted_RevisionBumped()
    {
        // A successfully decoded payload with every entry invalid commits an empty set.
        // This is a retraction: the provider intentionally supplies nothing usable.
        using var store = new PolicyStore();
        var provider = new FakeProvider(
            "p1",
            PolicyProviderKind.File,
            Json("""{"sampling_rate": 0.5}"""));
        var coordinator = new PolicyCoordinator(store, [provider]);
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        var revisionAfterFirst = store.Current.Revision;

        provider.NextPayload = Json("""{"sampling_rate": "bad-value"}""");
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(revisionAfterFirst + 1, store.Current.Revision);
        store.Current.TryGetProvider(new ProviderRegistrationId("p1"), out var snapshot);
        Assert.NotNull(snapshot);
        Assert.True(snapshot!.IsEmpty, "An all-invalid payload commits an empty snapshot (retraction).");
    }

    [Fact]
    public async Task RefreshAsync_UnrecognisedKeys_PoliciesStillCommitted_IgnoredKeyEventEmitted()
    {
        using var store = new PolicyStore();
        using var listener = new InMemoryEventListener(DynamicControlEventSource.Log);
        var provider = new FakeProvider(
            "p1",
            PolicyProviderKind.File,
            Json("""{"sampling_rate": 0.5, "unknown-key": "ignored"}"""));
        var coordinator = new PolicyCoordinator(store, [provider]);

        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, store.Current.Revision);

        var ignoredEvent = listener.Events.FirstOrDefault(e => e.EventId == 5);
        Assert.NotNull(ignoredEvent);
        Assert.Equal("p1", ignoredEvent!.Payload![0]);
        Assert.Equal(1, (int)ignoredEvent.Payload[1]!);
    }

    [Fact]
    public async Task RefreshAsync_NullPayload_NoSubmission_PreviousSnapshotSurvives()
    {
        using var store = new PolicyStore();
        var provider = new FakeProvider(
            "p1",
            PolicyProviderKind.File,
            Json("""{"sampling_rate": 0.5}"""));
        var coordinator = new PolicyCoordinator(store, [provider]);
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);
        var revisionAfterFirst = store.Current.Revision;

        provider.NextPayload = null;
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(revisionAfterFirst, store.Current.Revision);
        store.Current.TryGetProvider(new ProviderRegistrationId("p1"), out var snapshot);
        Assert.Single(snapshot!.Policies);
    }

    [Fact]
    public async Task RefreshAsync_FetchThrows_NoSubmission_PreviousSnapshotSurvives_EventEmitted()
    {
        using var store = new PolicyStore();
        using var listener = new InMemoryEventListener(DynamicControlEventSource.Log);
        var provider = new FakeProvider(
            "p1",
            PolicyProviderKind.File,
            Json("""{"sampling_rate": 0.5}"""));
        var coordinator = new PolicyCoordinator(store, [provider]);
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);
        var revisionAfterFirst = store.Current.Revision;

        provider.ThrowOnFetch = new InvalidOperationException("transport failure");
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(revisionAfterFirst, store.Current.Revision);
        var fetchFailed = listener.Events.FirstOrDefault(e => e.EventId == 2);
        Assert.NotNull(fetchFailed);
        Assert.Equal("p1", fetchFailed!.Payload![0]);
        Assert.Contains("transport failure", (string)fetchFailed.Payload[1]!);
    }

    [Fact]
    public async Task RefreshAsync_SamplingProbabilityZero_CommittedAsActivePolicy()
    {
        // Sampling probability of 0 is a policy requesting zero sampling, not a retraction.
        using var store = new PolicyStore();
        var provider = new FakeProvider(
            "p1",
            PolicyProviderKind.File,
            Json("""{"sampling_rate": 0.0}"""));
        var coordinator = new PolicyCoordinator(store, [provider]);

        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        store.Current.TryGetProvider(new ProviderRegistrationId("p1"), out var snapshot);
        Assert.NotNull(snapshot);
        Assert.False(snapshot!.IsEmpty, "A sampling rate of 0 is an active policy, not a retraction.");
        var policy = Assert.Single(snapshot.Policies);
        var samplingPolicy = Assert.IsType<TraceSamplingRatePolicy>(policy);
        Assert.Equal(0.0, samplingPolicy.SamplingProbability);
    }

    [Fact]
    public async Task RefreshAsync_TwoProviders_ReceiveDistinctSequencesGreaterThanZero()
    {
        using var store = new PolicyStore();
        var p1 = new FakeProvider(
            "p1",
            PolicyProviderKind.OpAmp,
            Json("""{"sampling_rate": 0.5}"""));
        var p2 = new FakeProvider(
            "p2",
            PolicyProviderKind.File,
            Json("""{"sampling_rate": 0.7}"""));
        var coordinator = new PolicyCoordinator(store, [p1, p2]);

        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        store.Current.TryGetProvider(new ProviderRegistrationId("p1"), out var snap1);
        store.Current.TryGetProvider(new ProviderRegistrationId("p2"), out var snap2);

        Assert.True(snap1!.Sequence >= 1, "p1 sequence must be >= 1.");
        Assert.True(snap2!.Sequence >= 1, "p2 sequence must be >= 1.");
        Assert.NotEqual(snap1.Sequence, snap2.Sequence);
    }

    [Fact]
    public async Task RefreshAsync_SecondRefresh_IssuesHigherSequencesThanFirst()
    {
        using var store = new PolicyStore();
        var provider = new FakeProvider(
            "p1",
            PolicyProviderKind.File,
            Json("""{"sampling_rate": 0.5}"""));
        var coordinator = new PolicyCoordinator(store, [provider]);

        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);
        store.Current.TryGetProvider(new ProviderRegistrationId("p1"), out var after1);
        var seqAfterFirst = after1!.Sequence;

        provider.NextPayload = Json("""{"sampling_rate": 0.8}""");
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);
        store.Current.TryGetProvider(new ProviderRegistrationId("p1"), out var after2);

        Assert.True(after2!.Sequence > seqAfterFirst, "Sequence issued by the second refresh must be higher than the first.");
    }

    [Fact]
    public async Task RefreshAsync_NullPayloadConsumesNoSequence_ThrowingFetchConsumesNoSequence()
    {
        using var store = new PolicyStore();

        // The throwing provider comes first but consumes no sequence on failure.
        // The returning provider's snapshot must have sequence = 1 (the first consumed).
        var throwing = new FakeProvider("throwing", PolicyProviderKind.OpAmp)
        {
            ThrowOnFetch = new IOException("simulated network error"),
        };
        var returning = new FakeProvider(
            "returning",
            PolicyProviderKind.File,
            Json("""{"sampling_rate": 0.5}"""));
        var noPayload = new FakeProvider("no-payload", PolicyProviderKind.File);
        var coordinator = new PolicyCoordinator(store, [throwing, noPayload, returning]);

        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        store.Current.TryGetProvider(new ProviderRegistrationId("returning"), out var snap);
        Assert.Equal(1, snap!.Sequence);
    }

    [Fact]
    public async Task RefreshAsync_FirstProviderThrows_OtherTwoAreStillFetchedAndSubmitted()
    {
        using var store = new PolicyStore();
        var p1 = new FakeProvider("p1", PolicyProviderKind.OpAmp)
        {
            ThrowOnFetch = new IOException("simulated error"),
        };
        var p2 = new FakeProvider(
            "p2",
            PolicyProviderKind.Http,
            Json("""{"sampling_rate": 0.5}"""));
        var p3 = new FakeProvider(
            "p3",
            PolicyProviderKind.File,
            Json("""{"log_level": "Warning"}"""));
        var coordinator = new PolicyCoordinator(store, [p1, p2, p3]);

        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        var found2 = store.Current.TryGetProvider(new ProviderRegistrationId("p2"), out var snap2);
        var found3 = store.Current.TryGetProvider(new ProviderRegistrationId("p3"), out var snap3);
        Assert.True(found2, "p2 should be present even though p1 threw.");
        Assert.True(found3, "p3 should be present even though p1 threw.");
        Assert.Single(snap2!.Policies);
        Assert.Single(snap3!.Policies);
    }

    [Fact]
    public async Task RefreshAsync_ProviderRecovery_SucceedsOnNextRefreshWithNoSpecialHandling()
    {
        using var store = new PolicyStore();
        var provider = new FakeProvider("p1", PolicyProviderKind.File)
        {
            ThrowOnFetch = new IOException("first call fails"),
        };
        var coordinator = new PolicyCoordinator(store, [provider]);

        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Same(PolicyStoreSnapshot.Empty, store.Current);

        provider.ThrowOnFetch = null;
        provider.NextPayload = Json("""{"sampling_rate": 0.5}""");
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, store.Current.Revision);
    }

    [Fact]
    public async Task RefreshAsync_IdenticalPayloadAndVersion_SuppressedUnchangedVersion_RevisionUnchanged()
    {
        using var store = new PolicyStore();
        var version = new PolicyProviderVersion("v1");
        var content = Utf8Bytes("""{"sampling_rate": 0.5}""");
        var provider = new FakeProvider(
            "p1",
            PolicyProviderKind.File,
            new PolicyProviderPayload(content, version));
        var coordinator = new PolicyCoordinator(store, [provider]);

        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);
        var revisionAfterFirst = store.Current.Revision;
        using var listener = new InMemoryEventListener(DynamicControlEventSource.Log);

        provider.NextPayload = new PolicyProviderPayload(content, version);
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(revisionAfterFirst, store.Current.Revision);
        var suppressed = listener.Events.Single(e => e.EventId == 8);
        Assert.Equal(EventLevel.Verbose, suppressed.Level);
        Assert.Equal("p1", suppressed.Payload![0]);
        Assert.Equal(2L, suppressed.Payload[1]);
    }

    [Fact]
    public async Task RefreshAsync_IdenticalPayloadWithEmptyVersion_AppliedBothTimes()
    {
        using var store = new PolicyStore();
        var content = Utf8Bytes("""{"sampling_rate": 0.5}""");
        var provider = new FakeProvider(
            "p1",
            PolicyProviderKind.File,
            new PolicyProviderPayload(content));
        var coordinator = new PolicyCoordinator(store, [provider]);

        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);
        var revisionAfterFirst = store.Current.Revision;

        provider.NextPayload = new PolicyProviderPayload(content);
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        // Empty version means no change detection; the second submission is applied.
        Assert.Equal(revisionAfterFirst + 1, store.Current.Revision);
    }

    [Fact]
    public async Task RefreshAsync_OverlappingCalls_RejectsWithoutFetchingOrReleasingActiveRefresh()
    {
        using var store = new PolicyStore();
        var completion = new TaskCompletionSource<PolicyProviderPayload?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new FakeProvider("p1", PolicyProviderKind.File)
        {
            FetchAsyncCallback = _ => completion.Task,
        };
        var coordinator = new PolicyCoordinator(store, [provider]);
        var activeRefresh = coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        try
        {
            Assert.False(activeRefresh.IsCompleted);
            var overlappingRefresh = coordinator.RefreshAsync(TestContext.Current.CancellationToken);
            await WaitHelper.WaitUntil(() => overlappingRefresh.IsCompleted);
            await Assert.ThrowsAsync<InvalidOperationException>(() => overlappingRefresh);
            var anotherOverlappingRefresh = coordinator.RefreshAsync(TestContext.Current.CancellationToken);
            await WaitHelper.WaitUntil(() => anotherOverlappingRefresh.IsCompleted);
            await Assert.ThrowsAsync<InvalidOperationException>(() => anotherOverlappingRefresh);
            Assert.Equal(1, provider.FetchCount);
            Assert.Same(PolicyStoreSnapshot.Empty, store.Current);
        }
        finally
        {
            completion.TrySetResult(Json("""{"sampling_rate": 0.5}"""));
            await activeRefresh;
        }

        // The completed task's payload was already consumed by activeRefresh; the next
        // refresh must fetch its own, fresh payload rather than reusing that instance.
        provider.FetchAsyncCallback = null;
        provider.NextPayload = Json("""{"sampling_rate": 0.5}""");
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, provider.FetchCount);
        Assert.Equal(2, store.Current.Revision);
    }

    [Fact]
    public async Task RefreshAsync_ReentrantCall_RejectsWithoutFetchingAgain()
    {
        using var store = new PolicyStore();
        var provider = new FakeProvider("p1", PolicyProviderKind.File);
        var coordinator = new PolicyCoordinator(store, [provider]);
        provider.FetchAsyncCallback = async token =>
        {
            if (provider.FetchCount == 1)
            {
                var reentrantRefresh = coordinator.RefreshAsync(token);
                await WaitHelper.WaitUntil(() => reentrantRefresh.IsCompleted);
                await Assert.ThrowsAsync<InvalidOperationException>(() => reentrantRefresh);
            }

            return Json("""{"sampling_rate": 0.5}""");
        };

        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, provider.FetchCount);
        Assert.Equal(1, store.Current.Revision);
    }

    [Fact]
    public async Task RefreshAsync_CancelledDuringAsyncFetch_ReleasesGuardForNextRefresh()
    {
        using var store = new PolicyStore();
        using var cts = new CancellationTokenSource();
        var completion = new TaskCompletionSource<PolicyProviderPayload?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new FakeProvider("p1", PolicyProviderKind.File)
        {
            FetchAsyncCallback = async token =>
            {
                using var registration = token.Register(() => completion.TrySetCanceled());
                return await completion.Task;
            },
        };
        var coordinator = new PolicyCoordinator(store, [provider]);
        var cancelledRefresh = coordinator.RefreshAsync(cts.Token);
        Assert.False(cancelledRefresh.IsCompleted);

        cts.Cancel();
        await WaitHelper.WaitUntil(() => cancelledRefresh.IsCompleted);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledRefresh);
        Assert.Same(PolicyStoreSnapshot.Empty, store.Current);

        provider.FetchAsyncCallback = null;
        provider.NextPayload = Json("""{"sampling_rate": 0.5}""");
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, provider.FetchCount);
        Assert.Equal(1, store.Current.Revision);
    }

    [Fact]
    public async Task RefreshAsync_AlreadyCancelledWithNoProviders_Throws()
    {
        using var store = new PolicyStore();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var coordinator = new PolicyCoordinator(store, []);

        await Assert.ThrowsAsync<OperationCanceledException>(() => coordinator.RefreshAsync(cts.Token));
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Same(PolicyStoreSnapshot.Empty, store.Current);
    }

    [Fact]
    public async Task RefreshAsync_AlreadyCancelledToken_ThrowsOperationCancelled_NoProviderFetched()
    {
        using var store = new PolicyStore();
        var provider = new FakeProvider(
            "p1",
            PolicyProviderKind.File,
            Json("""{"sampling_rate": 0.5}"""));
        var coordinator = new PolicyCoordinator(store, [provider]);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            coordinator.RefreshAsync(cts.Token));

        Assert.False(provider.WasFetched, "No provider should be fetched when the token is already cancelled.");
    }

    [Fact]
    public async Task RefreshAsync_CancelledBetweenProviders_RemainingProvidersNotFetched_WorkAlreadySubmittedRemains()
    {
        using var store = new PolicyStore();
        using var cts = new CancellationTokenSource();

        var p1 = new FakeProvider(
            "p1",
            PolicyProviderKind.OpAmp,
            Json("""{"sampling_rate": 0.5}"""));

        // p2 cancels the token as part of its fetch, simulating cancellation between providers.
        var p2 = new FakeProvider("p2", PolicyProviderKind.Http)
        {
            OnFetch = () => cts.Cancel(),
        };
        var p3 = new FakeProvider(
            "p3",
            PolicyProviderKind.File,
            Json("""{"log_level": "Warning"}"""));

        var coordinator = new PolicyCoordinator(store, [p1, p2, p3]);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            coordinator.RefreshAsync(cts.Token));

        var foundP1 = store.Current.TryGetProvider(new ProviderRegistrationId("p1"), out _);
        var foundP3 = store.Current.TryGetProvider(new ProviderRegistrationId("p3"), out _);
        Assert.True(foundP1, "p1's committed work should remain after cancellation.");
        Assert.False(foundP3, "p3 should not have been fetched after cancellation.");
    }

    [Fact]
    public async Task RefreshAsync_ProviderThrowsOperationCancelledOnUnrelatedToken_TreatedAsProviderFailure()
    {
        using var store = new PolicyStore();
        using var listener = new InMemoryEventListener(DynamicControlEventSource.Log);
        var provider = new FakeProvider("p1", PolicyProviderKind.File)
        {
            ThrowOnFetch = new OperationCanceledException("provider's own internal cancellation"),
        };
        var coordinator = new PolicyCoordinator(store, [provider]);

        // The caller's token is NOT cancelled; only the provider's internal token is.
        await coordinator.RefreshAsync(CancellationToken.None);

        var fetchFailed = listener.Events.FirstOrDefault(e => e.EventId == 2);
        Assert.NotNull(fetchFailed);
        Assert.Equal("p1", fetchFailed!.Payload![0]);
    }

    [Fact]
    public async Task RefreshAsync_MalformedPayload_EmitsEventId3()
    {
        using var store = new PolicyStore();
        using var listener = new InMemoryEventListener(DynamicControlEventSource.Log);
        var provider = new FakeProvider(
            "p1",
            PolicyProviderKind.File,
            new PolicyProviderPayload([0xFF, 0xFE]));
        var coordinator = new PolicyCoordinator(store, [provider]);

        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        var malformed = listener.Events.FirstOrDefault(e => e.EventId == 3);
        Assert.NotNull(malformed);
        Assert.Equal("p1", malformed!.Payload![0]);
    }

    [Fact]
    public async Task RefreshAsync_EntryRejected_EmitsEventId4()
    {
        using var store = new PolicyStore();
        using var listener = new InMemoryEventListener(DynamicControlEventSource.Log);
        var provider = new FakeProvider(
            "p1",
            PolicyProviderKind.File,
            Json("""{"sampling_rate": "not-valid"}"""));
        var coordinator = new PolicyCoordinator(store, [provider]);

        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        var rejected = listener.Events.FirstOrDefault(e => e.EventId == 4);
        Assert.NotNull(rejected);
        Assert.Equal("p1", rejected!.Payload![0]);
    }

    [Fact]
    public async Task RefreshAsync_ValidPayload_EmitsEventId7WithCorrectFields()
    {
        using var store = new PolicyStore();
        using var listener = new InMemoryEventListener(DynamicControlEventSource.Log);
        var provider = new FakeProvider(
            "p1",
            PolicyProviderKind.File,
            Json("""{"sampling_rate": 0.5}"""));
        var coordinator = new PolicyCoordinator(store, [provider]);

        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        var applied = listener.Events.Single(e => e.EventId == 7);
        Assert.Equal("p1", applied.Payload![0]);
        Assert.True((long)applied.Payload[1]! >= 1, "Sequence must be >= 1.");
        Assert.Equal(1L, (long)applied.Payload[2]!);
        Assert.Equal(1, (int)applied.Payload[3]!);
    }

    [Fact]
    public async Task RefreshAsync_Completes_EmitsRefreshCompletedEvent()
    {
        using var store = new PolicyStore();
        using var listener = new InMemoryEventListener(DynamicControlEventSource.Log);
        var provider = new FakeProvider("p1", PolicyProviderKind.File, Json("""{"sampling_rate": 0.5}"""));
        var coordinator = new PolicyCoordinator(store, [provider]);

        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        var completed = listener.Events.Single(e => e.EventId == 9);
        Assert.Equal(EventLevel.Verbose, completed.Level);
        Assert.Equal(1, (int)completed.Payload![0]!);
        var elapsed = Assert.IsType<string>(completed.Payload[1]);
        Assert.Matches(@"^\d+\.\d{2}ms$", elapsed);
    }

    [Fact]
    public async Task RefreshAsync_Cancelled_DoesNotEmitRefreshCompletedEvent()
    {
        using var store = new PolicyStore();
        using var cts = new CancellationTokenSource();
        using var listener = new InMemoryEventListener(DynamicControlEventSource.Log);
        var p1 = new FakeProvider("p1", PolicyProviderKind.OpAmp, Json("""{"sampling_rate": 0.5}"""));
        var p2 = new FakeProvider("p2", PolicyProviderKind.Http) { OnFetch = () => cts.Cancel() };
        var coordinator = new PolicyCoordinator(store, [p1, p2]);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            coordinator.RefreshAsync(cts.Token));

        Assert.DoesNotContain(listener.Events, e => e.EventId == 9);
    }

    [Fact]
    public async Task RefreshAsync_SharedStore_MetadataMismatch_EmitsSubmissionRejectedEvent()
    {
        // Two coordinators share a store. Coordinator A commits provider "p1" with File
        // metadata. Coordinator B then tries to submit the same ID with OpAmp metadata.
        // The store rejects it with RejectedMetadataMismatch, hitting the default switch case.
        using var store = new PolicyStore();
        var providerA = new FakeProvider("p1", PolicyProviderKind.File, Json("""{"sampling_rate": 0.5}"""));
        var coordinatorA = new PolicyCoordinator(store, [providerA]);
        var providerB = new FakeProvider("p1", PolicyProviderKind.OpAmp, Json("""{"sampling_rate": 0.7}"""));
        var coordinatorB = new PolicyCoordinator(store, [providerB]);

        await coordinatorA.RefreshAsync(TestContext.Current.CancellationToken);
        var revisionAfterA = store.Current.Revision;
        using var listener = new InMemoryEventListener(DynamicControlEventSource.Log);

        await coordinatorB.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(revisionAfterA, store.Current.Revision);
        var rejected = Assert.Single(listener.Events, e => e.EventId == 6);
        Assert.Equal("p1", rejected.Payload![0]);
        Assert.Contains("RejectedMetadataMismatch", Assert.IsType<string>(rejected.Payload[1]));
    }

    [Fact]
    public async Task RefreshAsync_SharedStore_StaleSequence_EmitsSubmissionRejectedEvent()
    {
        // Two coordinators share a store and register the same provider ID with identical
        // metadata. Coordinator A's first refresh commits sequence 1. Coordinator B's first
        // refresh also produces sequence 1, which the store rejects as stale.
        using var store = new PolicyStore();
        var providerA = new FakeProvider("p1", PolicyProviderKind.File, Json("""{"sampling_rate": 0.5}"""));
        var coordinatorA = new PolicyCoordinator(store, [providerA]);
        var providerB = new FakeProvider("p1", PolicyProviderKind.File, Json("""{"sampling_rate": 0.7}"""));
        var coordinatorB = new PolicyCoordinator(store, [providerB]);

        await coordinatorA.RefreshAsync(TestContext.Current.CancellationToken);
        var revisionAfterA = store.Current.Revision;
        using var listener = new InMemoryEventListener(DynamicControlEventSource.Log);

        await coordinatorB.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(revisionAfterA, store.Current.Revision);
        var rejected = Assert.Single(listener.Events, e => e.EventId == 6);
        Assert.Equal("p1", rejected.Payload![0]);
        Assert.Contains("RejectedStaleSequence", Assert.IsType<string>(rejected.Payload[1]));
    }

    private static byte[] Utf8Bytes(string json)
        => Encoding.UTF8.GetBytes(json);

    private static PolicyProviderPayload Json(string json)
        => new(Utf8Bytes(json));

    /// <summary>
    /// A test-only implementation of <see cref="ITelemetryPolicyProvider"/> that returns
    /// a configurable payload and can be set to throw on fetch.
    /// </summary>
    private sealed class FakeProvider : ITelemetryPolicyProvider
    {
        public FakeProvider(string registrationId, PolicyProviderKind kind, PolicyProviderPayload? payload = null)
        {
            this.CurrentMetadata = new PolicyProviderMetadata(new ProviderRegistrationId(registrationId), kind);
            this.NextPayload = payload;
        }

        public PolicyProviderMetadata Metadata
        {
            get
            {
                this.MetadataReads++;
                return this.MetadataException is { } exception ? throw exception : this.CurrentMetadata;
            }
        }

        public PolicyProviderMetadata CurrentMetadata { get; set; }

        public Exception? MetadataException { get; set; }

        public int MetadataReads { get; private set; }

        public int FetchCount { get; private set; }

        public Func<CancellationToken, Task<PolicyProviderPayload?>>? FetchAsyncCallback { get; set; }

        public bool WasFetched { get; private set; }

        public Exception? ThrowOnFetch { get; set; }

        public Action? OnFetch { get; set; }

        public PolicyProviderPayload? NextPayload { get; set; }

        public Task<PolicyProviderPayload?> FetchAsync(CancellationToken cancellationToken)
        {
            this.WasFetched = true;
            this.FetchCount++;
            this.OnFetch?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();

            return this.ThrowOnFetch is { } ex
                ? throw ex
                : this.FetchAsyncCallback?.Invoke(cancellationToken) ?? Task.FromResult(this.NextPayload);
        }
    }
}
