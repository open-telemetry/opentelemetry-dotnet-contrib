// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Opamp.Protocol;

namespace OpenTelemetry.OpAMPClient.Listeners.Messages;

internal record ConnectionSettingsMessage : IOpAMPMessage
{
    public required ConnectionSettingsOffers ConnectionSettings { get; set; }
}
