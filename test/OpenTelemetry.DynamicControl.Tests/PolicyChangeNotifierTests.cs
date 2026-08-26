// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Store;

namespace OpenTelemetry.DynamicControl.Tests;

public class PolicyChangeNotifierTests
{
    [Fact]
    public void Add_ReturnsSubscription_IncludedInSubscribers()
    {
        var notifier = new PolicyChangeNotifier();

        var subscription = notifier.Add(_ => { });

        Assert.Same(subscription, notifier.Subscribers[0]);
    }

    [Fact]
    public void Add_NullCallback_Throws()
    {
        var notifier = new PolicyChangeNotifier();

        Assert.Throws<ArgumentNullException>(() => notifier.Add(null!));
    }

    [Fact]
    public void Add_MultipleSubscriptions_AllIncluded()
    {
        var notifier = new PolicyChangeNotifier();

        var first = notifier.Add(_ => { });
        var second = notifier.Add(_ => { });

        Assert.Equal(2, notifier.Subscribers.Length);
        Assert.Same(first, notifier.Subscribers[0]);
        Assert.Same(second, notifier.Subscribers[1]);
    }

    [Fact]
    public void Remove_ViaDispose_NoLongerInSubscribers()
    {
        var notifier = new PolicyChangeNotifier();
        var kept = notifier.Add(_ => { });
        var removed = notifier.Add(_ => { });

        removed.Dispose();

        Assert.Equal(1, notifier.Subscribers.Length);
        Assert.Same(kept, notifier.Subscribers[0]);
    }

    [Fact]
    public void Remove_NotRegistered_NoOp()
    {
        var notifier = new PolicyChangeNotifier();
        var other = new PolicyChangeNotifier().Add(_ => { });

        // Removing a subscription that was never added to this notifier must not throw
        // or otherwise disturb this notifier's own subscriber list.
        notifier.Remove(other);

        Assert.True(notifier.Subscribers.IsEmpty);
    }

    [Fact]
    public void Dispose_DisposesAllSubscriptions_SubscribersEmpty()
    {
        var notifier = new PolicyChangeNotifier();
        var deliveries = new List<PolicyStoreSnapshot>();
        notifier.Add(s => deliveries.Add(s));
        notifier.Add(s => deliveries.Add(s));

        notifier.Dispose();

        Assert.True(notifier.Subscribers.IsEmpty);
    }

    [Fact]
    public void Add_AfterDispose_Throws()
    {
        var notifier = new PolicyChangeNotifier();
        notifier.Dispose();

        Assert.Throws<ObjectDisposedException>(() => notifier.Add(_ => { }));
        Assert.True(notifier.Subscribers.IsEmpty);
    }

    [Fact]
    public void Dispose_MultipleTimes_NoOp()
    {
        var notifier = new PolicyChangeNotifier();
        notifier.Add(_ => { });

        notifier.Dispose();
        notifier.Dispose();

        Assert.True(notifier.Subscribers.IsEmpty);
    }

    [Fact]
    public async Task Add_ConcurrentWithRemove_SubscribersNeverTorn()
    {
        const int operationCount = 500;
        var notifier = new PolicyChangeNotifier();
        var errors = new System.Collections.Concurrent.ConcurrentBag<string>();

        var addTasks = Enumerable.Range(0, operationCount).Select(_ => Task.Run(() => notifier.Add(_ => { })));

        var readTasks = Enumerable.Range(0, operationCount).Select(_ => Task.Run(() =>
        {
            var subscribers = notifier.Subscribers;
            for (var i = 0; i < subscribers.Length; i++)
            {
                if (subscribers[i] is null)
                {
                    errors.Add("Null entry observed in subscriber array");
                }
            }
        }));

        await Task.WhenAll(addTasks.Concat(readTasks));

        Assert.Empty(errors);
        Assert.Equal(operationCount, notifier.Subscribers.Length);
    }
}
