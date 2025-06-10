// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Opamp.Protocol;

namespace OpenTelemetry.OpAMPClient.Trash;

internal class SampleMessageListener : IOpAMPMessageListener
{
    public void OnConnectionSettingsReceived(ConnectionSettingsOffers connectionSettings)
    {
        Console.WriteLine("Certificate received");
    }

    public void OnCustomCapabilitiesReceived(CustomCapabilities customCapabilities)
    {
        Console.WriteLine("Custom capabilities received");
    }

    public void OnCustomMessageReceived(CustomMessage customMessage)
    {
        Console.WriteLine("Custom message received");
    }

    public void OnErrorResponseReceived(ServerErrorResponse errorResponse)
    {
        Console.WriteLine("On error response received");
    }

    public void OnPackagesAvailableReceived(PackagesAvailable packagesAvailable)
    {
        Console.WriteLine("On packages available received");
    }

    public void OnSettingsReceived(AgentRemoteConfig remoteConfig)
    {
        Console.WriteLine("Config received");
    }
}
