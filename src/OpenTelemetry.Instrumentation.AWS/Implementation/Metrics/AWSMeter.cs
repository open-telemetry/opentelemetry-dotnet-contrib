// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Runtime.Telemetry.Metrics;

namespace OpenTelemetry.Instrumentation.AWS.Implementation.Metrics;

internal sealed class AWSMeter : Meter
{
    private readonly System.Diagnostics.Metrics.Meter meter;

    /// <summary>
    /// Initializes a new instance of the <see cref="AWSMeter"/> class.
    /// </summary>
    /// <param name="meter">The Meter used for creating and tracking the Instruments.</param>
    public AWSMeter(System.Diagnostics.Metrics.Meter meter)
    {
        this.meter = meter ?? throw new ArgumentNullException(nameof(meter));
    }

    public override UpDownCounter<T> CreateUpDownCounter<T>(
        string name,
        string? units = null,
        string? description = null)
        where T : struct
    {
        var upDownCounter = this.meter.CreateUpDownCounter<T>(name, units, description);
        return new AWSUpDownCounter<T>(upDownCounter);
    }

    public override MonotonicCounter<T> CreateMonotonicCounter<T>(
        string name,
        string? units = null,
        string? description = null)
        where T : struct
    {
        var counter = this.meter.CreateCounter<T>(name, units, description);
        return new AWSMonotonicCounter<T>(counter);
    }

    public override Histogram<T> CreateHistogram<T>(
        string name,
        string? units = null,
        string? description = null)
        where T : struct
    {
        var histogram = this.meter.CreateHistogram<T>(name, units, description);
        return new AWSHistogram<T>(histogram);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.meter?.Dispose();
        }

        base.Dispose(disposing);
    }
}
