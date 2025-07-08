// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Listeners.Messages;

namespace OpenTelemetry.OpAmp.Client.Trash;

internal class SampleMessageListener : IOpAmpListener,
    IOpAmpListener<ConnectionSettingsMessage>,
    IOpAmpListener<CustomCapabilitiesMessage>,
    IOpAmpListener<CustomMessageMessage>,
    IOpAmpListener<ErrorResponseMessage>,
    IOpAmpListener<PackagesAvailableMessage>,
    IOpAmpListener<RemoteConfigMessage>
{
    public void HandleMessage(ConnectionSettingsMessage message)
    {
        Console.WriteLine("On connection settings received");
    }

    public void HandleMessage(CustomCapabilitiesMessage message)
    {
        Console.WriteLine("Custom capabilities received");
    }

    public void HandleMessage(CustomMessageMessage message)
    {
        Console.WriteLine("Custom message received");
    }

    public void HandleMessage(ErrorResponseMessage message)
    {
        Console.WriteLine("On error response received");
    }

    public void HandleMessage(PackagesAvailableMessage message)
    {
        Console.WriteLine("On packages available received");
    }

    public void HandleMessage(RemoteConfigMessage message)
    {
        Console.WriteLine("Config received");
    }
}
