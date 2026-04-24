// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Amazon.Runtime.Telemetry;
using Amazon.Runtime.Telemetry.Metrics;

namespace OpenTelemetry.Instrumentation.AWS.Implementation.Metrics;

internal sealed class AWSMonotonicCounter<T> : MonotonicCounter<T>
    where T : struct
{
    private static readonly ConcurrentDictionary<string, System.Diagnostics.Metrics.Counter<T>> MonotonicCountersDictionary = new();

    private readonly Func<bool> isDisposed;
    private readonly System.Diagnostics.Metrics.Counter<T> monotonicCounter;

    public AWSMonotonicCounter(
        System.Diagnostics.Metrics.Meter meter,
        string name,
        Func<bool> isDisposed,
        string? units = null,
        string? description = null)
    {
        this.isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));

#if NET
        this.monotonicCounter = MonotonicCountersDictionary.GetOrAdd(
            name,
            static (counterName, state) => CreateCounter(counterName, state),
            (meter, units, description));
#else
        this.monotonicCounter = MonotonicCountersDictionary.GetOrAdd(
            name,
            counterName => meter.CreateCounter<T>(counterName, units, description));
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
            this.monotonicCounter.Add(value, attributes.AllAttributes.ToArray());
        }
        else
        {
            this.monotonicCounter.Add(value);
        }
    }

    private static System.Diagnostics.Metrics.Counter<T> CreateCounter(
        string name,
        (System.Diagnostics.Metrics.Meter Meter, string? Units, string? Description) state)
        => state.Meter.CreateCounter<T>(name, state.Units, state.Description);
}
