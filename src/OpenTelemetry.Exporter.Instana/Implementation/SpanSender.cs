// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;

namespace OpenTelemetry.Exporter.Instana.Implementation;

internal sealed class SpanSender : IDisposable, ISpanSender
{
    private readonly CancellationTokenSource cancellationTokenSource;
    private readonly BatchExportProcessorOptions<Activity> options;
    private readonly ConcurrentQueue<InstanaSpan> queue;
    private readonly Transport transport;

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
        if (this.queue.Count < this.options.MaxQueueSize)
        {
            this.queue.Enqueue(instanaSpan);
            return true;
        }

        return false;
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
                    try
                    {
                        await this.transport.SendAsync(this.queue, this.cancellationTokenSource.Token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        InstanaExporterEventSource.Log.FailedExport(ex);
                    }
                }

                await Task.Delay(delay, this.cancellationTokenSource.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Processing cancelled
        }
    }
}
