// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;

namespace OpenTelemetry.Exporter.Instana.Implementation;

internal sealed class SpanSender : IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource;
    private readonly BatchExportProcessorOptions<Activity> options;
    private readonly ConcurrentQueue<InstanaSpan> queue;
    private readonly Transport transport;

    private int queueSize;

    public SpanSender(InstanaExporterOptions options)
    {
        this.options = options.BatchExportProcessorOptions;
        this.queue = new();
        this.cancellationTokenSource = new();
        this.transport = new Transport(options);

        Task.Factory.StartNew(this.ProcessingLoop, this.cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public void Dispose()
    {
        if (this.cancellationTokenSource != null)
        {
            this.cancellationTokenSource.Cancel();
            this.cancellationTokenSource.Dispose();
        }

        this.transport?.Dispose();

        GC.SuppressFinalize(this);
    }

    public bool Enqueue(InstanaSpan instanaSpan)
    {
        if (!this.TryReserveQueueSlot(this.cancellationTokenSource.Token))
        {
            return false;
        }

        try
        {
            this.queue.Enqueue(instanaSpan);
            return true;
        }
        catch
        {
            this.ReleaseQueueSlot();
            throw;
        }
    }

    private async Task ProcessingLoop()
    {
        var delay = this.options.ScheduledDelayMilliseconds;

        try
        {
            while (!this.cancellationTokenSource.IsCancellationRequested)
            {
                if (!this.queue.IsEmpty)
                {
                    int consumed = await this.transport.SendAsync(this.queue, this.cancellationTokenSource.Token).ConfigureAwait(false);
                    Interlocked.Add(ref this.queueSize, -consumed);
                }

                await Task.Delay(delay, this.cancellationTokenSource.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Processing cancelled
        }
    }

    private void ReleaseQueueSlot() => Interlocked.Decrement(ref this.queueSize);

    private bool TryReserveQueueSlot(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var queueSize = Volatile.Read(ref this.queueSize);

            if (queueSize >= this.options.MaxQueueSize)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref this.queueSize, queueSize + 1, queueSize) == queueSize)
            {
                return true;
            }
        }

        return false;
    }
}
