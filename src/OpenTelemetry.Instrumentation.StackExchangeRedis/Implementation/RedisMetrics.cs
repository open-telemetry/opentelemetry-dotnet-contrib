// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Reflection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Implementation;

internal class RedisMetrics : IDisposable
{
    internal const string DurationMetricName = "db.client.operation.duration";

    internal static readonly Assembly Assembly = typeof(StackExchangeRedisInstrumentation).Assembly;
    internal static readonly AssemblyName AssemblyName = Assembly.GetName();
    internal static readonly string InstrumentationName = AssemblyName.Name!;
    internal static readonly string InstrumentationVersion = Assembly.GetPackageVersion();

    private readonly Meter meter;

    public RedisMetrics()
    {
        this.meter = new Meter(InstrumentationName, InstrumentationVersion);

        this.DurationHistogram = this.meter.CreateHistogram<double>(
            DurationMetricName,
            unit: "s",
            description: "Duration of database client operations.");
    }

    public static RedisMetrics Instance { get; } = new RedisMetrics();

    public Histogram<double> DurationHistogram { get; }

    public bool Enabled => this.DurationHistogram.Enabled;

    public void Dispose()
    {
        this.meter.Dispose();
    }
}
