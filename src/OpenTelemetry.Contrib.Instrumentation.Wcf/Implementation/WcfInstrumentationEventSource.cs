﻿// <copyright file="WcfInstrumentationEventSource.cs" company="OpenTelemetry Authors">
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
using System.Globalization;
using System.Threading;

namespace OpenTelemetry.Contrib.Instrumentation.Wcf.Implementation
{
    [EventSource(Name = "OpenTelemetry-Instrumentation-Wcf")]
    internal class WcfInstrumentationEventSource : EventSource
    {
        public static readonly WcfInstrumentationEventSource Log = new WcfInstrumentationEventSource();

        [NonEvent]
        public void RequestFilterException(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, (EventKeywords)(-1)))
            {
                this.RequestFilterException(ToInvariantString(ex));
            }
        }

        [Event(EventIds.RequestIsFilteredOut, Message = "Request is filtered out.", Level = EventLevel.Verbose)]
        public void RequestIsFilteredOut(string eventName)
        {
            this.WriteEvent(EventIds.RequestIsFilteredOut, eventName);
        }

        [Event(EventIds.RequestFilterException, Message = "InstrumentationFilter threw exception. Request will not be collected. Exception {0}.", Level = EventLevel.Error)]
        public void RequestFilterException(string exception)
        {
            this.WriteEvent(EventIds.RequestFilterException, exception);
        }

        /// <summary>
        /// Returns a culture-independent string representation of the given <paramref name="exception"/> object,
        /// appropriate for diagnostics tracing.
        /// </summary>
        private static string ToInvariantString(Exception exception)
        {
            var originalUICulture = Thread.CurrentThread.CurrentUICulture;

            try
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
                return exception.ToString();
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = originalUICulture;
            }
        }

        private class EventIds
        {
            public const int RequestIsFilteredOut = 1;
            public const int RequestFilterException = 2;
        }
    }
}
