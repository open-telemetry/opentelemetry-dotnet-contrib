// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Reflection;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Implementation;

internal class RedisMetrics : IDisposable
{
    internal const string OperationMetricName = "db.client.operation.duration";
    internal const string QueueMetricName = "db.client.queue.duration";
    internal const string NetworkMetricName = "db.client.network.duration";

    internal static readonly Assembly Assembly = typeof(StackExchangeRedisInstrumentation).Assembly;
    internal static readonly AssemblyName AssemblyName = Assembly.GetName();
    internal static readonly string InstrumentationName = AssemblyName.Name!;
    internal static readonly string InstrumentationVersion = Assembly.GetPackageVersion();

    private readonly Meter meter;

    public RedisMetrics()
    {
        this.meter = new Meter(InstrumentationName, InstrumentationVersion);

        this.QueueHistogram = this.meter.CreateHistogram<double>(
            QueueMetricName,
            unit: "s",
            description: "Total time the redis request was waiting in queue before being sent to the server.");

        this.NetworkHistogram = this.meter.CreateHistogram<double>(
            NetworkMetricName,
            unit: "s",
            description: "Duration of redis requests since sent the request to receive the response.");

        this.OperationHistogram = this.meter.CreateHistogram<double>(
            OperationMetricName,
            unit: "s",
            description: "Total client request duration, including processing, queue and server duration.",
            [new(SemanticConventions.AttributeDbSystem, "redis")]);
    }

    public static RedisMetrics Instance { get; } = new RedisMetrics();

    public Histogram<double> QueueHistogram { get; }

    public Histogram<double> NetworkHistogram { get; }

    public Histogram<double> OperationHistogram { get; }

    public bool Enabled => this.OperationHistogram.Enabled;

    public void Dispose()
    {
        this.meter.Dispose();
    }
}
