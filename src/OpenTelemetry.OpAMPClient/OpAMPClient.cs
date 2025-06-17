// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAMPClient.Data;
using OpenTelemetry.OpAMPClient.Services;
using OpenTelemetry.OpAMPClient.Settings;
using OpenTelemetry.OpAMPClient.Transport;
using OpenTelemetry.OpAMPClient.Trash;

namespace OpenTelemetry.OpAMPClient;

/// <summary>
/// OpAMP client implementation that connects to an OpAMP server.
/// </summary>
public class OpAMPClient : IDisposable
{
    private readonly OpAMPSettings settings = new();
    private readonly FrameProcessor processor = new(new SampleMessageListener());
    private readonly Dictionary<string, IBackgroundService> services = [];
    private readonly FrameDispatcher dispatcher;
    private readonly IOpAMPTransport transport;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpAMPClient"/> class.
    /// </summary>
    /// <param name="configure">Configure OpAmp settings.</param>
    public OpAMPClient(Action<OpAMPSettings>? configure = null)
    {
        configure?.Invoke(this.settings);

        this.transport = ConstructTransport(this.settings.ConnectionType, this.processor);
        this.dispatcher = new FrameDispatcher(this.transport, this.settings);

        this.ConfigureServices();
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

    private static IOpAMPTransport ConstructTransport(ConnectionType connectionType, FrameProcessor processor)
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
            () => new(this.dispatcher));
    }

    private void ConfigureService<T>(Predicate<OpAMPSettings> isEnabled, Func<T> construct)
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
