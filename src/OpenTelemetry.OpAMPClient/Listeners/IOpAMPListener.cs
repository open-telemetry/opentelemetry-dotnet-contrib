// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAMPClient.Listeners;

internal interface IOpAMPListener
{
}

internal interface IOpAMPListener<TMessage> : IOpAMPListener
    where TMessage : IOpAMPMessage
{
    void HandleMessage(TMessage message);
}
