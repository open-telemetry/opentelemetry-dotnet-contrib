// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Hangfire;
using Hangfire.Client;
using Hangfire.Common;
using Hangfire.Server;
using OpenTelemetry.Context;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Hangfire.Implementation;

internal sealed class HangfireInstrumentationJobFilterAttribute : JobFilterAttribute, IServerFilter, IClientFilter
{
    private readonly HangfireInstrumentationOptions options;

#pragma warning disable CA1019 // Define accessors for attribute arguments
    public HangfireInstrumentationJobFilterAttribute(HangfireInstrumentationOptions options)
#pragma warning restore CA1019 // Define accessors for attribute arguments
    {
        this.options = options;
    }

    public void OnPerforming(PerformingContext filterContext)
    {
        // Short-circuit if nobody is listening
        if (!HangfireInstrumentation.ActivitySource.HasListeners())
        {
            return;
        }

        var activityContextData = ReadActivityContextData(filterContext);

        // Assigning Baggage.Current writes into the holder already present in the execution context, which
        // on a long-lived Hangfire worker thread is shared with work started by earlier jobs. Detach the
        // holder so the assignments below create one for this job only, and put it back in OnPerformed.
        // Reading Baggage.Current first ensures the baggage slot is registered.
        _ = Baggage.Current;
        filterContext.Items[HangfireInstrumentationConstants.PreviousBaggageKey] = RuntimeContext.GetValue(HangfireInstrumentationConstants.BaggageSlotName);
        RuntimeContext.SetValue(HangfireInstrumentationConstants.BaggageSlotName, null);

        var previousActivity = Activity.Current;
        filterContext.Items[HangfireInstrumentationConstants.PreviousActivityKey] = previousActivity;

        Activity? activity = null;

        try
        {
            ActivityContext parentContext = default;
            Baggage.Current = default;

            if (activityContextData is not null)
            {
                var propagationContext = Propagators.DefaultTextMapPropagator.Extract(default, activityContextData, ExtractActivityProperties);
                parentContext = propagationContext.ActivityContext;
                Baggage.Current = propagationContext.Baggage;
            }

            // Created, not started: Activity.Id caches the trace flags on first read, so a filtered activity
            // must have Recorded cleared before Start lets listeners see it.
            activity = HangfireInstrumentation.ActivitySource
                .CreateActivity(HangfireInstrumentationConstants.ActivityName, ActivityKind.Internal, parentContext);

            if (activity != null)
            {
                if (activity.IsAllDataRequested)
                {
                    if (this.IsFilteredOut(filterContext.BackgroundJob))
                    {
                        // The activity stays current until OnPerformed, so activities started by the job
                        // inherit the cleared Recorded flag and are dropped by a parent-based sampler.
                        activity.IsAllDataRequested = false;
                        activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                    }
                    else
                    {
                        activity.SetTag(WorkflowAttributes.AttributeWorkflowExecutionId, filterContext.BackgroundJob.Id);
                        activity.SetTag(WorkflowAttributes.AttributeWorkflowExecutionCreationTime, filterContext.BackgroundJob.CreatedAt.ToString("O"));
                    }
                }

                activity.Start();

                activity.DisplayName = this.GetDisplayName(filterContext.BackgroundJob);

                filterContext.Items.Add(HangfireInstrumentationConstants.ActivityKey, activity);
            }
        }
        catch (Exception ex)
        {
            HangfireInstrumentationEventSource.Log.StartJobActivityFailed(ex);

            // The activity is not in Items, so OnPerformed would not stop it. OnPerformed still restores
            // the baggage after the job.
            if (activity != null)
            {
                // Otherwise the half-started activity is exported with the placeholder name while the job runs.
                activity.IsAllDataRequested = false;
                activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                StopActivity(activity, previousActivity);
            }
        }
    }

    public void OnPerformed(PerformedContext filterContext)
    {
        var shouldRestoreBaggage = filterContext.Items.TryGetValue(HangfireInstrumentationConstants.PreviousBaggageKey, out var previousBaggage);

        try
        {
            if (filterContext.Items.TryGetValue(HangfireInstrumentationConstants.ActivityKey, out var value)
                && value is Activity activity)
            {
                try
                {
                    if (filterContext.Exception != null && activity.IsAllDataRequested)
                    {
                        this.SetStatusAndRecordException(activity, filterContext.Exception);
                    }
                }
                catch (Exception ex)
                {
                    HangfireInstrumentationEventSource.Log.RecordJobExceptionFailed(ex);
                }

                filterContext.Items.TryGetValue(HangfireInstrumentationConstants.PreviousActivityKey, out var previousActivity);
                StopActivity(activity, previousActivity as Activity);
            }
        }
        finally
        {
            if (shouldRestoreBaggage)
            {
                RuntimeContext.SetValue(HangfireInstrumentationConstants.BaggageSlotName, previousBaggage);
            }
        }
    }

    public void OnCreating(CreatingContext filterContext)
    {
        // Short-circuit if nobody is listening
        if (!HangfireInstrumentation.ActivitySource.HasListeners())
        {
            return;
        }

        var activity = Activity.Current;
        if (activity == null)
        {
            return;
        }

        var activityContextData = new Dictionary<string, string>();
        Propagators.DefaultTextMapPropagator.Inject(new PropagationContext(activity.Context, Baggage.Current), activityContextData, InjectActivityProperties);
        filterContext.SetJobParameter(HangfireInstrumentationConstants.ActivityContextKey, activityContextData);
    }

    public void OnCreated(CreatedContext filterContext)
    {
    }

    private static Dictionary<string, string>? ReadActivityContextData(PerformingContext filterContext)
    {
        try
        {
            return filterContext.GetJobParameter<Dictionary<string, string>>(HangfireInstrumentationConstants.ActivityContextKey);
        }
        catch (Exception ex)
        {
            HangfireInstrumentationEventSource.Log.ReadActivityContextFailed(ex);
            return null;
        }
    }

    private static void InjectActivityProperties(IDictionary<string, string> jobParams, string key, string value)
    {
        jobParams[key] = value;
    }

    private static IEnumerable<string> ExtractActivityProperties(Dictionary<string, string> telemetryData, string key)
    {
        return telemetryData.TryGetValue(key, out var value) ? [value] : [];
    }

    private static void StopActivity(Activity activity, Activity? previousActivity)
    {
        try
        {
            activity.Dispose();
        }
        catch (Exception ex)
        {
            HangfireInstrumentationEventSource.Log.StopJobActivityFailed(ex);
        }

        // Activity.Stop notifies listeners before it restores Activity.Current, so a throwing
        // ActivityStopped listener leaves the stopped job activity current on the worker.
        if (Activity.Current == activity)
        {
            Activity.Current = previousActivity;
        }
    }

    private string GetDisplayName(BackgroundJob backgroundJob)
    {
        try
        {
            if (this.options.DisplayNameFunc?.Invoke(backgroundJob) is { } displayName)
            {
                return displayName;
            }
        }
        catch (Exception ex)
        {
            HangfireInstrumentationEventSource.Log.DisplayNameFuncFailed(ex);
        }

        return HangfireInstrumentation.DefaultDisplayNameFunc(backgroundJob);
    }

    private bool IsFilteredOut(BackgroundJob backgroundJob)
    {
        try
        {
            return this.options.Filter?.Invoke(backgroundJob) == false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private void SetStatusAndRecordException(Activity activity, Exception exception)
    {
        activity.SetStatus(ActivityStatusCode.Error);

        if (this.options.RecordException)
        {
            activity.AddException(exception);
        }
    }
}
