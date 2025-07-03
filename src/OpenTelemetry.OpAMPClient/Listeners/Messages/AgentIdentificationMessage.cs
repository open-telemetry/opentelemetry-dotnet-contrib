// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Opamp.Protocol;

namespace OpenTelemetry.OpAMPClient.Listeners.Messages;

internal record AgentIdentificationMessage : IOpAMPMessage
{
    public required AgentIdentification AgentIdentification { get; set; }
}
