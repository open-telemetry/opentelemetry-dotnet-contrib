// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Protocol;

namespace OpenTelemetry.OpAmp.Client.Listeners.Messages;

internal class ErrorResponseMessage : IOpAmpMessage
{
    public ErrorResponseMessage(ServerErrorResponse serverErrorResponse)
    {
        this.ErrorResponse = serverErrorResponse;
    }

    public ServerErrorResponse ErrorResponse { get; set; }
}
