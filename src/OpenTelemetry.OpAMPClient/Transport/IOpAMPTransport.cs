// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;

namespace OpenTelemetry.OpAMPClient.Transport;

internal interface IOpAMPTransport
{
    Task SendAsync<T>(T message, CancellationToken token)
        where T : IMessage<T>;
}
