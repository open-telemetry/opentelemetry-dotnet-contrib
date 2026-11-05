// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Web;
using OpenTelemetry.Context.Propagation;

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

        method!.Invoke(
            new TelemetryHttpModule(),
            [
                context,
                () =>
                {
                    stepCalled = true;
                    activityInStep = Activity.Current;
                },
            ]);

        Assert.True(stepCalled);
        Assert.Same(activity, activityInStep);
        Assert.Same(activity, Activity.Current);
        Activity.Current = null;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Error_After_EndRequest_Does_Not_Restart_Telemetry(bool errorInHandler)
    {
        var previousBaggage = Baggage.Current;
        var previousPropagator = TelemetryHttpModule.Options.TextMapPropagator;
        var previousStartedCallback = TelemetryHttpModule.Options.OnRequestStartedCallback;
        var previousExceptionCallback = TelemetryHttpModule.Options.OnExceptionCallback;

        using var source = new ActivitySource("TelemetryHttpModuleTest");
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        try
        {
            Baggage.Current = default;
            Activity.Current = null;

            Activity? requestActivity = null;
            var started = 0;
            var exceptions = 0;
            var exceptionActivities = new List<Activity?>();
            TelemetryHttpModule.Options.TextMapPropagator = new CompositeTextMapPropagator([new TraceContextPropagator(), new BaggagePropagator()]);
            TelemetryHttpModule.Options.OnRequestStartedCallback = (_, _) =>
            {
                started++;
                requestActivity = source.StartActivity("Request");
                return requestActivity;
            };
            TelemetryHttpModule.Options.OnExceptionCallback = (activity, _, _) =>
            {
                exceptions++;
                exceptionActivities.Add(activity);
            };

            var contextBase = HttpContextHelper.GetFakeHttpContextBase(headers: new Dictionary<string, string> { ["baggage"] = "tenant=value" });
            var context = HttpContext.Current;
            var application = new HttpApplication();
            typeof(HttpApplication).GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(application, context);

            var module = new TelemetryHttpModule();
            Invoke(module, "Application_BeginRequest", application);

            Assert.Equal(1, started);
            Assert.NotNull(requestActivity);
            Assert.Same(requestActivity, Activity.Current);
            Assert.Equal(1, Baggage.Current.Count);

            if (errorInHandler)
            {
                // The error is raised before the module's EndRequest handler runs
                context.AddError(new InvalidOperationException("Handler"));
                Invoke(module, "Application_Error", application);

                Assert.Same(requestActivity, Assert.Single(exceptionActivities));
                Assert.False(requestActivity!.IsStopped);
                Assert.Equal(1, Baggage.Current.Count);
            }

            Invoke(module, "Application_EndRequest", application);

            Assert.True(requestActivity!.IsStopped);
            Assert.Null(Activity.Current);
            Assert.Equal(0, Baggage.Current.Count);
            Assert.Null(contextBase.Items[ActivityHelper.ContextKey]);

            if (!errorInHandler)
            {
                // The error is raised by an EndRequest handler that runs after the module's one
                context.AddError(new InvalidOperationException("Late EndRequest handler"));
                Invoke(module, "Application_Error", application);

                // The exception is still reported, but without an activity
                Assert.Null(Assert.Single(exceptionActivities));
            }

            Assert.Equal(1, started);
            Assert.Equal(1, exceptions);
            Assert.Null(Activity.Current);
            Assert.Equal(0, Baggage.Current.Count);
            Assert.Null(contextBase.Items[ActivityHelper.ContextKey]);
        }
        finally
        {
            TelemetryHttpModule.Options.TextMapPropagator = previousPropagator;
            TelemetryHttpModule.Options.OnRequestStartedCallback = previousStartedCallback;
            TelemetryHttpModule.Options.OnExceptionCallback = previousExceptionCallback;
            Baggage.Current = previousBaggage;
            Activity.Current = null;
        }
    }

    private static void Invoke(TelemetryHttpModule module, string methodName, HttpApplication application)
    {
        var method = typeof(TelemetryHttpModule).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(module, [application, EventArgs.Empty]);
    }

    private sealed class TestHttpContextBase : HttpContextBase
    {
        private readonly IDictionary items = new Hashtable();

        public override IDictionary Items => this.items;

        public override HttpApplication ApplicationInstance => throw new InvalidOperationException("The callback context should be used directly.");
    }
}
