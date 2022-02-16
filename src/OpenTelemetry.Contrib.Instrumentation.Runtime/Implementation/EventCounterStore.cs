// <copyright file="EventCounterStore.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Contrib.Instrumentation.Runtime.Implementation
{
    internal class EventCounterStore : IEventCounterStore
    {
        private readonly Dictionary<string, CounterInfo> eventCounters = new Dictionary<string, CounterInfo>();

        public void Subscribe(string counterName, EventCounterType type)
        {
            this.eventCounters[counterName] = new CounterInfo
            {
                Value = 0,
                Parser = type == EventCounterType.Sum ? TryParseIncrementingCounter : TryParseCounter,
            };
        }

        public bool HasSubscription(string counterName)
        {
            return this.eventCounters.ContainsKey(counterName);
        }

        public bool HasSubscriptions()
        {
            return this.eventCounters.Count > 0;
        }

        public double ReadDouble(string counterName)
        {
            return Convert.ToDouble(this.eventCounters[counterName].Value);
        }

        public long ReadLong(string counterName)
        {
            return Convert.ToInt64(this.eventCounters[counterName].Value);
        }

        public void WriteValue(string counterName, IDictionary<string, object> eventPayload)
        {
            var info = this.eventCounters[counterName];
            var value = info.Parser(eventPayload);
            if (value == null)
            {
                throw new MismatchedCounterTypeException($"Counter '{counterName}' could not be parsed indicating the counter has been declared as the wrong type.");
            }
            else
            {
                info.Value = value;
            }
        }

        private static object TryParseIncrementingCounter(IDictionary<string, object> payload)
        {
            if (payload.TryGetValue("Increment", out var increment))
            {
                return increment;
            }

            return null;
        }

        private static object TryParseCounter(IDictionary<string, object> payload)
        {
            if (payload.TryGetValue("Mean", out var mean))
            {
                return mean;
            }

            return null;
        }

        private sealed class CounterInfo
        {
            public Func<IDictionary<string, object>, object?> Parser { get; set; }

            public object Value { get; set; }
        }
    }
}
