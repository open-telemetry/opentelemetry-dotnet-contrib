using System.Diagnostics.Metrics;

namespace OpenTelemetry.Instrumentation.Cassandra;

internal class CassandraMeter
{
    public static Meter Instance => new Meter(typeof(CassandraMeter).Assembly.GetName().Name);
}
