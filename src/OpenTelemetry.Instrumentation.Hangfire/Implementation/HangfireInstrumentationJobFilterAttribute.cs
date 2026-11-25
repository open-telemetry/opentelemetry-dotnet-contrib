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

        // Read first: if this throws, OnPerformed is not called, so the baggage must not have been changed yet.
        var activityContextData = filterContext.GetJobParameter<Dictionary<string, string>>(HangfireInstrumentationConstants.ActivityContextKey);

        // Assigning Baggage.Current writes into the holder already present in the execution context, which
        // on a long-lived Hangfire worker thread is shared with work started by earlier jobs. Detach the
        // holder so the assignments below create one for this job only, and put it back in OnPerformed.
        // Reading Baggage.Current first ensures the baggage slot is registered.
        _ = Baggage.Current;
        filterContext.Items[HangfireInstrumentationConstants.PreviousBaggageKey] = RuntimeContext.GetValue(HangfireInstrumentationConstants.BaggageSlotName);
        RuntimeContext.SetValue(HangfireInstrumentationConstants.BaggageSlotName, null);

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
                        activity.SetTag(HangfireInstrumentationConstants.JobIdTag, filterContext.BackgroundJob.Id);
                        activity.SetTag(HangfireInstrumentationConstants.JobCreatedAtTag, filterContext.BackgroundJob.CreatedAt.ToString("O"));
                    }
                }

                activity.Start();

                activity.DisplayName = this.GetDisplayName(filterContext.BackgroundJob);

                filterContext.Items.Add(HangfireInstrumentationConstants.ActivityKey, activity);
            }
        }
        catch (Exception ex)
        {
            // The activity is not in Items, so OnPerformed would not stop it. OnPerformed still restores
            // the baggage after the job.
            activity?.Dispose();
            HangfireInstrumentationEventSource.Log.StartJobActivityFailed(ex);
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
                finally
                {
                    activity.Dispose();
                }
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

    private static void InjectActivityProperties(IDictionary<string, string> jobParams, string key, string value)
    {
        jobParams[key] = value;
    }

    private static IEnumerable<string> ExtractActivityProperties(Dictionary<string, string> telemetryData, string key)
    {
        return telemetryData.TryGetValue(key, out var value) ? [value] : [];
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
        catch (Exception)
        {
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
