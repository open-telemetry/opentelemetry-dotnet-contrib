// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;

namespace OpenTelemetry.OpAmp.Client.Internal.Transport;

internal interface IOpAmpTransport
{
    bool RequiresResponseBeforeNextSend { get; }

    // beforeSerialize is called with the message right before it is serialized.
    Task SendAsync<T>(T message, CancellationToken token, Action<T>? beforeSerialize = null)
        where T : IMessage<T>;
}
