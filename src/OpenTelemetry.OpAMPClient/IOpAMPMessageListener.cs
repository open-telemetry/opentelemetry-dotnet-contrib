// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Opamp.Protocol;

namespace OpenTelemetry.OpAMPClient;

internal interface IOpAMPMessageListener
{
    void OnConnectionSettingsReceived(ConnectionSettingsOffers connectionSettings);

    void OnCustomCapabilitiesReceived(CustomCapabilities customCapabilities);

    void OnCustomMessageReceived(CustomMessage customMessage);

    void OnErrorResponseReceived(ServerErrorResponse errorResponse);

    void OnPackagesAvailableReceived(PackagesAvailable packagesAvailable);

    void OnSettingsReceived(AgentRemoteConfig remoteConfig);
}
