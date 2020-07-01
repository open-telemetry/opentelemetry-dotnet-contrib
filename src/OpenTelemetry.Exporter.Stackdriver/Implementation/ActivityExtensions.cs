// <copyright file="ActivityExtensions.cs" company="OpenTelemetry Authors">
// Copyright 2020, OpenTelemetry Authors
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

using System.Linq;
using System.Diagnostics;
using Google.Cloud.Trace.V2;
using Google.Protobuf.WellKnownTypes;

namespace OpenTelemetry.Exporter.Stackdriver.Implementation
{
    internal static class ActivityExtensions {
        /// <summary>
        /// Translating an Activity to Stackdriver's Span format.
        /// According to <see href="https://cloud.google.com/trace/docs/reference/v2/rpc/google.devtools.cloudtrace.v2"/> specifications.
        /// </summary>
        /// <param name="activity">Activity.</param>
        /// <param name="projectId">Google Cloud Platform Project Id.</param>
        /// <returns><see cref="TelemetrySpan"/>.</returns>
        public static Google.Cloud.Trace.V2.Span ToSpan(this Activity activity, string projectId)
        {
            var spanId = activity.SpanId.ToHexString();

            // Base span settings
            var span = new Google.Cloud.Trace.V2.Span
            {
                SpanName = new SpanName(projectId, activity.TraceId.ToHexString(), spanId),
                SpanId = spanId,
                DisplayName = new TruncatableString { Value = activity.OperationName },
                StartTime = Timestamp.FromDateTime(activity.StartTimeUtc),
                EndTime = Timestamp.FromDateTime(activity.StartTimeUtc.Add(activity.Duration)),
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
                span.Links = new Google.Cloud.Trace.V2.Span.Types.Links
                {
                    Link = { activity.Links.Select(l => l.ToLink()) },
                };
            }

            // Span Attributes
            if (activity.Tags != null)
            {
                span.Attributes = new Google.Cloud.Trace.V2.Span.Types.Attributes
                {
                    AttributeMap =
                    {
                        activity.Tags.Where(t => t.Key != null && t.Value != null)
                            .ToDictionary(t => t.Key, t => t.Value.ToAttributeValue()),
                    }
                };
            }

            // Span Events
            if (activity.Events != null)
            {
                span.TimeEvents = new Google.Cloud.Trace.V2.Span.Types.TimeEvents
                {
                    TimeEvent = { activity.Events.Select(e => e.toEvent()) },
                };
            }
            // TODO: Attributes
            return span;
        }

        public static Google.Cloud.Trace.V2.Span.Types.Link ToLink(this ActivityLink link)
        {
            var ret = new Google.Cloud.Trace.V2.Span.Types.Link();
            ret.SpanId = link.Context.SpanId.ToHexString();
            ret.TraceId = link.Context.TraceId.ToHexString();

            if (link.Attributes != null)
            {
                ret.Attributes = new Google.Cloud.Trace.V2.Span.Types.Attributes
                {
                    AttributeMap =
                    {
                        link.Attributes.Where(t => t.Key != null && t.Value != null)
                            .ToDictionary(att => att.Key, att => att.Value.ToAttributeValue()),
                    },
                };
            }

            return ret;
        }

        public static Google.Cloud.Trace.V2.Span.Types.TimeEvent toEvent(this ActivityEvent ev)
        {
            return new Google.Cloud.Trace.V2.Span.Types.TimeEvent{
                Time = Timestamp.FromDateTimeOffset(ev.Timestamp),
                Annotation = new Google.Cloud.Trace.V2.Span.Types.TimeEvent.Types.Annotation{
                    Description = new TruncatableString{ Value = ev.Name },
                    Attributes = new Google.Cloud.Trace.V2.Span.Types.Attributes
                    {
                        AttributeMap =
                        {
                            ev.Attributes.Where(t => t.Key != null && t.Value != null)
                                .ToDictionary(att => att.Key, att => att.Value.ToAttributeValue()),
                        },
                    },
                },
            };
        }
    }
}
