// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

#pragma warning disable IDE0005 // Using directive is unnecessary.
using System;
using System.Diagnostics;
#pragma warning restore IDE0005 // Using directive is unnecessary.

namespace OpenTelemetry.Tests;

internal sealed class TestActivityProcessor : BaseProcessor<Activity>
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

    public override void OnStart(Activity span)
    {
        this.StartAction?.Invoke(span);
    }

    public override void OnEnd(Activity span)
    {
        this.EndAction?.Invoke(span);
    }

    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        this.ForceFlushCalled = true;
        return true;
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        this.ShutdownCalled = true;
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        this.DisposedCalled = true;
    }
}
