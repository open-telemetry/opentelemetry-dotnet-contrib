// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAMPClient.Services.Internal;
using OpenTelemetry.OpAMPClient.Settings;
using OpenTelemetry.OpAMPClient.Transport;

namespace OpenTelemetry.OpAMPClient;

internal class FrameDispatcher : IDisposable
{
    private readonly IOpAMPTransport transport;
    private readonly OpAMPSettings settings;
    private readonly FrameBuilder frameBuilder;
    private readonly SemaphoreSlim syncRoot = new(1, 1);

    public FrameDispatcher(IOpAMPTransport transport, OpAMPSettings settings)
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
