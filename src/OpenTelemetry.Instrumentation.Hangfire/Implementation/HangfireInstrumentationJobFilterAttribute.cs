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

    internal class HangfireInstrumentationJobFilterAttribute : JobFilterAttribute, IServerFilter
    {
        public void OnPerforming(PerformingContext performingContext)
        {
            // Short-circuit if nobody is listening
            if (!HangfireInstrumentation.ActivitySource.HasListeners())
            {
                return;
            }

            var activity = HangfireInstrumentation.ActivitySource
                .StartActivity(HangfireInstrumentationConstants.ActivityName, ActivityKind.Internal, parentContext: default);
            if (activity != null)
            {
                activity.DisplayName = $"JOB {performingContext.BackgroundJob.Job.Type.Name}.{performingContext.BackgroundJob.Job.Method.Name}";
                activity.SetTag(HangfireInstrumentationConstants.JobIdTag, performingContext.BackgroundJob.Id);
                activity.SetTag(HangfireInstrumentationConstants.JobCreatedAtTag, performingContext.BackgroundJob.CreatedAt.ToString("O"));
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
    }
}
