// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Runtime.Telemetry;
using Amazon.Runtime.Telemetry.Metrics;

namespace OpenTelemetry.Instrumentation.AWS.Implementation.Metrics;

internal sealed class AWSUpDownCounter<T> : UpDownCounter<T>
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
            // TODO: remove ToArray call and use when AttributesAsSpan expected to be added at AWS SDK v4.
            this.upDownCounter.Add(value, attributes.AllAttributes.ToArray());
        }
        else
        {
            this.upDownCounter.Add(value);
        }
    }
}
