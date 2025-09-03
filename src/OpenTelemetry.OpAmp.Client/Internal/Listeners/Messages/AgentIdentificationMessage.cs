// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;

namespace OpenTelemetry.OpAmp.Client.Internal.Listeners.Messages;

internal class AgentIdentificationMessage : IOpAmpMessage
{
    public AgentIdentificationMessage(AgentIdentification agentIdentification)
    {
        this.AgentIdentification = agentIdentification;
    }

    public AgentIdentification AgentIdentification { get; set; }
}
