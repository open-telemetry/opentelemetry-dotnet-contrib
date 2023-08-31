// <copyright file="QuartzDiagnosticListener.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Quartz.Implementation;

internal sealed class QuartzDiagnosticListener : ListenerHandler
{
    internal static readonly AssemblyName AssemblyName = typeof(QuartzDiagnosticListener).Assembly.GetName();
    internal static readonly string ActivitySourceName = AssemblyName.Name;
    internal static readonly Version Version = AssemblyName.Version;
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version.ToString());
    internal readonly PropertyFetcher<object> JobDetailsPropertyFetcher = new("JobDetail");

    private readonly QuartzInstrumentationOptions options;

    public QuartzDiagnosticListener(string sourceName, QuartzInstrumentationOptions options)
        : base(sourceName)
    {
        Guard.ThrowIfNull(options);

        this.options = options;
    }

    public override void OnStartActivity(Activity activity, object payload)
    {
        if (activity.IsAllDataRequested)
        {
            if (!this.options.TracedOperations.Contains(activity.OperationName))
            {
                QuartzInstrumentationEventSource.Log.OperationIsFilteredOut(activity.OperationName);
                activity.IsAllDataRequested = false;
                return;
            }

            activity.DisplayName = GetDisplayName(activity);

            ActivityInstrumentationHelper.SetActivitySourceProperty(activity, ActivitySource);
            ActivityInstrumentationHelper.SetKindProperty(activity, GetActivityKind(activity));

            try
            {
                this.JobDetailsPropertyFetcher.TryFetch(payload, out var jobDetails);
                if (jobDetails != null)
                {
                    this.options.Enrich?.Invoke(activity, "OnStartActivity", jobDetails);
                }
            }
            catch (Exception ex)
            {
                QuartzInstrumentationEventSource.Log.EnrichmentException(ex);
            }
        }
    }

    public override void OnStopActivity(Activity activity, object payload)
    {
        if (activity.IsAllDataRequested)
        {
            try
            {
                this.JobDetailsPropertyFetcher.TryFetch(payload, out var jobDetails);
                if (jobDetails != null)
                {
                    this.options.Enrich?.Invoke(activity, "OnStopActivity", jobDetails);
                }
            }
            catch (Exception ex)
            {
                QuartzInstrumentationEventSource.Log.EnrichmentException(ex);
            }
        }
    }

    public override void OnException(Activity activity, object payload)
    {
        if (activity.IsAllDataRequested)
        {
            var exc = payload as Exception;
            if (exc == null)
            {
                QuartzInstrumentationEventSource.Log.NullPayload(nameof(QuartzDiagnosticListener), nameof(this.OnStopActivity));
                return;
            }

            if (this.options.RecordException)
            {
                activity.RecordException(exc);
            }

            activity.SetStatus(Status.Error.WithDescription(exc.Message));

            try
            {
                this.options.Enrich?.Invoke(activity, "OnException", exc);
            }
            catch (Exception ex)
            {
                QuartzInstrumentationEventSource.Log.EnrichmentException(ex);
            }
        }
    }

    private static string GetDisplayName(Activity activity)
    {
        return activity.OperationName switch
        {
            OperationName.Job.Execute => $"execute {GetTag(activity.Tags, TagName.JobName)}",
            OperationName.Job.Veto => $"veto {GetTag(activity.Tags, TagName.JobName)}",
            _ => activity.DisplayName,
        };
    }

    private static ActivityKind GetActivityKind(Activity activity)
    {
        return activity.OperationName switch
        {
            OperationName.Job.Execute => ActivityKind.Internal,
            OperationName.Job.Veto => ActivityKind.Internal,
            _ => activity.Kind,
        };
    }

    private static string? GetTag(IEnumerable<KeyValuePair<string, string?>> tags, string tagName)
    {
        var tag = tags.SingleOrDefault(kv => kv.Key == tagName);
        return tag.Value;
    }
}
