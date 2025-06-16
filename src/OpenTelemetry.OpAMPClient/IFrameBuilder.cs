// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Opamp.Protocol;
using OpenTelemetry.OpAMPClient.Services.Internal;

namespace OpenTelemetry.OpAMPClient;

internal interface IFrameBuilder
{
    IFrameBuilder AddDescription();

    IFrameBuilder AddHeartbeat(HealthReport health);

    IFrameBuilder AddCapabilities();

    IFrameBuilder AddCurrentConfig();

    IFrameBuilder AddConfigStatus();

    IFrameBuilder AddPackageStatus();

    IFrameBuilder AddDisconnectRequest();

    IFrameBuilder SetFlags();

    AgentToServer Build();
}
