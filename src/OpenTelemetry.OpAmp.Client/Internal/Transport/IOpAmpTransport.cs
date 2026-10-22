// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;

namespace OpenTelemetry.OpAmp.Client.Internal.Transport;

internal interface IOpAmpTransport
{
    // Completes after transmission for WebSocket, or after receiving and synchronously
    // dispatching the entire response for HTTP. The pipe releases the next send afterward.
    // beforeSerialize is called with the message right before it is serialized.
    Task SendAsync<T>(T message, CancellationToken token, Action<T>? beforeSerialize = null)
        where T : IMessage<T>;
}
