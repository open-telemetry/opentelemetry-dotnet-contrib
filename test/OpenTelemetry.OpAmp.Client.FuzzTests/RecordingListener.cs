// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Messages;

namespace OpenTelemetry.OpAmp.Client.FuzzTests;

internal sealed class RecordingListener<T> : IOpAmpListener<T>
    where T : OpAmpMessage
{
    public List<T> Messages { get; } = [];

    public void HandleMessage(T message) => this.Messages.Add(message);
}
