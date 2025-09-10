// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Internal.Transport;
using OpenTelemetry.OpAmp.Client.Internal.Transport.Http;
using OpenTelemetry.OpAmp.Client.Internal.Transport.WebSocket;

namespace OpenTelemetry.OpAmp.Client.Internal;

internal sealed class OpAmpClient
{
    private readonly OpAmpClientSettings settings = new();
    private readonly FrameProcessor processor = new();
    private readonly IOpAmpTransport transport;

    public OpAmpClient(Action<OpAmpClientSettings>? configure = null)
    {
        configure?.Invoke(this.settings);

        this.transport = ConstructTransport(this.settings, this.processor);
    }

    private static IOpAmpTransport ConstructTransport(OpAmpClientSettings settings, FrameProcessor processor)
    {
        return settings.ConnectionType switch
        {
            ConnectionType.WebSocket => new WsTransport(settings.ServerUrl, processor),
            ConnectionType.Http => new PlainHttpTransport(settings.ServerUrl, processor),
            _ => throw new NotSupportedException("Unsupported transport type"),
        };
    }
}
