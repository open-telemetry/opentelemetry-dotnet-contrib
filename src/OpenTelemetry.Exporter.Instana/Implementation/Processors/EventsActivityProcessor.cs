// <copyright file="EventsActivityProcessor.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;

namespace OpenTelemetry.Exporter.Instana.Implementation.Processors;

internal class EventsActivityProcessor : ActivityProcessorBase, IActivityProcessor
{
    public override async Task ProcessAsync(Activity activity, InstanaSpan instanaSpan)
    {
        this.PreProcess(activity, instanaSpan);

        foreach (var activityEvent in activity.Events)
        {
            if (activityEvent.Name == InstanaExporterConstants.EXCEPTION_FIELD)
            {
                instanaSpan.TransformInfo.HasExceptionEvent = true;
            }

            var spanEvent = new SpanEvent
            {
                Name = activityEvent.Name,
                Ts = activityEvent.Timestamp.Ticks,
                Tags = new Dictionary<string, string>(),
            };

            foreach (var eventTag in activityEvent.Tags)
            {
                if (eventTag.Value != null)
                {
                    spanEvent.Tags[eventTag.Key] = eventTag.Value.ToString();
                }
            }

            instanaSpan.Data.Events.Add(spanEvent);
        }

        await base.ProcessAsync(activity, instanaSpan).ConfigureAwait(false);
    }
}
