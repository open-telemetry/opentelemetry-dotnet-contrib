// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Protocol;

namespace OpenTelemetry.OpAmp.Client.Listeners.Messages;

internal record FlagsMessage : IOpAmpMessage
{
    public required ServerToAgentFlags Flags { get; set; }
}
