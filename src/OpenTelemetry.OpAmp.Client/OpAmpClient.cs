// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Services;
using OpenTelemetry.OpAmp.Client.Internal.Services.Heartbeat;
using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Messages;
using OpenTelemetry.OpAmp.Client.Settings;

namespace OpenTelemetry.OpAmp.Client;

/// <summary>
/// OpenTelemetry OpAMP Client.
/// </summary>
public sealed class OpAmpClient : IDisposable
{
    private readonly OpAmpClientSettings settings = new();

    private readonly Dictionary<string, IBackgroundService> services = [];
    private readonly FrameProcessor processor = new();
    private readonly OpAmpPipe pipe;

    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpAmpClient"/> class.
    /// </summary>
    /// <param name="configure">Configure OpAMP client settings callback.</param>
    public OpAmpClient(Action<OpAmpClientSettings>? configure = null)
    {
        configure?.Invoke(this.settings);

        this.pipe = new OpAmpPipe(this.settings, this.processor);
        this.ConfigureServices();
    }

    /// <summary>
    /// Starts the <see cref="OpAmpClient"/> instance, establishing the connection to the server and starting all configured services.
    /// </summary>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the client has already been disposed.</exception>
    public async Task StartAsync(CancellationToken token = default)
    {
        this.ThrowIfDisposed();

        await this.pipe.StartAsync(token)
            .ConfigureAwait(false);

        foreach (var service in this.services.Values)
        {
            service.Start();
        }
    }

