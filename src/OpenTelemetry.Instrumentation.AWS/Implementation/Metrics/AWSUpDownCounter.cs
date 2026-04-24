// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Amazon.Runtime.Telemetry;
using Amazon.Runtime.Telemetry.Metrics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.AWS.Implementation.Metrics;

internal sealed class AWSUpDownCounter<T> : UpDownCounter<T>
    where T : struct
{
    private static readonly ConcurrentDictionary<string, System.Diagnostics.Metrics.UpDownCounter<T>> UpDownCountersDictionary = new();

    private readonly Func<bool> isDisposed;
    private readonly System.Diagnostics.Metrics.UpDownCounter<T> upDownCounter;

    public AWSUpDownCounter(
        System.Diagnostics.Metrics.Meter meter,
        string name,
        Func<bool> isDisposed,
        string? units = null,
        string? description = null)
    {
        Guard.ThrowIfNull(isDisposed);
        this.isDisposed = isDisposed;

#if NET
        this.upDownCounter = UpDownCountersDictionary.GetOrAdd(
            name,
            static (counterName, state) => CreateUpDownCounter(counterName, state),
            (meter, units, description));
#else
        this.upDownCounter = UpDownCountersDictionary.GetOrAdd(
            name,
            counterName => meter.CreateUpDownCounter<T>(counterName, units, description));
#endif
    }

    public override void Add(T value, Attributes? attributes = null)
    {
        if (this.isDisposed())
        {
            return;
        }

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

    private static System.Diagnostics.Metrics.UpDownCounter<T> CreateUpDownCounter(
        string name,
        (System.Diagnostics.Metrics.Meter Meter, string? Units, string? Description) state)
        => state.Meter.CreateUpDownCounter<T>(name, state.Units, state.Description);
}
