// <copyright file="DriverMeter.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics.Metrics;
using Cassandra.Metrics.Abstractions;

namespace OpenTelemetry.Instrumentation.Cassandra;

internal sealed class DriverMeter : IDriverMeter
{
    private readonly Histogram<long> meter;

    public DriverMeter(string name)
    {
        this.meter = CassandraMeter.Instance.CreateHistogram<long>(name);
    }

    public void Mark(long amount)
    {
        this.meter.Record(amount);
    }
}
