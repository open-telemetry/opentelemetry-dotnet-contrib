// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Web;
using OpenTelemetry.Context;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNet;

/// <summary>
/// Activity helper class.
/// </summary>
internal static class ActivityHelper
{
    /// <summary>
    /// <see cref="Activity.OperationName"/> for OpenTelemetry.Instrumentation.AspNet created <see cref="Activity"/> objects.
    /// </summary>
    internal const string AspNetActivityName = "Microsoft.AspNet.HttpReqIn";

    /// <summary>
    /// Key to store the state in HttpContext.
    /// </summary>
    internal const string ContextKey = "__AspnetInstrumentationContext__";

    /// <summary>
    /// OpenTelemetry.Instrumentation.AspNet <see cref="ActivitySource"/> name.
    /// </summary>
    internal const string AspNetSourceName = "OpenTelemetry.Instrumentation.AspNet";

    internal static readonly object StartedButNotSampledObj = new();

    private const string BaggageSlotName = "otel.baggage";
    private static readonly Func<HttpRequest, string, IEnumerable<string>> HttpRequestHeaderValuesGetter = (request, name) => request.Headers.GetValues(name);
    private static readonly ActivitySource AspNetSource = new(
        AspNetSourceName,
        typeof(ActivityHelper).Assembly.GetPackageVersion());

    [ThreadStatic]
    private static KeyValuePair<string, object?>[]? cachedTagsStorage;

    /// <summary>
    /// Try to get the started <see cref="Activity"/> for the running <see
    /// cref="HttpContext"/>.
    /// </summary>
    /// <param name="context"><see cref="HttpContext"/>.</param>
    /// <param name="aspNetActivity">Started <see cref="Activity"/> or <see
    /// langword="null"/> if 1) start has not been called or 2) start was
    /// called but sampling decided not to create an instance.</param>
    /// <returns><see langword="true"/> if start has been called.</returns>
    public static bool HasStarted(HttpContext context, out Activity? aspNetActivity)
    {
        var itemValue = context.Items[ContextKey];
        if (itemValue is ContextHolder contextHolder)
        {
            aspNetActivity = contextHolder.Activity;
            return true;
        }

        aspNetActivity = null;
        return itemValue == StartedButNotSampledObj;
    }

    /// <summary>
    /// Creates root (first level) activity that describes incoming request.
    /// </summary>
    /// <param name="textMapPropagator"><see cref="TextMapPropagator"/>.</param>
    /// <param name="context"><see cref="HttpContext"/>.</param>
    /// <param name="onRequestStartedCallback">Callback action.</param>
    /// <returns>New root activity.</returns>
    public static Activity? StartAspNetActivity(TextMapPropagator textMapPropagator, HttpContext context, Action<Activity?, HttpContext>? onRequestStartedCallback)
    {
        var propagationContext = textMapPropagator.Extract(default, context.Request, HttpRequestHeaderValuesGetter);

        KeyValuePair<string, object?>[]? tags;
        if (context.Request?.Unvalidated?.Path is string path)
        {
            tags = cachedTagsStorage ??= new KeyValuePair<string, object?>[1];

            tags[0] = new KeyValuePair<string, object?>(SemanticConventions.AttributeUrlPath, path);
        }
        else
        {
            tags = null;
        }

        var activity = AspNetSource.StartActivity(AspNetActivityName, ActivityKind.Server, propagationContext.ActivityContext, tags);

        if (tags is not null)
        {
            tags[0] = default;
        }

        if (activity != null)
        {
            if (textMapPropagator is not TraceContextPropagator)
            {
                Baggage.Current = propagationContext.Baggage;

                context.Items[ContextKey] = new ContextHolder(activity, RuntimeContext.GetValue(BaggageSlotName));
            }
            else
            {
                context.Items[ContextKey] = new ContextHolder(activity);
            }

            try
            {
                onRequestStartedCallback?.Invoke(activity, context);
            }
            catch (Exception callbackEx)
            {
                AspNetTelemetryEventSource.Log.CallbackException(activity, "OnStarted", callbackEx);
            }

            AspNetTelemetryEventSource.Log.ActivityStarted(activity);
        }
        else
        {
            onRequestStartedCallback?.Invoke(activity, context);
            context.Items[ContextKey] = StartedButNotSampledObj;
        }

        return activity;
    }

