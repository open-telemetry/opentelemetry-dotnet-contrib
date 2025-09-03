// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;

namespace OpenTelemetry.OpAmp.Client.Internal.Listeners.Messages;

internal class CustomMessageMessage : IOpAmpMessage
{
    public CustomMessageMessage(CustomMessage customMessage)
    {
        this.CustomMessage = customMessage;
    }

    public CustomMessage CustomMessage { get; set; }
}
