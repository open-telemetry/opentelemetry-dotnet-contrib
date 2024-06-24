// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Exporter.Stackdriver.Tests;

public class TestActivityProcessor : BaseProcessor<Activity>, IDisposable
{
    public Action<Activity>? StartAction;
    public Action<Activity>? EndAction;

    public TestActivityProcessor()
    {
    }

    public TestActivityProcessor(Action<Activity> onStart, Action<Activity> onEnd)
    {
        this.StartAction = onStart;
        this.EndAction = onEnd;
    }

    public bool ShutdownCalled { get; private set; }

    public bool ForceFlushCalled { get; private set; }

    public bool DisposedCalled { get; private set; }

    public override void OnStart(Activity data)
    {
        this.StartAction?.Invoke(data);
    }

    public override void OnEnd(Activity data)
    {
        this.EndAction?.Invoke(data);
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        this.ShutdownCalled = true;
        base.OnShutdown(timeoutMilliseconds);
        return true;
    }

    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        this.ForceFlushCalled = true;
        return base.OnForceFlush(timeoutMilliseconds);
    }

    protected override void Dispose(bool disposing)
    {
        this.DisposedCalled = true;
        base.Dispose(disposing);
    }
}