    /// <summary>
    /// Stops the <see cref="OpAmpClient"/> instance gracefully, terminating the connection to the server and stopping all running services.
    /// </summary>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    /// <remarks>
    /// This method is the preferred shutdown path when the caller wants the client to notify the
    /// server that it is disconnecting. In particular, for WebSocket transport this attempts a
    /// graceful close handshake after sending the agent disconnect message.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown if the client has already been disposed.</exception>
    public async Task StopAsync(CancellationToken token = default)
    {
        this.ThrowIfDisposed();

        foreach (var service in this.services.Values)
        {
            service.Stop();
        }

        await this.pipe.StopAsync(token)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Flushes queued OpAMP client messages to the server.
    /// </summary>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A task that completes when the pipe has no queued data or active flush.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the client has already been disposed.</exception>
    public async Task FlushAsync(CancellationToken token = default)
    {
        this.ThrowIfDisposed();

        await this.pipe.FlushAsync(token)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Subscribe the specified listener to receive OpAMP messages of <typeparamref name="T"/> type.
    /// </summary>
    /// <typeparam name="T">The <see cref="OpAmpMessage"/> to subscribe to.</typeparam>
    /// <param name="listener">A listener capable of handling messages of type <typeparamref name="T"/>.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the client has already been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="listener"/> is null.</exception>
    public void Subscribe<T>(IOpAmpListener<T> listener)
        where T : OpAmpMessage
    {
        this.ThrowIfDisposed();
        Guard.ThrowIfNull(listener, nameof(listener));
        this.processor.Subscribe(listener);
    }

    /// <summary>
    /// Unsubscribe the specified listener from receiving OpAMP messages of <typeparamref name="T"/> type.
    /// </summary>
    /// <typeparam name="T">The <see cref="OpAmpMessage"/> to unsubscribe from.</typeparam>
    /// <param name="listener">A listener capable of handling messages of type <typeparamref name="T"/>.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the client has already been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="listener"/> is null.</exception>
    public void Unsubscribe<T>(IOpAmpListener<T> listener)
        where T : OpAmpMessage
    {
        this.ThrowIfDisposed();
        Guard.ThrowIfNull(listener, nameof(listener));
        this.processor.Unsubscribe(listener);
    }

    /// <summary>
    /// Queues a report containing the current effective configuration of the agent.
    /// </summary>
    /// <param name="files">Configuration files to report.</param>
    /// <remarks>
    /// <para>
    /// File contents are transmitted verbatim to the OpAMP server with no redaction.
    /// Ensure the transport is secured with TLS and the OpAMP server is fully trusted
    /// before reporting files that may contain sensitive data such as passwords or tokens.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if effective configuration reporting is not enabled in settings.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the client has already been disposed.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="files"/> contains two or more files with the same file name.</exception>
    public void SendEffectiveConfig(IEnumerable<EffectiveConfigFile> files)
    {
        this.ThrowIfDisposed();

        if (!this.settings.EffectiveConfigurationReporting.EnableReporting)
        {
            throw new InvalidOperationException("Effective configuration reporting is not enabled in settings.");
        }

        this.pipe.AppendMessage(MessageBuilderHelper.AppendEffectiveConfig(files));
    }

    /// <summary>
    /// Queues the status of a remote configuration previously received from the OpAMP server.
    /// </summary>
    /// <param name="statusReport">The remote configuration status report.</param>
    /// <exception cref="InvalidOperationException">Thrown if remote configuration status reporting is not enabled in settings.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the client has already been disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="statusReport"/> is null.</exception>
    public void SendRemoteConfigStatus(RemoteConfigStatusReport statusReport)
    {
        this.ThrowIfDisposed();
        Guard.ThrowIfNull(statusReport);

        if (!this.settings.RemoteConfiguration.ReportsRemoteConfigStatus)
        {
            throw new InvalidOperationException("Remote configuration status reporting is not enabled in settings.");
        }

        this.pipe.AppendMessage(MessageBuilderHelper.AppendRemoteConfigStatus(statusReport));
    }

    /// <summary>
    /// Queues custom capabilities supported by the agent.
    /// </summary>
    /// <param name="capabilities">Capabilities list.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the client has already been disposed.</exception>
    public void SendCustomCapabilities(IEnumerable<string> capabilities)
    {
        this.ThrowIfDisposed();

        this.pipe.AppendMessage(MessageBuilderHelper.AppendCustomCapabilities(capabilities));
    }

    /// <summary>
    /// Queues a custom message related to a supported custom capability.
    /// </summary>
    /// <param name="capability">Capability that matches a reported custom capability.</param>
    /// <param name="type">Type of message within the capability.</param>
    /// <param name="data">Contents of the message.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the client has already been disposed.</exception>
    public void SendCustomMessage(string capability, string type, ReadOnlyMemory<byte> data)
    {
        this.ThrowIfDisposed();

        this.pipe.AppendMessage(MessageBuilderHelper.AppendCustomMessage(capability, type, data));
    }

    /// <summary>
    /// Queues a full state report message to restore the lost state in the server.
    /// </summary>
    /// <param name="report">Report that contains supported partials.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the client has already been disposed.</exception>
    public void SendFullStateReport(FullStateReport report)
    {
        this.ThrowIfDisposed();
        Guard.ThrowIfNull(report);

        if (report.EffectiveConfigFiles != null && !this.settings.EffectiveConfigurationReporting.EnableReporting)
        {
            throw new InvalidOperationException("Effective configuration reporting is not enabled in settings.");
        }

        if (report.RemoteConfigStatus != null && !this.settings.RemoteConfiguration.ReportsRemoteConfigStatus)
        {
            throw new InvalidOperationException("Remote configuration status reporting is not enabled in settings.");
        }

        if (this.settings.Heartbeat.IsEnabled)
        {
            var service = this.GetService<HeartbeatService>(HeartbeatService.Name);
            report.HealthReport = service.CreateHealthReport();
        }

        this.pipe.AppendMessage(MessageBuilderHelper.AppendFullStateReport(report));
    }

    /// <summary>
    /// Disposes the <see cref="OpAmpClient"/> instance and releases all associated resources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Disposal performs synchronous, best-effort cleanup of background services and transport
    /// resources.
    /// </para>
    /// <para>
    /// This is not a graceful shutdown path and does not send the agent disconnect message. Call
    /// <see cref="StopAsync(CancellationToken)"/> before disposal when the client
    /// should unregister cleanly from the server.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        foreach (var service in this.services.Values)
        {
            service.Stop();
        }

        foreach (var service in this.services.Values)
        {
            if (service is IDisposable disposableService)
            {
                disposableService.Dispose();
            }
        }

        this.pipe.Dispose();
    }

    // Used for testing purposes only.
    internal void SendHeartbeat(HealthReport healthReport)
    {
        this.ThrowIfDisposed();

        this.pipe.AppendMessage(MessageBuilderHelper.AppendHeartbeat(healthReport));
    }

    private void ThrowIfDisposed()
    {
#if NET8_0_OR_GREATER
        ObjectDisposedException.ThrowIf(this.disposed, this);
#else
        if (this.disposed)
        {
            throw new ObjectDisposedException(nameof(OpAmpClient));
        }
#endif
    }

    private void ConfigureServices()
    {
        this.ConfigureService<HeartbeatService>(
            settings => settings.Heartbeat.IsEnabled,
            () => new(this.pipe, this.processor));
    }

    private TService GetService<TService>(string serviceName) => (TService)this.services[serviceName];

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
