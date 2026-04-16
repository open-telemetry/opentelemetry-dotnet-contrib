// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Amazon.Runtime.Telemetry;
using Amazon.Runtime.Telemetry.Metrics;

namespace OpenTelemetry.Instrumentation.AWS.Implementation.Metrics;

internal sealed class AWSMeterProvider : MeterProvider
{
    private static readonly ConcurrentDictionary<string, System.Diagnostics.Metrics.Meter> MetersDictionary = new();

    public override Meter GetMeter(string scope, Attributes? attributes = null)
    {
        // Passing attributes to the Meter is currently not possible due to version limitations
        // in the dependencies. Since none of the SDK operations utilize attributes at this level,
        // so we will omit the attributes for now.
        // This will be revisited after the release of OpenTelemetry.Extensions.AWS which will
        // update OpenTelemetry core component version(s) to `1.9.0` and allow passing tags to
        // the meter constructor.

        var meter = MetersDictionary.GetOrAdd(
            scope,
            static scopeName => new System.Diagnostics.Metrics.Meter(scopeName));

        return new AWSMeter(meter);
    }
}
