// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

internal class ListenerHandle : IDisposable
{
    private readonly ITraceListener listener;
    private bool isDisposed;

    public ListenerHandle(ITraceListener listener)
    {
        this.listener = listener;
        TraceSourceManager.AddTraceListener(listener, startupDone: true);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!this.isDisposed)
        {
            if (disposing)
            {
                TraceSourceManager.RemoveTraceListener(this.listener);
            }

            this.isDisposed = true;
        }
    }
}
