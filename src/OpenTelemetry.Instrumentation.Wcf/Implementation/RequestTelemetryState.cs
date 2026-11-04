// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

internal sealed class RequestTelemetryState
{
    private const int SendInProgress = 0;
    private const int SendCompleted = 1;
    private const int ChannelClosedDuringSend = 2;

    private int completed;
    private int sendState;

    public IDisposable? SuppressionScope { get; set; }

    public Activity? Activity { get; set; }

    public bool IsOneWay { get; set; }

    public string? MessageId { get; set; }

    /// <summary>
    /// Gets or sets the object (the channel) that sent the request, used to release its outstanding requests when it closes.
    /// </summary>
    public object? Owner { get; set; }

    /// <summary>
    /// Called when the channel that sent the request has closed or faulted. If the send has not finished yet,
    /// completion is deferred so that the send's own failure path can report why it failed.
    /// </summary>
    /// <returns><see langword="true"/> if completion was deferred until the send finishes.</returns>
    public bool TryDeferCompletionWhileSending()
        => Interlocked.CompareExchange(ref this.sendState, ChannelClosedDuringSend, SendInProgress) == SendInProgress;

    /// <summary>
    /// Called when the send of the request has finished without failing.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the channel closed or faulted while the send was in progress,
    /// in which case the request is no longer tracked and must be completed by the caller.
    /// </returns>
    public bool MarkSendCompleted()
        => Interlocked.CompareExchange(ref this.sendState, SendCompleted, SendInProgress) == ChannelClosedDuringSend;

    public bool TryMarkCompleted() => Interlocked.Exchange(ref this.completed, 1) == 0;
}
