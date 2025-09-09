// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Services.Heartbeat;
using OpenTelemetry.OpAmp.Client.Internal.Settings;
using OpenTelemetry.OpAmp.Client.Internal.Transport;

namespace OpenTelemetry.OpAmp.Client.Internal;

internal sealed class FrameDispatcher : IDisposable
{
    private readonly IOpAmpTransport transport;
    private readonly FrameBuilder frameBuilder;
    private readonly SemaphoreSlim syncRoot = new(1, 1);

    public FrameDispatcher(IOpAmpTransport transport, OpAmpClientSettings settings)
    {
        Guard.ThrowIfNull(transport, nameof(transport));
        Guard.ThrowIfNull(settings, nameof(settings));

        this.transport = transport;
        this.frameBuilder = new FrameBuilder(settings);
    }

    // TODO: May need to redesign to request only partials
    // so any other message waiting to be sent can be included to optimize transport usage and locking time.
    public async Task DispatchServerFrameAsync(CancellationToken token)
    {
        await this.syncRoot.WaitAsync(token)
            .ConfigureAwait(false);

        try
        {
            var message = this.frameBuilder
                .StartBaseMessage()
                .Build();

            // TODO: change to proper logging
            Console.WriteLine("[debug] Sending identification message.");

            await this.transport.SendAsync(message, token)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // TODO: change to proper logging
            Console.WriteLine($"[error]: {ex.Message}");

            this.frameBuilder.Reset(); // Reset the builder in case of failure
        }
        finally
        {
            this.syncRoot.Release();
        }
    }

    public void Dispose()
    {
        this.syncRoot.Dispose();
    }

    public async Task DispatchHeartbeatAsync(HealthReport report, CancellationToken token)
    {
        await this.syncRoot.WaitAsync(token)
            .ConfigureAwait(false);

        try
        {
            var message = this.frameBuilder
                .StartBaseMessage()
                .AddHeartbeat(report)
                .Build();

            // TODO: change to debug log message
            Console.WriteLine("[debug] Sending hearthbeat message");

            await this.transport.SendAsync(message, token)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // TODO: log error
            Console.WriteLine("[error] hearthbeat message failure");

            this.frameBuilder.Reset(); // Reset the builder in case of failure
        }
        finally
        {
            this.syncRoot.Release();
        }
    }
}
