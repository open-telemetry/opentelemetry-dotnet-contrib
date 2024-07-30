// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Reflection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Implementation;

internal class RedisMetrics : IDisposable
{
    internal const string MetricRequestDurationName = "redis.client.request.duration";
    internal const string MetricQueueDurationName = "redis.client.queue.duration";
    internal const string MetricNetworkDurationName = "redis.client.network.duration";

    internal static readonly Assembly Assembly = typeof(StackExchangeRedisInstrumentation).Assembly;
    internal static readonly AssemblyName AssemblyName = Assembly.GetName();
    internal static readonly string InstrumentationName = AssemblyName.Name!;
    internal static readonly string InstrumentationVersion = Assembly.GetPackageVersion();

    private readonly Meter meter;

    public RedisMetrics()
    {
        this.meter = new Meter(InstrumentationName, InstrumentationVersion);

        this.QueueHistogram = this.meter.CreateHistogram<double>(
            MetricQueueDurationName,
            unit: "s",
            description: "Total time the redis request was waiting in queue before being sent to the server.");

        this.NetworkHistogram = this.meter.CreateHistogram<double>(
            MetricNetworkDurationName,
            unit: "s",
            description: "Duration of redis requests since sent the request to receive the response.");

        this.RequestHistogram = this.meter.CreateHistogram<double>(
            MetricRequestDurationName,
            unit: "s",
            description: "Total client request duration, including processing, queue and server duration.");
    }

    public static RedisMetrics Instance { get; } = new RedisMetrics();

    public Histogram<double> QueueHistogram { get; }

    public Histogram<double> NetworkHistogram { get; }

    public Histogram<double> RequestHistogram { get; }

    public bool Enabled => this.RequestHistogram.Enabled;

    public void Dispose()
    {
        this.meter.Dispose();
    }
}
