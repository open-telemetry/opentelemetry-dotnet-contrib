// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Web;

namespace OpenTelemetry.Instrumentation.AspNet.Tests;

public class TelemetryHttpModuleTest
{
    [Fact]
    public void OnExecuteRequestStep_UsesProvidedContextAndRestoresActivity()
    {
        var context = new TestHttpContextBase();
        using var activity = new Activity("test").Start();
        context.Items[ActivityHelper.ContextKey] = new ActivityHelper.ContextHolder(activity!);
        Activity.Current = null;
        var stepCalled = false;
        Activity? activityInStep = null;

        var method = typeof(TelemetryHttpModule).GetMethod("OnExecuteRequestStep", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        method!.Invoke(new TelemetryHttpModule(), [context, (Action)(() =>
        {
            stepCalled = true;
            activityInStep = Activity.Current;
        })]);

        Assert.True(stepCalled);
        Assert.Same(activity, activityInStep);
        Assert.Same(activity, Activity.Current);
        Activity.Current = null;
    }

    private sealed class TestHttpContextBase : HttpContextBase
    {
        private readonly IDictionary items = new Hashtable();

        public override IDictionary Items => this.items;

        public override HttpApplication ApplicationInstance => throw new InvalidOperationException("The callback context should be used directly.");
    }
}
