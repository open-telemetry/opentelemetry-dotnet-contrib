// <copyright file="ActivityExtensions.Identifiers.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;

namespace OpenTelemetry.Contrib.Exporter.Elastic.Implementation
{
    /// <summary>
    /// Extension methods to create Elastic APM transactions/spans.
    /// </summary>
    internal static partial class ActivityExtensions
    {
        private static string GetSpanId(this Activity activity)
        {
            return activity.SpanId.ToHexString();
        }

        private static string GetTraceId(this Activity activity)
        {
            return activity.Context.TraceId.ToHexString();
        }

        private static string GetParentId(this Activity activity)
        {
            return activity.ParentSpanId == default
                ? null
                : activity.ParentSpanId.ToHexString();
        }
    }
}
