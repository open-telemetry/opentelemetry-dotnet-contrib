// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Protocol;

namespace OpenTelemetry.OpAmp.Client.Listeners.Messages;

internal class RemoteConfigMessage : IOpAmpMessage
{
    public AgentRemoteConfig RemoteConfig { get; set; }
}
