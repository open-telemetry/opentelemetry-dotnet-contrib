// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Opamp.Protocol;

namespace OpenTelemetry.OpAMPClient.Listeners.Messages;

internal record ErrorResponseMessage : IOpAMPMessage
{
    public required ServerErrorResponse ErrorResponse { get; set; }
}
