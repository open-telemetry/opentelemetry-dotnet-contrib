// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Protocol;

namespace OpenTelemetry.OpAmp.Client.Listeners.Messages;

internal class ConnectionSettingsMessage : IOpAmpMessage
{
    public ConnectionSettingsOffers ConnectionSettings { get; set; }
}
