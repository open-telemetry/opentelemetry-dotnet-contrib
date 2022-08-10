// <copyright file="HangfireInstrumentationJobFilterAttribute.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.Hangfire.Implementation;

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using global::Hangfire.Client;
using global::Hangfire.Common;
using global::Hangfire.Server;
using OpenTelemetry.Context.Propagation;

internal class HangfireInstrumentationJobFilterAttribute : JobFilterAttribute, IServerFilter, IClientFilter
{
    public void OnPerforming(PerformingContext performingContext)
    {
        // Short-circuit if nobody is listening
        if (!HangfireInstrumentation.ActivitySource.HasListeners())
        {
            return;
        }

        var activityContextData = performingContext.GetJobParameter<Dictionary<string, string>>(HangfireInstrumentationConstants.ActivityContextKey);
        ActivityContext parentContext = default;
        if (activityContextData is not null)
        {
            var propagationContext = Propagators.DefaultTextMapPropagator.Extract(default, activityContextData, ExtractActivityProperties);
            parentContext = propagationContext.ActivityContext;
            Baggage.Current = propagationContext.Baggage;
        }

        var activity = HangfireInstrumentation.ActivitySource
            .StartActivity(HangfireInstrumentationConstants.ActivityName, ActivityKind.Internal, parentContext);

        if (activity != null)
        {
            activity.DisplayName = $"JOB {performingContext.BackgroundJob.Job.Type.Name}.{performingContext.BackgroundJob.Job.Method.Name}";

            if (activity.IsAllDataRequested)
            {
                activity.SetTag(HangfireInstrumentationConstants.JobIdTag, performingContext.BackgroundJob.Id);
                activity.SetTag(HangfireInstrumentationConstants.JobCreatedAtTag, performingContext.BackgroundJob.CreatedAt.ToString("O"));
            }

            performingContext.Items.Add(HangfireInstrumentationConstants.ActivityKey, activity);
        }
    }

    public void OnPerformed(PerformedContext performedContext)
    {
        // Short-circuit if nobody is listening
        if (!HangfireInstrumentation.ActivitySource.HasListeners() || !performedContext.Items.ContainsKey(HangfireInstrumentationConstants.ActivityKey))
        {
            return;
        }

        if (performedContext.Items[HangfireInstrumentationConstants.ActivityKey] is Activity activity)
        {
            if (performedContext.Exception != null)
            {
                activity.SetStatus(ActivityStatusCode.Error, performedContext.Exception.Message);
            }

            activity.Dispose();
        }
    }

    public void OnCreating(CreatingContext creatingContext)
    {
        // Short-circuit if nobody is listening
        if (!HangfireInstrumentation.ActivitySource.HasListeners())
        {
            return;
        }

        ActivityContext contextToInject = default;
        if (Activity.Current != null)
        {
            contextToInject = Activity.Current.Context;
        }

        var activityContextData = new Dictionary<string, string>();
        Propagators.DefaultTextMapPropagator.Inject(new PropagationContext(contextToInject, Baggage.Current), activityContextData, InjectActivityProperties);
        creatingContext.SetJobParameter(HangfireInstrumentationConstants.ActivityContextKey, activityContextData);
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
        return telemetryData.ContainsKey(key) ? new[] { telemetryData[key] } : Enumerable.Empty<string>();
    }
}
