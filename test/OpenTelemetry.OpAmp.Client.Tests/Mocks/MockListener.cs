// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Messages;

namespace OpenTelemetry.OpAmp.Client.Tests.Mocks;

internal class MockListener : IOpAmpListener<CustomMessageMessage>, IDisposable
{
    private readonly AutoResetEvent messageEvent = new(false);

    public ConcurrentBag<CustomMessageMessage> Messages { get; private set; } = [];

    public void HandleMessage(CustomMessageMessage message)
    {
        this.Messages.Add(message);
        this.messageEvent.Set();
    }

    public bool TryWaitForMessage(TimeSpan timeout)
    {
        return this.messageEvent.WaitOne(timeout);
    }

    public void WaitForMessages(TimeSpan timeout)
    {
        if (!this.TryWaitForMessage(timeout))
        {
            throw new TimeoutException($"No message was received within {timeout}.");
        }
    }

    public void Dispose()
    {
        this.messageEvent.Dispose();
    }
}
