// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using OpenTelemetry.OpAmp.Client.Internal.Listeners;
using OpenTelemetry.OpAmp.Client.Internal.Listeners.Messages;

namespace OpenTelemetry.OpAmp.Client.Tests.Mocks;

internal class MockListener : IOpAmpListener<CustomMessageMessage>
{
    public ConcurrentBag<CustomMessageMessage> Messages { get; private set; } = [];

    public void HandleMessage(CustomMessageMessage message) => this.Messages.Add(message);
}
