// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Runtime.Telemetry;
using Amazon.Runtime.Telemetry.Metrics;

namespace OpenTelemetry.Instrumentation.AWS.Implementation.Metrics;

internal class AWSMonotonicCounter<T> : MonotonicCounter<T>
    where T : struct
{
    private readonly System.Diagnostics.Metrics.Counter<T> counter;

    public AWSMonotonicCounter(System.Diagnostics.Metrics.Counter<T> counter)
    {
        this.counter = counter;
    }

    public override void Add(T value, Attributes? attributes = null)
    {
        if (attributes != null)
        {
            this.counter.Add(value, attributes.AllAttributes.ToArray());
        }
        else
        {
            this.counter.Add(value);
        }
    }
}
