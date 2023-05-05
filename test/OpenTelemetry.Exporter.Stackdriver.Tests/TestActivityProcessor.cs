// <copyright file="TestActivityProcessor.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
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
