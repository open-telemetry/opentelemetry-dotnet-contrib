// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Services.Internal;
using OpenTelemetry.OpAmp.Client.Settings;
using OpenTelemetry.OpAmp.Client.Transport;

namespace OpenTelemetry.OpAmp.Client;

internal class FrameDispatcher : IDisposable
{
    private readonly IOpAmpTransport transport;
    private readonly OpAmpSettings settings;
    private readonly FrameBuilder frameBuilder;
    private readonly SemaphoreSlim syncRoot = new(1, 1);

    public FrameDispatcher(IOpAmpTransport transport, OpAmpSettings settings)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.frameBuilder = new FrameBuilder(settings);
    }

    // TODO: May need to redesign to request only partials
    // so any other message waiting to be sent can be included to optimize transport usage and locking time.
    public async Task DispatchIdentificationFrameAsync(CancellationToken token)
    {
        await this.syncRoot.WaitAsync(token)
            .ConfigureAwait(false);

        try
        {
            var message = this.frameBuilder
                .StartBaseMessage()
                .AddDescription()
                .Build();

            // TODO: change to debug log message
            Console.WriteLine("Sending identification message.");

            await this.transport.SendAsync(message, token)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // TODO: log error
            Console.WriteLine("hearthbeat message failure");
            this.frameBuilder.Reset(); // Reset the builder in case of failure
        }
        finally
        {
            this.syncRoot.Release();
        }
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
            Console.WriteLine("Sending hearthbeat message");

            await this.transport.SendAsync(message, token)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // TODO: log error
            Console.WriteLine("hearthbeat message failure");
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
}
