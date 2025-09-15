// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Services;
using OpenTelemetry.OpAmp.Client.Internal.Services.Heartbeat;
using OpenTelemetry.OpAmp.Client.Internal.Transport;
using OpenTelemetry.OpAmp.Client.Internal.Transport.Http;
using OpenTelemetry.OpAmp.Client.Internal.Transport.WebSocket;
using OpenTelemetry.OpAmp.Client.Settings;

namespace OpenTelemetry.OpAmp.Client;

/// <summary>
/// OpenTelemetry OpAMP Client.
/// </summary>
public sealed class OpAmpClient : IDisposable
{
    private readonly OpAmpClientSettings settings = new();
    private readonly FrameProcessor processor = new();
    private readonly Dictionary<string, IBackgroundService> services = [];
    private readonly FrameDispatcher dispatcher;
    private readonly IOpAmpTransport transport;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpAmpClient"/> class.
    /// </summary>
    /// <param name="configure">Configure OpAMP client settings callback.</param>
    public OpAmpClient(Action<OpAmpClientSettings>? configure = null)
    {
        configure?.Invoke(this.settings);

        this.transport = ConstructTransport(this.settings, this.processor);
        this.dispatcher = new FrameDispatcher(this.transport, this.settings);

        this.ConfigureServices();
    }

    /// <summary>
    /// Starts the <see cref="OpAmpClient"/> instance, establishing the connection to the server and starting all configured services.
    /// </summary>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    public async Task StartAsync(CancellationToken token = default)
    {
        if (this.transport is WsTransport wsTransport)
        {
            await wsTransport.StartAsync(token)
                .ConfigureAwait(false);
        }

        // TODO: Dispatch identification message here

        foreach (var service in this.services.Values)
        {
            service.Start();
        }
    }

    /// <summary>
    /// Stops the <see cref="OpAmpClient"/> instance, terminating the connection to the server and stopping all running services.
    /// </summary>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    public async Task StopAsync(CancellationToken token = default)
    {
        // TODO: Identify if there is any messages to send to the server before stopping services

        foreach (var service in this.services.Values)
        {
            service.Stop();
        }

        if (this.transport is WsTransport wsTransport)
        {
            await wsTransport.StopAsync(token)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Disposes the OpAmpClient instance and releases all associated resources.
    /// </summary>
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
