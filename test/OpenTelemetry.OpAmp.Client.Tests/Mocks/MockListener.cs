// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using OpenTelemetry.OpAmp.Client.Internal.Listeners;
using OpenTelemetry.OpAmp.Client.Internal.Listeners.Messages;

namespace OpenTelemetry.OpAmp.Client.Tests.Mocks;

internal class MockListener : IOpAmpListener<CustomMessageMessage>, IDisposable
{
    private AutoResetEvent messageEvent = new(false);

    public ConcurrentBag<CustomMessageMessage> Messages { get; private set; } = [];

    public void HandleMessage(CustomMessageMessage message)
    {
        this.Messages.Add(message);
        this.messageEvent.Set();
    }

    public void WaitForMessages(TimeSpan timeout)
    {
        this.messageEvent.WaitOne(timeout);
    }

    public void Dispose()
    {
        this.messageEvent.Dispose();
    }
}
