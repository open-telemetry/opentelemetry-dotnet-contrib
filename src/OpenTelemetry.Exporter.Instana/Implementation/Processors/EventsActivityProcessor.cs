// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Exporter.Instana.Implementation.Processors;

internal sealed class EventsActivityProcessor : ActivityProcessorBase
{
    public override void Process(Activity activity, InstanaSpan instanaSpan)
    {
        this.PreProcess(activity, instanaSpan);

        foreach (var activityEvent in activity.Events)
        {
            if (activityEvent.Name == InstanaExporterConstants.ExceptionField && instanaSpan.TransformInfo != null)
            {
                instanaSpan.TransformInfo.HasExceptionEvent = true;
            }

            var spanEvent = new SpanEvent
            {
                Name = activityEvent.Name,
                Ts = activityEvent.Timestamp.Ticks,
                Tags = [],
            };

            foreach (var eventTag in activityEvent.Tags)
            {
                if (eventTag.Value != null)
                {
                    spanEvent.Tags[eventTag.Key] = eventTag.Value.ToString() ?? string.Empty;
                }
            }

            instanaSpan.Data.Events.Add(spanEvent);
        }

        base.Process(activity, instanaSpan);
    }
}
