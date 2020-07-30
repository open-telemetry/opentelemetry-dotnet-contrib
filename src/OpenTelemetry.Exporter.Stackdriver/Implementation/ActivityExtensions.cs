﻿// <copyright file="ActivityExtensions.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Google.Cloud.Trace.V2;
using Google.Protobuf.WellKnownTypes;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Stackdriver.Implementation
{
    internal static class ActivityExtensions
    {
        private static Dictionary<string, string> labelsToReplace = new Dictionary<string, string>
        {
            { "component", "/component" },
            { "http.method", "/http/method" },
            { "http.host", "/http/host" },
            { "http.status_code", "/http/status_code" },
            { "http.user_agent", "/http/user_agent" },
            { "http.path", "/http/path" },
            { "http.url", "/http/url" },
            { "http.route", "/http/route" },
        };

        /// <summary>
        /// Translating <see cref="Activity"/> to Stackdriver's Span
        /// According to <see href="https://cloud.google.com/trace/docs/reference/v2/rpc/google.devtools.cloudtrace.v2"/> specifications.
        /// </summary>
        /// <param name="activity">Activity in OpenTelemetry format.</param>
        /// <param name="projectId">Google Cloud Platform Project Id.</param>
        /// <returns><see cref="TelemetrySpan"/>.</returns>
        public static Span ToSpan(this Activity activity, string projectId)
        {
            var spanId = activity.Context.SpanId.ToHexString();

            // Base span settings
            var span = new Span
            {
                SpanName = new SpanName(projectId, activity.Context.TraceId.ToHexString(), spanId),
                SpanId = spanId,
                DisplayName = new TruncatableString { Value = activity.DisplayName },
                StartTime = activity.StartTimeUtc.ToTimestamp(),
                EndTime = activity.StartTimeUtc.Add(activity.Duration).ToTimestamp(),
                ChildSpanCount = null,
            };
            if (activity.ParentSpanId != null)
            {
                var parentSpanId = activity.ParentSpanId.ToHexString();
                if (!string.IsNullOrEmpty(parentSpanId))
                {
                    span.ParentSpanId = parentSpanId;
                }
            }

            // Span Links
            if (activity.Links != null)
            {
                span.Links = new Span.Types.Links
                {
                    Link = { activity.Links.Select(l => l.ToLink()) },
                };
            }

            // Span Attributes
            if (activity.Tags != null)
            {
                span.Attributes = new Span.Types.Attributes
                {
                    AttributeMap =
                    {
                        activity.Tags?.ToDictionary(
                                        s => s.Key,
                                        s => s.Value?.ToAttributeValue()),
                    },
                };
            }

            // StackDriver uses different labels that are used to categorize spans
            // replace attribute keys with StackDriver version
            foreach (var entry in labelsToReplace)
            {
                if (span.Attributes.AttributeMap.TryGetValue(entry.Key, out var attrValue))
                {
                    span.Attributes.AttributeMap.Remove(entry.Key);
                    span.Attributes.AttributeMap.Add(entry.Value, attrValue);
                }
            }

            return span;
        }

        public static Span.Types.Link ToLink(this ActivityLink link)
        {
            var ret = new Span.Types.Link
            {
                SpanId = link.Context.SpanId.ToHexString(),
                TraceId = link.Context.TraceId.ToHexString(),
            };

            if (link.Attributes != null)
            {
                ret.Attributes = new Span.Types.Attributes
                {
                    AttributeMap =
                    {
                        link.Attributes.ToDictionary(
                         att => att.Key,
                         att => att.Value.ToAttributeValue()),
                    },
                };
            }

            return ret;
        }

        public static AttributeValue ToAttributeValue(this object av)
        {
            switch (av)
            {
                case string s:
                    return new AttributeValue()
                    {
                        StringValue = new TruncatableString() { Value = s },
                    };
                case bool b:
                    return new AttributeValue() { BoolValue = b };
                case long l:
                    return new AttributeValue() { IntValue = l };
                case double d:
                    return new AttributeValue()
                    {
                        StringValue = new TruncatableString() { Value = d.ToString() },
                    };
                default:
                    return new AttributeValue()
                    {
                        StringValue = new TruncatableString() { Value = av.ToString() },
                    };
            }
        }
    }
}
