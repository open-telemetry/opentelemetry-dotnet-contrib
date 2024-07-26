// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Runtime.Telemetry;
using Amazon.Runtime.Telemetry.Metrics;

namespace OpenTelemetry.Instrumentation.AWS.Implementation.Metrics;

internal class AWSUpDownCounter<T> : UpDownCounter<T>
    where T : struct
{
    private readonly System.Diagnostics.Metrics.UpDownCounter<T> upDownCounter;

    public AWSUpDownCounter(System.Diagnostics.Metrics.UpDownCounter<T> upDownCounter)
    {
        this.upDownCounter = upDownCounter;
    }

    public override void Add(T value, Attributes? attributes = null)
    {
        if (attributes != null)
        {
            this.upDownCounter.Add(value, attributes.AllAttributes.ToArray());
        }
        else
        {
            this.upDownCounter.Add(value);
        }
    }
}
