// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Listeners;

internal interface IOpAmpListener
{
}

internal interface IOpAmpListener<TMessage> : IOpAmpListener
    where TMessage : IOpAmpMessage
{
    void HandleMessage(TMessage message);
}
