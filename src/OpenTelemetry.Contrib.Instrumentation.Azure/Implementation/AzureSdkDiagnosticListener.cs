﻿// <copyright file="AzureSdkDiagnosticListener.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Instrumentation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Instrumentation.Azure.Implementation
{
    internal class AzureSdkDiagnosticListener : ListenerHandler
    {
        internal const string ActivitySourceName = "AzureSDK";
        internal const string ActivityName = ActivitySourceName + ".HttpRequestOut";
        private static readonly Version Version = typeof(AzureSdkDiagnosticListener).Assembly.GetName().Version;
        private readonly ActivitySourceAdapter activitySource;

        // all fetchers must not be reused between DiagnosticSources.
        private readonly PropertyFetcher<IEnumerable<Activity>> linksPropertyFetcher = new PropertyFetcher<IEnumerable<Activity>>("Links");

        public AzureSdkDiagnosticListener(string sourceName, ActivitySourceAdapter activitySource)
            : base(sourceName)
        {
            this.activitySource = activitySource;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            string operationName = null;
            var activityKind = ActivityKind.Internal;

            foreach (var keyValuePair in activity.Tags)
            {
                if (keyValuePair.Key == "http.url")
                {
                    operationName = keyValuePair.Value;
                    activityKind = ActivityKind.Client;
                    break;
                }

                if (keyValuePair.Key == "kind")
                {
                    if (Enum.TryParse(keyValuePair.Value, true, out ActivityKind parsedActivityKind))
                    {
                        activityKind = parsedActivityKind;
                    }
                }
            }

            if (operationName == null)
            {
                operationName = this.GetOperationName(activity);
            }

            activity.DisplayName = operationName;
            this.activitySource.Start(activity, activityKind);
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            this.activitySource.Stop(activity);
        }

        public override void OnException(Activity activity, object payload)
        {
            activity.SetStatus(Status.Error.WithDescription(payload?.ToString()));
        }

        private string GetOperationName(Activity activity)
        {
            // activity name looks like 'Azure.<...>.<Class>.<Name>'
            // as namespace is too verbose, we'll just take the last two nodes from the activity name as telemetry name
            // this will change with https://github.com/Azure/azure-sdk-for-net/issues/9071 ~Feb 2020

            string activityName = activity.OperationName;
            int methodDotIndex = activityName.LastIndexOf('.');
            if (methodDotIndex <= 0)
            {
                return activityName;
            }

            int classDotIndex = activityName.LastIndexOf('.', methodDotIndex - 1);

            if (classDotIndex == -1)
            {
                return activityName;
            }

            return activityName.Substring(classDotIndex + 1, activityName.Length - classDotIndex - 1);
        }
    }
}
