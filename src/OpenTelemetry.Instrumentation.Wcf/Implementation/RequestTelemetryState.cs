// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

internal sealed class RequestTelemetryState
{
    private int completed;

    public IDisposable? SuppressionScope { get; set; }

    public Activity? Activity { get; set; }

    public bool IsOneWay { get; set; }

    public string? MessageId { get; set; }

    public bool TryMarkCompleted() => Interlocked.Exchange(ref this.completed, 1) == 0;
}
