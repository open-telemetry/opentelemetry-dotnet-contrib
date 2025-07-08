// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Data;
using OpenTelemetry.OpAmp.Client.Listeners.Messages;
using OpenTelemetry.OpAmp.Client.Services;
using OpenTelemetry.OpAmp.Client.Settings;
using OpenTelemetry.OpAmp.Client.Transport;
using OpenTelemetry.OpAmp.Client.Trash;

namespace OpenTelemetry.OpAmp.Client;

/// <summary>
/// OpAMP client implementation that connects to an OpAMP server.
/// </summary>
public class OpAmpClient : IDisposable
{
    private readonly OpAmpSettings settings = new();
    private readonly FrameProcessor processor = new();
    private readonly Dictionary<string, IBackgroundService> services = [];
    private readonly FrameDispatcher dispatcher;
    private readonly IOpAmpTransport transport;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpAmpClient"/> class.
    /// </summary>
    /// <param name="configure">Configure OpAmp settings.</param>
    public OpAmpClient(Action<OpAmpSettings>? configure = null)
    {
        configure?.Invoke(this.settings);

        this.transport = ConstructTransport(this.settings.ConnectionType, this.processor);
        this.dispatcher = new FrameDispatcher(this.transport, this.settings);

        this.ConfigureServices();

        // TODO: for testing only, remove when done
        var listener = new SampleMessageListener();
        this.processor.Subscribe<ConnectionSettingsMessage>(listener);
        this.processor.Subscribe<CustomCapabilitiesMessage>(listener);
        this.processor.Subscribe<CustomMessageMessage>(listener);
        this.processor.Subscribe<ErrorResponseMessage>(listener);
        this.processor.Subscribe<PackagesAvailableMessage>(listener);
        this.processor.Subscribe<RemoteConfigMessage>(listener);
    }

    /// <summary>
    /// Starts the asynchronous operation to initialize the transport and send identification data.
    /// </summary>
    /// <param name="token">A <see cref="CancellationToken"/> that can be used to cancel the operation. Defaults to <see
    /// langword="default"/> if not provided.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task StartAsync(CancellationToken token = default)
    {
        if (this.transport is WsTransport wsTransport)
        {
            await wsTransport.StartAsync(token)
                .ConfigureAwait(false);
        }

        await this.dispatcher.DispatchIdentificationFrameAsync(token)
            .ConfigureAwait(false);

        foreach (var service in this.services)
        {
            service.Value.Start();
        }
    }

    /// <summary>
    /// Updates the agent's health status.
    /// </summary>
    /// <param name="status">The agent's health update.</param>
    public void UpdateHealth(HealthStatus status)
    {
        if (this.services.TryGetValue(HeartbeatService.Name, out var service) && service is HeartbeatService heartbeatService)
        {
            heartbeatService.UpdateStatus(status);
        }
    }

    /// <summary>
    /// Releases resources used by this client.
    /// </summary>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// If disposing, disposes all backgroundservices and other resources.
    /// </summary>
    /// <param name="disposing">true if disposing, false if finalizing.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var service in this.services.Values)
            {
                service.Stop();

                if (service is IDisposable disposableService)
                {
                    disposableService.Dispose();
                }
            }

            this.dispatcher.Dispose();
        }
    }

    private static IOpAmpTransport ConstructTransport(ConnectionType connectionType, FrameProcessor processor)
    {
        return connectionType switch
        {
            ConnectionType.WebSocket => new WsTransport(processor),
            ConnectionType.Http => new HttpTransport(processor),
            _ => throw new NotSupportedException("Unsupported transport type"),
        };
    }

    private void ConfigureServices()
    {
        this.ConfigureService<HeartbeatService>(
            settings => settings.HeartbeatSettings.IsEnabled,
            () => new(this.dispatcher, this.processor));
    }

    private void ConfigureService<T>(Predicate<OpAmpSettings> isEnabled, Func<T> construct)
        where T : IBackgroundService
    {
        if (isEnabled(this.settings))
        {
            var service = construct();
            service.Configure(this.settings);

            this.services[service.ServiceName] = service;
        }
    }
}
