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

namespace OpenTelemetry.Instrumentation.Hangfire.Implementation
{
    using System.Diagnostics;
    using global::Hangfire.Common;
    using global::Hangfire.Server;
    using OpenTelemetry.Trace;

    internal class HangfireInstrumentationJobFilterAttribute : JobFilterAttribute, IServerFilter
    {
        private const string ActivityKey = "ActivityKey";

        public void OnPerforming(PerformingContext filterContext)
        {
            // Short-circuit if nobody is listening
            if (!HangfireInstrumentation.ActivitySource.HasListeners())
            {
                return;
            }

            var activityName = $"{filterContext.BackgroundJob.Job.Type.Name}.{filterContext.BackgroundJob.Job.Method.Name}".ToLowerInvariant();
            var activity = HangfireInstrumentation.ActivitySource.StartActivity(activityName, ActivityKind.Server, parentContext: default);

            if (activity != null)
            {
                activity.DisplayName = $"JOB {filterContext.BackgroundJob.Job.Type.Name}.{filterContext.BackgroundJob.Job.Method.Name}";
                activity.SetTag(HangfireInstrumentationConstants.JobIdTag, filterContext.BackgroundJob.Id);
                activity.SetTag(HangfireInstrumentationConstants.JobCreatedAtTag, filterContext.BackgroundJob.CreatedAt.ToString("O"));
                filterContext.Items.Add(ActivityKey, activity);
            }
        }

        public void OnPerformed(PerformedContext filterContext)
        {
            // Short-circuit if nobody is listening
            if (!HangfireInstrumentation.ActivitySource.HasListeners() || !filterContext.Items.ContainsKey(ActivityKey))
            {
                return;
            }

            if (filterContext.Items[ActivityKey] is Activity activity)
            {
                if (filterContext.Exception != null)
                {
                    activity.SetStatus(Status.Error);
                }

                activity.Stop();
                activity.Dispose();
            }
        }
    }
}
