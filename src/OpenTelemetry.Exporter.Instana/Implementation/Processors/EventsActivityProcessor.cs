﻿// <copyright file="EventsActivityProcessor.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("OpenTelemetry.Exporter.Instana.Tests, PublicKey=002400000480000094000000060200000024000052534131000400000100010051c1562a090fb0c9f391012a32198b5e5d9a60e9b80fa2d7b434c9e5ccb7259bd606e66f9660676afc6692b8cdc6793d190904551d2103b7b22fa636dcbb8208839785ba402ea08fc00c8f1500ccef28bbf599aa64ffb1e1d5dc1bf3420a3777badfe697856e9d52070a50c3ea5821c80bef17ca3acffa28f89dd413f096f898")]

namespace OpenTelemetry.Exporter.Instana.Implementation.Processors
{
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

            await base.ProcessAsync(activity, instanaSpan);
        }
    }
}
