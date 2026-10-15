// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

internal sealed class RequestTelemetryState
{
    private IDisposable? suppressionScope;
    private int isCompleted;

    public IDisposable? SuppressionScope
    {
        get => this.suppressionScope;
        set => this.suppressionScope = value;
    }

    public Activity? Activity { get; set; }

    public bool IsOneWay { get; set; }

    public bool IsTracked { get; set; }

    public string? MessageId { get; set; }

    public void DisposeSuppressionScope()
    {
        Interlocked.Exchange(ref this.suppressionScope, null)?.Dispose();
    }

    public bool TryMarkCompleted()
    {
        return Interlocked.Exchange(ref this.isCompleted, 1) == 0;
    }
}
