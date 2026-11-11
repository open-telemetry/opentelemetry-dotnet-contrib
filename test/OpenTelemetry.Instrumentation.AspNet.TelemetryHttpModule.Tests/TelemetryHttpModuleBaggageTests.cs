// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;
using System.Web;
using OpenTelemetry.Context;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Instrumentation.AspNet.Tests;

public class TelemetryHttpModuleBaggageTests
{
    [Theory]
    [InlineData(null, false, false)]
    [InlineData("tenant=incoming", false, false)]
    [InlineData("tenant=incoming", true, false)]
    [InlineData(null, false, true)]
    [InlineData("tenant=incoming", false, true)]
    [InlineData("tenant=incoming", true, true)]
    public async Task RestoresBaggageFromThePreviousRequestStep(string? incomingBaggage, bool clearBaggage, bool throwInStep)
    {
        await RunInFreshContext(() =>
        {
            using var source = new ActivitySource(new ActivitySourceOptions(nameof(TelemetryHttpModuleBaggageTests)));
            using var listener = EnableListener(source);
            var propagator = new CompositeTextMapPropagator([new TraceContextPropagator(), new BaggagePropagator()]);
            var headers = incomingBaggage == null ? null : new Dictionary<string, string> { ["baggage"] = incomingBaggage };
            var context = HttpContextHelper.GetFakeHttpContextBase(headers: headers);
            using var activity = ActivityHelper.StartAspNetActivity(propagator, context, (_, parent) => source.StartActivity("request", ActivityKind.Server, parent));
            Assert.NotNull(activity);

            var exception = new InvalidOperationException("Request step failed.");
            void UpdateBaggage()
            {
                if (clearBaggage)
                {
                    Baggage.Current = default;
                }
                else
                {
                    Baggage.SetBaggage("tenant", "updated");
                }

                if (throwInStep)
                {
                    throw exception;
                }
            }

            if (throwInStep)
            {
                Assert.Same(exception, Assert.Throws<InvalidOperationException>(() => ExecuteStep(context, UpdateBaggage)));
            }
            else
            {
                ExecuteStep(context, UpdateBaggage);
            }

            var expected = Baggage.Current;
            LoseContext();
            ExecuteStep(context, () =>
            {
                Assert.Same(activity, Activity.Current);
                Assert.Equal(expected, Baggage.Current);
                Baggage.SetBaggage("tenant", "next-step");
            });

            LoseContext();
            ExecuteStep(context, () => Assert.Equal("next-step", Baggage.Current.GetBaggage("tenant")));
            ActivityHelper.StopAspNetActivity(propagator, activity, context, null);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("tenant=incoming")]
    public async Task RestoringBaggageDoesNotChangeAnotherExecutionContext(string? incomingBaggage)
    {
        await RunInFreshContext(() =>
        {
            using var source = new ActivitySource(new ActivitySourceOptions(nameof(TelemetryHttpModuleBaggageTests)));
            using var listener = EnableListener(source);
            var propagator = new CompositeTextMapPropagator([new TraceContextPropagator(), new BaggagePropagator()]);
            var headers = incomingBaggage == null ? null : new Dictionary<string, string> { ["baggage"] = incomingBaggage };
            var context = HttpContextHelper.GetFakeHttpContextBase(headers: headers);
            Baggage.Current = default;
            Activity? activity = null;

            var requestContext = ExecutionContext.Capture();
            Assert.NotNull(requestContext);
            ExecutionContext.Run(
                requestContext,
                _ =>
                {
                    LoseContext();
                    activity = ActivityHelper.StartAspNetActivity(propagator, context, (_, parent) => source.StartActivity("request", ActivityKind.Server, parent));
                },
                null);
            Assert.NotNull(activity);

            try
            {
                Baggage.SetBaggage("ambient", "other-operation");
                var otherContext = ExecutionContext.Capture();
                Assert.NotNull(otherContext);
                ExecutionContext.Run(
                    otherContext,
                    _ =>
                    {
                        Activity.Current = null;
                        ExecuteStep(context, () =>
                        {
                            Assert.Same(activity, Activity.Current);
                            Assert.Null(Baggage.Current.GetBaggage("ambient"));
                            Assert.Equal(incomingBaggage == null ? null : "incoming", Baggage.Current.GetBaggage("tenant"));
                        });
                    },
                    null);

                Assert.Equal("other-operation", Baggage.Current.GetBaggage("ambient"));
                Assert.Null(Baggage.Current.GetBaggage("tenant"));
            }
            finally
            {
                activity.Dispose();
            }
        });
    }

    [Fact]
    public async Task TraceContextOnlyDoesNotRestoreBaggage()
    {
        await RunInFreshContext(() =>
        {
            using var source = new ActivitySource(new ActivitySourceOptions(nameof(TelemetryHttpModuleBaggageTests)));
            using var listener = EnableListener(source);
            var propagator = new TraceContextPropagator();
            var context = HttpContextHelper.GetFakeHttpContextBase();
            using var activity = ActivityHelper.StartAspNetActivity(propagator, context, (_, parent) => source.StartActivity("request", ActivityKind.Server, parent));
            Assert.NotNull(activity);

            ExecuteStep(context, () => Baggage.SetBaggage("tenant", "request"));
            LoseContext();
            Baggage.SetBaggage("ambient", "other-operation");
            ExecuteStep(context, () =>
            {
                Assert.Same(activity, Activity.Current);
                Assert.Equal("other-operation", Baggage.Current.GetBaggage("ambient"));
                Assert.Null(Baggage.Current.GetBaggage("tenant"));
            });
            ActivityHelper.StopAspNetActivity(propagator, activity, context, null);
        });
    }

    [Fact]
    public async Task CompletedRequestIsNotSavedOrRestored()
    {
        await RunInFreshContext(() =>
        {
            using var source = new ActivitySource(new ActivitySourceOptions(nameof(TelemetryHttpModuleBaggageTests)));
            using var listener = EnableListener(source);
            var propagator = new CompositeTextMapPropagator([new TraceContextPropagator(), new BaggagePropagator()]);
            var context = HttpContextHelper.GetFakeHttpContextBase(headers: new Dictionary<string, string> { ["baggage"] = "tenant=incoming" });
            using var activity = ActivityHelper.StartAspNetActivity(propagator, context, (_, parent) => source.StartActivity("request", ActivityKind.Server, parent));
            Assert.NotNull(activity);

            ExecuteStep(context, () => ActivityHelper.StopAspNetActivity(propagator, activity, context, null));
            Assert.Null(context.Items[ActivityHelper.ContextKey]);
            LoseContext();
            ExecuteStep(context, () =>
            {
                Assert.Null(Activity.Current);
                Assert.Equal(0, Baggage.Current.Count);
            });
            Assert.Null(context.Items[ActivityHelper.ContextKey]);
        });
    }

    private static Task RunInFreshContext(Action action)
    {
        using (ExecutionContext.SuppressFlow())
        {
            return Task.Run(action, TestContext.Current.CancellationToken);
        }
    }

    private static ActivityListener EnableListener(ActivitySource source)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = candidate => ReferenceEquals(candidate, source),
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static void LoseContext()
    {
        Activity.Current = null;
        RuntimeContext.SetValue("otel.baggage", null);
    }

    private static void ExecuteStep(HttpContextBase context, Action step)
    {
        var method = typeof(TelemetryHttpModule).GetMethod("OnExecuteRequestStep", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var callback = (Action<HttpContextBase, Action>)Delegate.CreateDelegate(typeof(Action<HttpContextBase, Action>), new TelemetryHttpModule(), method);
        callback(context, step);
    }
}
