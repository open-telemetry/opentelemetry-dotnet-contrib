// <copyright file="TraceEventExtensions.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Tracing;

namespace OpenTelemetry.Contrib.EventCounterListener.EventPipe
{
    internal static class TraceEventExtensions
    {
        public static bool TryGetCounterPayload(this EventWrittenEventArgs eventArgs, /* CounterFilter filter,*/ out ICounterPayload payload)
        {
            payload = null;

            if ("EventCounters".Equals(eventArgs.EventName))
            {
                // IDictionary<string, object> payloadVal = (IDictionary<string, object>)eventArgs.Payload;
                // IDictionary<string, object> payloadFields = (IDictionary<string, object>)(payloadVal["Payload"]);
                var rawPayloadIndex = eventArgs.PayloadNames.IndexOf("Payload");
                if (rawPayloadIndex < 0)
                {
                    return false;
                }

                var payloadFields = eventArgs.Payload[rawPayloadIndex] as IDictionary<string, object>;

                //Make sure we are part of the requested series. If multiple clients request metrics, all of them get the metrics.
                string series = payloadFields["Series"].ToString();
                string counterName = payloadFields["Name"].ToString();

                //CONSIDER
                //Concurrent counter sessions do not each get a separate interval. Instead the payload
                //for _all_ the counters changes the Series to be the lowest specified interval, on a per provider basis.
                //Currently the CounterFilter will remove any data whose Series doesn't match the requested interval.

                //Do filtering somewhere else  -- hananiel
                //if (!filter.IsIncluded(traceEvent.ProviderName, counterName, GetInterval(series)))
                //{
                //    return false;
                //}

                float intervalSec = (float)payloadFields["IntervalSec"];
                string displayName = payloadFields["DisplayName"].ToString();
                string displayUnits = payloadFields["DisplayUnits"].ToString();
                double value = 0;
                CounterType counterType = CounterType.Metric;

                if (payloadFields["CounterType"].Equals("Mean"))
                {
                    value = (double)payloadFields["Mean"];
                }
                else if (payloadFields["CounterType"].Equals("Sum"))
                {
                    counterType = CounterType.Rate;
                    value = (double)payloadFields["Increment"];
                    if (string.IsNullOrEmpty(displayUnits))
                    {
                        displayUnits = "count";
                    }
                }

                payload = new CounterPayload(
                    DateTime.Now,
                    eventArgs.EventSource.Name,
                    counterName,
                    displayName,
                    displayUnits,
                    value,
                    counterType,
                    intervalSec);
                return true;
            }

            return false;
        }

        private static int GetInterval(string series)
        {
            const string comparison = "Interval=";
            int interval = 0;
            if (series.StartsWith(comparison, StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(series.Substring(comparison.Length), out interval);
            }
            return interval;
        }
    }
}
