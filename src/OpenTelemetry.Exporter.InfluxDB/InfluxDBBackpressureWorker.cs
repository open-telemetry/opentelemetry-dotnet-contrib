// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.InfluxDB;

internal sealed class InfluxDBBackpressureWorker : IDisposable
{
    private readonly object syncObject = new();
    private readonly Queue<IReadOnlyList<string>> pendingExports = [];
    private readonly SemaphoreSlim exportSignal = new(0);
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly Task processingTask;
    private readonly int maxPendingExports;
    private readonly BackpressureMode backpressureMode;
    private readonly IInfluxDBExportPayloadWriter payloadWriter;
    private readonly Action<Exception> exportExceptionHandler;
    private int activeEnqueueCount;
    private bool disposed;
    private bool writeFailedSinceLastFlush;
    private bool writeInProgress;

    public InfluxDBBackpressureWorker(
        int maxPendingExports,
        BackpressureMode backpressureMode,
        IInfluxDBExportPayloadWriter payloadWriter,
        Action<Exception> exportExceptionHandler)
    {
        this.maxPendingExports = maxPendingExports;
        this.backpressureMode = backpressureMode;
        this.payloadWriter = payloadWriter;
        this.exportExceptionHandler = exportExceptionHandler;
        this.processingTask = Task.Run(this.ProcessPendingExportsAsync);
    }

    public bool Enqueue(IReadOnlyList<string> lineProtocol, out int droppedWriteCount)
    {
        droppedWriteCount = 0;

        lock (this.syncObject)
        {
            this.activeEnqueueCount++;

            try
            {
                this.ThrowIfDisposed();

                while (this.GetPendingExportCountUnsafe() >= this.maxPendingExports)
                {
                    switch (this.backpressureMode)
                    {
                        case BackpressureMode.Wait:
                            Monitor.Wait(this.syncObject);
                            this.ThrowIfDisposed();
                            break;
                        case BackpressureMode.DropNewest:
                            droppedWriteCount = lineProtocol.Count;
                            return false;
                        case BackpressureMode.DropOldest:
                            if (this.pendingExports.Count == 0)
                            {
                                droppedWriteCount = lineProtocol.Count;
                                return false;
                            }

                            droppedWriteCount = this.pendingExports.Dequeue().Count;

                            // Best-effort reclaim the dropped payload's signal when it has not
                            // already been consumed by the background worker.
                            _ = this.exportSignal.Wait(0);
                            break;
                        default:
                            throw new InvalidOperationException($"Unsupported {nameof(BackpressureMode)} value '{this.backpressureMode}'.");
                    }
                }

                this.ThrowIfDisposed();

                this.pendingExports.Enqueue(lineProtocol);
                this.exportSignal.Release();
                return true;
            }
            finally
            {
                this.activeEnqueueCount--;
                Monitor.PulseAll(this.syncObject);
            }
        }
    }

    public bool Flush(int timeoutMilliseconds)
    {
        Stopwatch? stopwatch = null;
        if (timeoutMilliseconds != Timeout.Infinite)
        {
            Guard.ThrowIfOutOfRange(timeoutMilliseconds, min: 0);
            stopwatch = Stopwatch.StartNew();
        }

        lock (this.syncObject)
        {
            while (this.activeEnqueueCount > 0 || this.writeInProgress || this.pendingExports.Count > 0)
            {
                if (timeoutMilliseconds == Timeout.Infinite)
                {
                    Monitor.Wait(this.syncObject);
                }
                else
                {
                    var remainingMilliseconds = timeoutMilliseconds - stopwatch!.ElapsedMilliseconds;
                    if (remainingMilliseconds <= 0 ||
                        !Monitor.Wait(this.syncObject, (int)remainingMilliseconds))
                    {
                        return false;
                    }
                }
            }

            var writeFailed = this.writeFailedSinceLastFlush;
            this.writeFailedSinceLastFlush = false;
            return !writeFailed;
        }
    }

    public void Dispose()
    {
        lock (this.syncObject)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            Monitor.PulseAll(this.syncObject);
        }

        this.cancellationTokenSource.Cancel();
        this.exportSignal.Release();

        try
        {
            this.processingTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            this.exportSignal.Dispose();
            this.cancellationTokenSource.Dispose();
        }
    }

    private async Task ProcessPendingExportsAsync()
    {
        try
        {
            while (true)
            {
                await this.exportSignal.WaitAsync(this.cancellationTokenSource.Token).ConfigureAwait(false);

                IReadOnlyList<string>? lineProtocol = null;
                lock (this.syncObject)
                {
                    if (this.pendingExports.Count > 0)
                    {
                        lineProtocol = this.pendingExports.Dequeue();
                        this.writeInProgress = true;
                        Monitor.PulseAll(this.syncObject);
                    }
                }

                if (lineProtocol == null)
                {
                    continue;
                }

                var writeFailed = false;
                try
                {
                    await this.payloadWriter.WriteAsync(lineProtocol, this.cancellationTokenSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (this.cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    writeFailed = true;
                    this.exportExceptionHandler(exception);
                }
                finally
                {
                    lock (this.syncObject)
                    {
                        this.writeFailedSinceLastFlush |= writeFailed;
                        this.writeInProgress = false;
                        Monitor.PulseAll(this.syncObject);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (this.cancellationTokenSource.IsCancellationRequested)
        {
        }
    }

    private int GetPendingExportCountUnsafe() => this.pendingExports.Count + (this.writeInProgress ? 1 : 0);

    private void ThrowIfDisposed()
    {
#if NET
        ObjectDisposedException.ThrowIf(this.disposed, this);
#else
        if (this.disposed)
        {
            throw new ObjectDisposedException(nameof(InfluxDBBackpressureWorker));
        }
#endif
    }
}
