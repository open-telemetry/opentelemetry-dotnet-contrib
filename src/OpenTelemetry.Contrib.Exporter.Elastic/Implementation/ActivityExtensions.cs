// <copyright file="ActivityExtensions.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using OpenTelemetry.Contrib.Exporter.Elastic.Implementation.V2;

namespace OpenTelemetry.Contrib.Exporter.Elastic.Implementation
{
    /// <summary>
    /// Extension methods to create Elastic APM transactions/spans.
    /// </summary>
    internal static partial class ActivityExtensions
    {
        internal static IJsonSerializable ToElasticApmSpan(
            this Activity activity,
            ElasticOptions options)
        {
            if (options.IntakeApiVersion != IntakeApiVersion.V2)
            {
                throw new NotSupportedException();
            }

            string name = activity.DisplayName;
            string traceId = activity.GetTraceId();
            string id = activity.GetSpanId();
            string parentId = activity.GetParentId();
            long duration = activity.Duration.ToEpochMicroseconds();
            long timestamp = activity.StartTimeUtc.ToEpochMicroseconds();
            string type = activity.GetActivityType();

            if (activity.Kind == ActivityKind.Internal)
            {
                return new Span(name, traceId, id, parentId, duration, timestamp, type);
            }

            var httpStatusCode = activity.GetHttpStatusCode();
            var otelStatusCode = activity.GetOtelStatusCode();
            var result = options.CustomMappings.TransactionResult(httpStatusCode, otelStatusCode);
            var outcome = GetOutcome(httpStatusCode, default);

            return new Transaction(name, traceId, id, parentId, duration, timestamp, type, result, outcome);
        }

        private static string GetActivityType(this Activity activity)
        {
            return activity.Kind switch
            {
                ActivityKind.Server => "server",
                ActivityKind.Producer => "producer",
                ActivityKind.Consumer => "consumer",
                ActivityKind.Client => "client",
                _ => null,
            };
        }
    }
}
