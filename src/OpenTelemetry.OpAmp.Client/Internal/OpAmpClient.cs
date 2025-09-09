// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Internal.Services;
using OpenTelemetry.OpAmp.Client.Internal.Services.Heartbeat;
using OpenTelemetry.OpAmp.Client.Internal.Settings;
using OpenTelemetry.OpAmp.Client.Internal.Transport;
using OpenTelemetry.OpAmp.Client.Internal.Transport.Http;
using OpenTelemetry.OpAmp.Client.Internal.Transport.WebSocket;

namespace OpenTelemetry.OpAmp.Client.Internal;

internal sealed class OpAmpClient : IDisposable
{
    private readonly OpAmpClientSettings settings = new();
    private readonly FrameProcessor processor = new();
    private readonly Dictionary<string, IBackgroundService> services = [];
    private readonly FrameDispatcher dispatcher;
    private readonly IOpAmpTransport transport;

    public OpAmpClient(Action<OpAmpClientSettings>? configure = null)
    {
        configure?.Invoke(this.settings);

        this.transport = ConstructTransport(this.settings, this.processor);
        this.dispatcher = new FrameDispatcher(this.transport, this.settings);

        this.ConfigureServices();
    }

    public void Dispose()
    {
        this.dispatcher.Dispose();
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

    private void ConfigureServices()
    {
        this.ConfigureService<HeartbeatService>(
            settings => settings.Heartbeat.IsEnabled,
            () => new(this.dispatcher, this.processor));
    }

    private void ConfigureService<T>(Predicate<OpAmpClientSettings> isEnabledCallback, Func<T> construct)
        where T : IBackgroundService
    {
        if (isEnabledCallback(this.settings))
        {
            var service = construct();
            service.Configure(this.settings);

            this.services[service.ServiceName] = service;
        }
    }
}
