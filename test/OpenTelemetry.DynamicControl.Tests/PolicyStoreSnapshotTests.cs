// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Providers;
using OpenTelemetry.DynamicControl.Internal.Store;

namespace OpenTelemetry.DynamicControl.Tests;

public class PolicyStoreSnapshotTests
{
    [Fact]
    public void Empty_HasRevisionZeroAndNoProviders()
    {
        var empty = PolicyStoreSnapshot.Empty;

        Assert.Equal(0, empty.Revision);
        Assert.Empty(empty.Providers);
    }

    [Fact]
    public void Empty_IsSameReference() => Assert.Same(PolicyStoreSnapshot.Empty, PolicyStoreSnapshot.Empty);

    [Fact]
    public void TryGetProvider_Miss_ReturnsFalseWithNullOut()
    {
        var found = PolicyStoreSnapshot.Empty.TryGetProvider(new ProviderRegistrationId("missing"), out var provider);

        Assert.False(found, "TryGetProvider should return false for a missing provider");
        Assert.Null(provider);
    }

    [Fact]
    public void Providers_SeveralProviders_OrderedByOrdinalRegistrationId()
    {
        var store = new PolicyStore();

        // Commit in non-alphabetical order.
        CommitProvider(store, "z-provider", PolicyProviderKind.File, sequence: 1);
        CommitProvider(store, "a-provider", PolicyProviderKind.OpAmp, sequence: 1);
        CommitProvider(store, "m-provider", PolicyProviderKind.Http, sequence: 1);

        var snapshot = store.Current;

        Assert.Equal(3, snapshot.Providers.Length);
        Assert.Equal("a-provider", snapshot.Providers[0].RegistrationId.Value);
        Assert.Equal("m-provider", snapshot.Providers[1].RegistrationId.Value);
        Assert.Equal("z-provider", snapshot.Providers[2].RegistrationId.Value);
    }

    [Fact]
    public void TryGetProvider_Hit_ReturnsTrueAndSnapshot()
    {
        var store = new PolicyStore();
        CommitProvider(store, "my-provider", PolicyProviderKind.File, sequence: 1);

        var id = new ProviderRegistrationId("my-provider");
        var found = store.Current.TryGetProvider(id, out var provider);

        Assert.True(found, "TryGetProvider should find the committed provider");
        Assert.NotNull(provider);
        Assert.Equal(id, provider.RegistrationId);
    }

    [Fact]
    public void Providers_CannotBeMutatedThroughIListCast()
    {
        var store = new PolicyStore();
        CommitProvider(store, "a-provider", PolicyProviderKind.File, sequence: 1);
        CommitProvider(store, "b-provider", PolicyProviderKind.OpAmp, sequence: 1);

        var snapshot = store.Current;
        var first = snapshot.Providers[0];

        var asList = Assert.IsType<IList<PolicyProviderSnapshot>>(snapshot.Providers, exactMatch: false);

        Assert.True(asList.IsReadOnly, "Providers list must be read-only");
        Assert.Throws<NotSupportedException>(() => { asList[0] = snapshot.Providers[1]; });
        Assert.Throws<NotSupportedException>(() => asList.RemoveAt(0));
        Assert.Throws<NotSupportedException>(asList.Clear);
        Assert.Same(first, snapshot.Providers[0]);
    }

    [Fact]
    public void Empty_Providers_IsAlsoReadOnly()
    {
        var providers = PolicyStoreSnapshot.Empty.Providers;

        var asList = Assert.IsType<IList<PolicyProviderSnapshot>>(providers, exactMatch: false);
        Assert.True(asList.IsReadOnly, "empty snapshot's Providers must also be read-only");
    }

    private static void CommitProvider(PolicyStore store, string idValue, PolicyProviderKind kind, long sequence)
    {
        var metadata = new PolicyProviderMetadata(new ProviderRegistrationId(idValue), kind);
        PolicyProviderSnapshot.TryCreate(metadata, sequence, PolicyProviderVersion.Empty, [], out var snapshot, out _);
        store.ReplaceProvider(snapshot!);
    }
}
