// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal.Services.Heartbeat;
using OpenTelemetry.OpAmp.Client.Messages;

namespace OpenTelemetry.OpAmp.Client.Internal;

internal interface IFrameBuilder
{
    IFrameBuilder AddAgentDescription();

    IFrameBuilder AddHealth(HealthReport health);

    IFrameBuilder AddAgentDisconnect();

    IFrameBuilder AddCapabilities();

    IFrameBuilder AddEffectiveConfig(IEnumerable<EffectiveConfigFile> files);

    AgentToServer Build();
}
