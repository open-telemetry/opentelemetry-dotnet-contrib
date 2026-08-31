// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Sources;
using OpenTelemetry.DynamicControl.Internal.Store;

namespace OpenTelemetry.DynamicControl.Tests;

public class PolicyStoreSnapshotTests
{
    [Fact]
    public void Empty_HasRevisionZeroAndNoSources()
    {
        var empty = PolicyStoreSnapshot.Empty;

        Assert.Equal(0, empty.Revision);
        Assert.Empty(empty.Sources);
    }

    [Fact]
    public void Empty_IsSameReference() => Assert.Same(PolicyStoreSnapshot.Empty, PolicyStoreSnapshot.Empty);

    [Fact]
    public void TryGetSource_Miss_ReturnsFalseWithNullOut()
    {
        var found = PolicyStoreSnapshot.Empty.TryGetSource(new SourceRegistrationId("missing"), out var source);

        Assert.False(found, "TryGetSource should return false for a missing source");
        Assert.Null(source);
    }

    [Fact]
    public void Sources_SeveralSources_OrderedByOrdinalRegistrationId()
    {
        var store = new PolicyStore();

        // Commit in non-alphabetical order.
        CommitSource(store, "z-source", PolicySourceKind.File, sequence: 1);
        CommitSource(store, "a-source", PolicySourceKind.OpAmp, sequence: 1);
        CommitSource(store, "m-source", PolicySourceKind.Http, sequence: 1);

        var snapshot = store.Current;

        Assert.Equal(3, snapshot.Sources.Length);
        Assert.Equal("a-source", snapshot.Sources[0].RegistrationId.Value);
        Assert.Equal("m-source", snapshot.Sources[1].RegistrationId.Value);
        Assert.Equal("z-source", snapshot.Sources[2].RegistrationId.Value);
    }

    [Fact]
    public void TryGetSource_Hit_ReturnsTrueAndSnapshot()
    {
        var store = new PolicyStore();
        CommitSource(store, "my-source", PolicySourceKind.File, sequence: 1);

        var id = new SourceRegistrationId("my-source");
        var found = store.Current.TryGetSource(id, out var source);

        Assert.True(found, "TryGetSource should find the committed source");
        Assert.NotNull(source);
        Assert.Equal(id, source.RegistrationId);
    }

    [Fact]
    public void Sources_CannotBeMutatedThroughIListCast()
    {
        var store = new PolicyStore();
        CommitSource(store, "a-source", PolicySourceKind.File, sequence: 1);
        CommitSource(store, "b-source", PolicySourceKind.OpAmp, sequence: 1);

        var snapshot = store.Current;
        var first = snapshot.Sources[0];

        var asList = Assert.IsType<IList<PolicySourceSnapshot>>(snapshot.Sources, exactMatch: false);

        Assert.True(asList.IsReadOnly, "Sources list must be read-only");
        Assert.Throws<NotSupportedException>(() => { asList[0] = snapshot.Sources[1]; });
        Assert.Throws<NotSupportedException>(() => asList.RemoveAt(0));
        Assert.Throws<NotSupportedException>(asList.Clear);
        Assert.Same(first, snapshot.Sources[0]);
    }

    [Fact]
    public void Empty_Sources_IsAlsoReadOnly()
    {
        var sources = PolicyStoreSnapshot.Empty.Sources;

        var asList = Assert.IsType<IList<PolicySourceSnapshot>>(sources, exactMatch: false);
        Assert.True(asList.IsReadOnly, "empty snapshot's Sources must also be read-only");
    }

    private static void CommitSource(PolicyStore store, string idValue, PolicySourceKind kind, long sequence)
    {
        var metadata = new PolicySourceMetadata(new SourceRegistrationId(idValue), kind);
        PolicySourceSnapshot.TryCreate(metadata, sequence, PolicySourceVersion.Empty, [], out var snapshot, out _);
        store.ReplaceSource(snapshot!);
    }
}
