// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Listeners.Messages;

namespace OpenTelemetry.OpAmp.Client.Tests.Mocks;

internal class MockListener : IOpAmpListener<CustomMessageMessage>
{
    public List<CustomMessageMessage> Messages { get; private set; } = [];

    public void HandleMessage(CustomMessageMessage message) => this.Messages.Add(message);
}
