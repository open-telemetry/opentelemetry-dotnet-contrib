// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Listeners.Messages;

namespace OpenTelemetry.OpAmp.Client.Tests.Mocks;

internal class MockListener : IOpAmpListener<CustomMessageMessage>
{
    public ConcurrentBag<CustomMessageMessage> Messages { get; private set; } = [];

    public void HandleMessage(CustomMessageMessage message) => this.Messages.Add(message);
}
