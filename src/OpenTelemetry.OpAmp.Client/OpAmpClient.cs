// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Transport;
using OpenTelemetry.OpAmp.Client.Transport.Http;

namespace OpenTelemetry.OpAmp.Client;

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

#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private static IOpAmpTransport ConstructTransport(OpAmpClientSettings settings, FrameProcessor processor)
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
    {
        return settings.ConnectionType switch
        {
            ConnectionType.WebSocket => throw new NotImplementedException("WebSocket transport is not available."),
            ConnectionType.Http => new PlainHttpTransport(settings.ServerUrl, processor),
            _ => throw new NotSupportedException("Unsupported transport type"),
        };
    }
}
