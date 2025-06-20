// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAMPClient.Listeners;
using OpenTelemetry.OpAMPClient.Listeners.Messages;

namespace OpenTelemetry.OpAMPClient.Tests.Mocks;

internal class MockListener : IOpAMPListener<CustomMessageMessage>
{
    public List<CustomMessageMessage> Messages { get; private set; } = [];

    public void HandleMessage(CustomMessageMessage message) => this.Messages.Add(message);
}
