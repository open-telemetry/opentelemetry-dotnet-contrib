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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using OpenTelemetry.Instrumentation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Instrumentation.Quartz.Implementation
{
    internal sealed class QuartzDiagnosticListener : ListenerHandler
    {
        internal static readonly AssemblyName AssemblyName = typeof(QuartzDiagnosticListener).Assembly.GetName();
        internal static readonly string ActivitySourceName = AssemblyName.Name;
        internal static readonly Version Version = AssemblyName.Version;
        internal static readonly ActivitySource ActivitySource = new ActivitySource(ActivitySourceName, Version.ToString());

        private readonly QuartzInstrumentationOptions options;

        public QuartzDiagnosticListener(string sourceName, QuartzInstrumentationOptions options)
            : base(sourceName)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            // By this time, samplers have already run and
            // activity.IsAllDataRequested populated accordingly.

            if (Sdk.SuppressInstrumentation)
            {
                return;
            }

            if (activity.IsAllDataRequested)
            {
                if (this.options.TracedOperations != null && !this.options.TracedOperations.Contains(activity.OperationName))
                {
                    QuartzInstrumentationEventSource.Log.RequestIsFilteredOut(activity.OperationName);
                    activity.IsAllDataRequested = false;
                    return;
                }

                activity.DisplayName = this.GetDisplayName(activity);

                ActivityInstrumentationHelper.SetActivitySourceProperty(activity, ActivitySource);
                ActivityInstrumentationHelper.SetKindProperty(activity, this.GetActivityKind(activity));
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            if (activity.IsAllDataRequested)
            {
                try
                {
                    return;
                }
                catch (Exception ex)
                {
                    QuartzInstrumentationEventSource.Log.EnrichmentException(ex);
                }
            }
        }

        public override void OnException(Activity activity, object payload)
        {
            if (!this.options.TracedOperations.Contains(activity.OperationName))
            {
                return;
            }

            if (!(payload is Exception exception))
            {
                QuartzInstrumentationEventSource.Log.NullPayload(nameof(QuartzDiagnosticListener), nameof(this.OnStopActivity));
                return;
            }

            activity.AddTag(SemanticConventions.AttributeExceptionType, "true");
            activity.AddTag(SemanticConventions.AttributeExceptionMessage, this.options.IncludeExceptionDetails ? exception.Message : $"{nameof(QuartzInstrumentationOptions.IncludeExceptionDetails)} is disabled");
            activity.AddTag(SemanticConventions.AttributeExceptionStacktrace, this.options.IncludeExceptionDetails ? exception.StackTrace : $"{nameof(QuartzInstrumentationOptions.IncludeExceptionDetails)} is disabled");
        }

        private string GetDisplayName(Activity activity)
        {
            return activity.OperationName switch
            {
                OperationName.Job.Execute => $"execute {this.GetTag(activity.Tags, TagName.JobName)}",
                OperationName.Job.Veto => $"veto {this.GetTag(activity.Tags, TagName.JobName)}",
                _ => activity.DisplayName,
            };
        }

        private ActivityKind GetActivityKind(Activity activity)
        {
            return activity.OperationName switch
            {
                OperationName.Job.Execute => ActivityKind.Internal,
                OperationName.Job.Veto => ActivityKind.Internal,
                _ => activity.Kind,
            };
        }

        private string GetTag(IEnumerable<KeyValuePair<string, string>> tags, string tagName)
         {
             var tag = tags.SingleOrDefault(kv => kv.Key == tagName);
             return tag.Value;
         }
    }
}
