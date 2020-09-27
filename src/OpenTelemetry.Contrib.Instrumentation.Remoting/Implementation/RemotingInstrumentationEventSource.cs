// <copyright file="RemotingInstrumentationEventSource.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Tracing;

namespace OpenTelemetry.Contrib.Instrumentation.Remoting.Implementation
{
    [EventSource(Name = "OpenTelemetry-Instrumentation-Remoting")]
    internal class RemotingInstrumentationEventSource : EventSource
    {
        public static readonly RemotingInstrumentationEventSource Log = new RemotingInstrumentationEventSource();

        [NonEvent]
        public void MessageFilterException(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, (EventKeywords)(-1)))
            {
                this.MessageFilterException(ex.ToString());
            }
        }

        [Event(1, Message = "InstrumentationFilter threw an exception. Message will not be collected. Exception {0}.", Level = EventLevel.Error)]
        public void MessageFilterException(string exception)
        {
            this.WriteEvent(1, exception);
        }

        [NonEvent]
        public void DynamicSinkException(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, (EventKeywords)(-1)))
            {
                this.DynamicSinkException(ex.ToString());
            }
        }

        [Event(2, Message = "DynamicSink message processor threw an exception. Remoting activity will not be recorded. Exception {0}.", Level = EventLevel.Error)]
        public void DynamicSinkException(string exception)
        {
            this.WriteEvent(2, exception);
        }
    }
}
