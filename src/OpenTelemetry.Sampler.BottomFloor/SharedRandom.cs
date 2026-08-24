// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Sampler.BottomFloor;

/// <summary>
/// Supplies the default random source for the per-arrival draw.
/// <para/>
/// The sampler draws once per record from a shared generator, so that generator
/// has to be safe to use from every thread that logs. <c>Random.Shared</c>
/// provides exactly that, but only on .NET 6 and later. On the downlevel
/// targets this falls back to a per-thread generator, which gives each thread
/// its own unshared state instead of serialising on a lock.
/// <para/>
/// Each thread's generator is seeded from a <see cref="Guid"/> rather than left
/// to the default constructor: on .NET Framework <c>new Random()</c> seeds from
/// the tick count, so threads started within the same tick would otherwise draw
/// identical sequences and bias the sample.
/// </summary>
internal static class SharedRandom
{
#if NET
    /// <summary>
    /// Gets the shared, thread-safe random source.
    /// </summary>
    public static Random Instance => Random.Shared;
#else
#pragma warning disable CA5394 // Do not use insecure randomness

    /// <summary>
    /// Gets the shared, thread-safe random source.
    /// </summary>
    public static Random Instance { get; } = new PerThreadRandom();

    private sealed class PerThreadRandom : Random
    {
        [ThreadStatic]
        private static Random? current;

        private static Random Current => current ??= new Random(Guid.NewGuid().GetHashCode());

        public override int Next() => Current.Next();

        public override int Next(int maxValue) => Current.Next(maxValue);

        public override int Next(int minValue, int maxValue) => Current.Next(minValue, maxValue);

        public override void NextBytes(byte[] buffer) => Current.NextBytes(buffer);

        public override double NextDouble() => Current.NextDouble();

        protected override double Sample() => Current.NextDouble();
    }
#pragma warning restore CA5394
#endif
}
