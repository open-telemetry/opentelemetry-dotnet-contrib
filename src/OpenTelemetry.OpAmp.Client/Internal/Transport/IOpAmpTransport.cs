// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;

namespace OpenTelemetry.OpAmp.Client.Internal.Transport;

internal interface IOpAmpTransport
{
    /// <summary>
    /// Sends an OpAMP message.
    /// </summary>
    /// <typeparam name="T">The protobuf message type to send.</typeparam>
    /// <param name="message">The message to serialize and transmit.</param>
    /// <param name="token">A cancellation token for the send operation.</param>
    /// <param name="beforeSerialize">Called with the message right before it is serialized.</param>
    /// <returns>
    /// A task that completes after transmission for WebSocket, or after receiving and
    /// synchronously dispatching the entire response for HTTP.
    /// </returns>
    /// <remarks>
    /// The pipe waits for this operation to complete before starting the next send.
    /// </remarks>
    Task SendAsync<T>(T message, CancellationToken token, Action<T>? beforeSerialize = null)
        where T : IMessage<T>;
}
