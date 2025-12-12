// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;

namespace OpenTelemetry.OpAmp.Client.Internal.Listeners.Messages;

internal class ErrorResponseMessage : OpAmpMessage
{
    public ErrorResponseMessage(ServerErrorResponse serverErrorResponse)
    {
        this.ErrorResponse = serverErrorResponse;
    }

    public ServerErrorResponse ErrorResponse { get; set; }
}
