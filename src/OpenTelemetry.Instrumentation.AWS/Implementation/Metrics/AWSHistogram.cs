// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Runtime.Telemetry;
using Amazon.Runtime.Telemetry.Metrics;

namespace OpenTelemetry.Instrumentation.AWS.Implementation.Metrics;

internal class AWSHistogram<T> : Histogram<T>
    where T : struct
{
    private readonly System.Diagnostics.Metrics.Histogram<T> histogram;

    public AWSHistogram(System.Diagnostics.Metrics.Histogram<T> histogram)
    {
        this.histogram = histogram;
    }

    public override void Record(T value, Attributes? attributes = null)
    {
        if (attributes != null)
        {
            this.histogram.Record(value, attributes.AllAttributes.ToArray());
        }
        else
        {
            this.histogram.Record(value);
        }
    }
}
