// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Reflection;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Implementation;

internal class RedisMetrics : IDisposable
{
    internal const string DurationMetricName = "db.client.operation.duration";
    internal const string QueueTimeMetricName = "db.client.operation.queue_time";
    internal const string ServerTimeMetricName = "db.client.operation.server_time";

    internal static readonly Assembly Assembly = typeof(StackExchangeRedisInstrumentation).Assembly;
    internal static readonly AssemblyName AssemblyName = Assembly.GetName();
    internal static readonly string InstrumentationName = AssemblyName.Name!;
    internal static readonly string InstrumentationVersion = Assembly.GetPackageVersion();

    private readonly Meter meter;

    public RedisMetrics()
    {
        this.meter = new Meter(InstrumentationName, InstrumentationVersion);

        this.QueueTimeHistogram = this.meter.CreateHistogram<double>(
            QueueTimeMetricName,
            unit: "s",
            description: "Total time the redis request was waiting in queue before being sent to the server.");

        this.ServerTimeHistogram = this.meter.CreateHistogram<double>(
            ServerTimeMetricName,
            unit: "s",
            description: "Duration of redis requests since sent the request to receive the response.");

        this.DurationHistogram = this.meter.CreateHistogram<double>(
            DurationMetricName,
            unit: "s",
            description: "Total client request duration, including processing, queue and server duration.",
            [new(SemanticConventions.AttributeDbSystem, "redis")]);
    }

    public static RedisMetrics Instance { get; } = new RedisMetrics();

    public Histogram<double> QueueTimeHistogram { get; }

    public Histogram<double> ServerTimeHistogram { get; }

    public Histogram<double> DurationHistogram { get; }

    public bool Enabled => this.DurationHistogram.Enabled;

    public void Dispose()
    {
        this.meter.Dispose();
    }
}
