// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Protocol;

namespace OpenTelemetry.OpAmp.Client.Listeners.Messages;

internal record ErrorResponseMessage : IOpAmpMessage
{
    public required ServerErrorResponse ErrorResponse { get; set; }
}
