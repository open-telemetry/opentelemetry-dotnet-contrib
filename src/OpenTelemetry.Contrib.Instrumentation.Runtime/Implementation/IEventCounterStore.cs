// <copyright file="IEventCounterStore.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Contrib.Instrumentation.Runtime.Implementation
{
    internal interface IEventCounterStore
    {
        /// <summary>
        /// Subscribe events of the given event counter.
        /// </summary>
        /// <param name="counterName">Name of the counter.</param>
        /// <param name="type">Type of the counter.</param>
        void Subscribe(string counterName, EventCounterType type);

        /// <summary>
        /// Determine whether an event counter was subscribed.
        /// </summary>
        /// <param name="counterName">Name of the event counter.</param>
        /// <returns>True if the event counter was subscribed.</returns>
        bool HasSubscription(string counterName);

        /// <summary>
        /// Remove the subscription to event counter events.
        /// </summary>
        /// <param name="counterName">Name of the counter.</param>
        void Unsubscribe(string counterName);

        /// <summary>
        /// Extracts and persists the counter value.
        /// </summary>
        /// <param name="counterName">Name of the event counter.</param>
        /// <param name="eventPayload">The event data.</param>
        void WriteValue(string counterName, IDictionary<string, object> eventPayload);

        /// <summary>
        /// Reads a double value stored for the given counter name.
        /// </summary>
        /// <param name="counterName">Name of the counter.</param>
        /// <returns>A value stored for the event counter.</returns>
        double ReadDouble(string counterName);

        /// <summary>
        /// Reads a long value stored for the given counter name.
        /// </summary>
        /// <param name="counterName">Name of the counter.</param>
        /// <returns>A value stored for the event counter.</returns>
        long ReadLong(string counterName);
    }
}
