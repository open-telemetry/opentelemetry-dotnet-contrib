// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAMPClient.Listeners;
using OpenTelemetry.OpAMPClient.Listeners.Messages;

namespace OpenTelemetry.OpAMPClient.Trash;

internal class SampleMessageListener : IOpAMPListener,
    IOpAMPListener<ConnectionSettingsMessage>,
    IOpAMPListener<CustomCapabilitiesMessage>,
    IOpAMPListener<CustomMessageMessage>,
    IOpAMPListener<ErrorResponseMessage>,
    IOpAMPListener<PackagesAvailableMessage>,
    IOpAMPListener<RemoteConfigMessage>
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
