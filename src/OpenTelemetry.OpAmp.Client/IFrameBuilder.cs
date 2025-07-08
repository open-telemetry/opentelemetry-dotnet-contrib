// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Protocol;
using OpenTelemetry.OpAmp.Client.Services.Internal;

namespace OpenTelemetry.OpAmp.Client;

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