    /// <summary>
    /// Stops the activity and notifies listeners about it.
    /// </summary>
    /// <param name="textMapPropagator"><see cref="TextMapPropagator"/>.</param>
    /// <param name="aspNetActivity"><see cref="Activity"/>.</param>
    /// <param name="context"><see cref="HttpContext"/>.</param>
    /// <param name="onRequestStoppedCallback">Callback action.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void StopAspNetActivity(TextMapPropagator textMapPropagator, Activity? aspNetActivity, HttpContext context, Action<Activity?, HttpContext>? onRequestStoppedCallback)
    {
        if (aspNetActivity == null)
        {
            Debug.Assert(context.Items[ContextKey] == StartedButNotSampledObj, "Context item is not StartedButNotSampledObj.");

            // This is the case where a start was called but no activity was
            // created due to a sampling decision.
            onRequestStoppedCallback?.Invoke(aspNetActivity, context);
            context.Items[ContextKey] = null;
            return;
        }

        Debug.Assert(context.Items[ContextKey] is ContextHolder, "Context item is not an ContextHolder instance.");

        var currentActivity = Activity.Current;
        context.Items[ContextKey] = null;

        // Make sure that the activity has a proper end time before onRequestStoppedCallback is called.
        // Note that the activity must not be stopped before the callback is called.
        if (aspNetActivity.Duration == TimeSpan.Zero)
        {
            aspNetActivity.SetEndTime(DateTime.UtcNow);
        }

        try
        {
            onRequestStoppedCallback?.Invoke(aspNetActivity, context);
        }
        catch (Exception callbackEx)
        {
            AspNetTelemetryEventSource.Log.CallbackException(aspNetActivity, "OnStopped", callbackEx);
        }

        aspNetActivity.Stop();
        AspNetTelemetryEventSource.Log.ActivityStopped(aspNetActivity);

        if (textMapPropagator is not TraceContextPropagator)
        {
            Baggage.Current = default;
        }

        if (currentActivity != aspNetActivity)
        {
            Activity.Current = currentActivity;
        }
    }

    /// <summary>
    /// Notifies listeners about an unhandled exception thrown on the <see cref="HttpContext"/>.
    /// </summary>
    /// <param name="aspNetActivity"><see cref="Activity"/>.</param>
    /// <param name="context"><see cref="HttpContext"/>.</param>
    /// <param name="exception"><see cref="Exception"/>.</param>
    /// <param name="onExceptionCallback">Callback action.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteActivityException(Activity? aspNetActivity, HttpContext context, Exception exception, Action<Activity?, HttpContext, Exception>? onExceptionCallback)
    {
        try
        {
            onExceptionCallback?.Invoke(aspNetActivity, context, exception);
        }
        catch (Exception callbackEx)
        {
            AspNetTelemetryEventSource.Log.CallbackException(aspNetActivity, "OnException", callbackEx);
        }

        AspNetTelemetryEventSource.Log.ActivityException(aspNetActivity, exception);
    }

    /// <summary>
    /// It's possible that a request is executed in both native threads and managed threads,
    /// in such case Activity.Current will be lost during native thread and managed thread switch.
    /// This method is intended to restore the current activity in order to correlate the child
    /// activities with the root activity of the request.
    /// </summary>
    /// <param name="context"><see cref="HttpContext"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RestoreContextIfNeeded(HttpContext context)
    {
        if (context.Items[ContextKey] is ContextHolder contextHolder && Activity.Current != contextHolder.Activity)
        {
            Activity.Current = contextHolder.Activity;
            if (contextHolder.Baggage != null)
            {
                RuntimeContext.SetValue(BaggageSlotName, contextHolder.Baggage);
            }

            AspNetTelemetryEventSource.Log.ActivityRestored(contextHolder.Activity);
        }
    }

    internal sealed class ContextHolder
    {
        public Activity Activity;
        public object? Baggage;

        public ContextHolder(Activity activity, object? baggage = null)
        {
            this.Activity = activity;
            this.Baggage = baggage;
        }
    }
}
