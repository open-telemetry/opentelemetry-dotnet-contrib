// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Renci.SshNet;
using Renci.SshNet.Common;

namespace OpenTelemetry.Instrumentation.SshNet.Implementation;

/// <summary>
/// Decorates an <see cref="IBaseClient"/> and provides helpers for
/// wrapping operations in an <see cref="SshNetOperationScope"/>.
/// </summary>
/// <typeparam name="TClient">The decorated client type.</typeparam>
/// <param name="inner">The client to decorate.</param>
/// <param name="options">The instrumentation options.</param>
/// <param name="protocol">The protocol name recorded on spans and metrics.</param>
internal abstract class InstrumentedBaseClient<TClient>(TClient inner, SshNetInstrumentationOptions options, string protocol) : IBaseClient
    where TClient : class, IBaseClient
{
    public event EventHandler<ExceptionEventArgs>? ErrorOccurred
    {
        add => this.Inner.ErrorOccurred += value;
        remove => this.Inner.ErrorOccurred -= value;
    }

    public event EventHandler<HostKeyEventArgs>? HostKeyReceived
    {
        add => this.Inner.HostKeyReceived += value;
        remove => this.Inner.HostKeyReceived -= value;
    }

    public event EventHandler<SshIdentificationEventArgs>? ServerIdentificationReceived
    {
        add => this.Inner.ServerIdentificationReceived += value;
        remove => this.Inner.ServerIdentificationReceived -= value;
    }

    public ConnectionInfo ConnectionInfo => this.Inner.ConnectionInfo;

    public bool IsConnected => this.Inner.IsConnected;

    public TimeSpan KeepAliveInterval
    {
        get => this.Inner.KeepAliveInterval;
        set => this.Inner.KeepAliveInterval = value;
    }

    internal TClient Inner { get; } = inner ?? throw new ArgumentNullException(nameof(inner));

    protected SshNetInstrumentationOptions Options { get; } = options;

    public void Connect() => this.Run("Connect", null, this.Inner.Connect);

    public Task ConnectAsync(CancellationToken cancellationToken) =>
        this.RunAsync("Connect", null, () => this.Inner.ConnectAsync(cancellationToken));

    public void Disconnect() => this.Inner.Disconnect();

    public void SendKeepAlive() => this.Inner.SendKeepAlive();

    public void Dispose() => this.Inner.Dispose();

    protected SshNetOperationScope StartScope(string operation, string? path = null, string? targetPath = null)
        => SshNetOperationScope.Start(this.Options, protocol, operation, this.SafeConnectionInfo(), path, targetPath);

    protected void Run(string operation, string? path, Action action, string? targetPath = null)
    {
        using var scope = this.StartScope(operation, path, targetPath);
        try
        {
            action();
        }
        catch (Exception ex)
        {
            scope.SetException(ex);
            throw;
        }
    }

    protected T Run<T>(string operation, string? path, Func<T> action, string? targetPath = null)
    {
        using var scope = this.StartScope(operation, path, targetPath);
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            scope.SetException(ex);
            throw;
        }
    }

    protected async Task RunAsync(string operation, string? path, Func<Task> action, string? targetPath = null)
    {
        using var scope = this.StartScope(operation, path, targetPath);
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            scope.SetException(ex);
            throw;
        }
    }

    protected async Task<T> RunAsync<T>(string operation, string? path, Func<Task<T>> action, string? targetPath = null)
    {
        using var scope = this.StartScope(operation, path, targetPath);
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            scope.SetException(ex);
            throw;
        }
    }

    private ConnectionInfo? SafeConnectionInfo()
    {
        try
        {
            return this.Inner.ConnectionInfo;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }
}
