// <copyright file="IMetricDataTransport.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Geneva;

internal interface IMetricDataTransport : IDisposable
{
    /// <summary>
    /// Writes a standard metric event containing only a single value.
    /// </summary>
    /// <param name="eventType">Type of the event.</param>
    /// <param name="body">The byte array containing the serialized data.</param>
    /// <param name="size">Length of the payload (fixed + variable).</param>
    void Send(
        MetricEventType eventType,
        byte[] body,
        int size);
}
