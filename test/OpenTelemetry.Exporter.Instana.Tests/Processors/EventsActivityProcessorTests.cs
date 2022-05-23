// <copyright file="EventsActivityProcessorTests.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Instana.Implementation;
using OpenTelemetry.Exporter.Instana.Implementation.Processors;
using Xunit;

namespace OpenTelemetry.Exporter.Instana.Tests.Processors
{
    public class EventsActivityProcessorTests
    {
        private EventsActivityProcessor eventsActivityProcessor = new EventsActivityProcessor();

        [Fact]
        public async Task ProcessAsync()
        {
            var activityTagsCollection = new ActivityTagsCollection();
            activityTagsCollection.Add(new KeyValuePair<string, object?>("eventTagKey", "eventTagValue"));
            var activityEvent = new ActivityEvent(
                "testActivityEvent",
                DateTimeOffset.MinValue,
                activityTagsCollection);

            var activityTagsCollection2 = new ActivityTagsCollection();
            activityTagsCollection2.Add(new KeyValuePair<string, object?>("eventTagKey2", "eventTagValue2"));
            var activityEvent2 = new ActivityEvent(
                "testActivityEvent2",
                DateTimeOffset.MaxValue,
                activityTagsCollection2);

            Activity activity = new Activity("testOperationName");
            activity.AddEvent(activityEvent);
            activity.AddEvent(activityEvent2);
            InstanaSpan instanaSpan = new InstanaSpan() { TransformInfo = new OpenTelemetry.Exporter.Instana.Implementation.InstanaSpanTransformInfo() };
            await this.eventsActivityProcessor.ProcessAsync(activity, instanaSpan);

            Assert.True(instanaSpan.Ec == 0);
            Assert.True(instanaSpan.Data.Events.Count == 2);
            Assert.True(instanaSpan.Data.Events[0].Name == "testActivityEvent");
            Assert.True(instanaSpan.Data.Events[0].Ts > 0);
            Assert.True(instanaSpan.Data.Events[0].Tags["eventTagKey"] == "eventTagValue");
            Assert.True(instanaSpan.Data.Events[1].Name == "testActivityEvent2");
            Assert.True(instanaSpan.Data.Events[1].Ts > 0);
            Assert.True(instanaSpan.Data.Events[1].Tags["eventTagKey2"] == "eventTagValue2");
        }
    }
}
