# Cassandra Instrumentation for OpenTelemetry

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.Cassandra.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Cassandra)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.Cassandra.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Cassandra)

This is an
[Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments [CassandraCSharpDriver](https://github.com/datastax/csharp-driver)
and collects telemetry about cassandra metrics.

## Steps to enable OpenTelemetry.Instrumentation.Cassandra

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.Cassandra`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Cassandra)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Instrumentation.Cassandra
```

### Step 2: Enable Cassandra Instrumentation at application startup

Cassandra instrumentation must be enabled at application startup.

The following example demonstrates adding Cassandra instrumentation to a
console application. This example also sets up the OpenTelemetry Console
exporter, which requires adding the package
[`OpenTelemetry.Exporter.Console`](https://www.nuget.org/packages/OpenTelemetry.Exporter.Console)
to the application.

```csharp
using OpenTelemetry.Trace;

public class Program
{
    public static void Main(string[] args)
    {
        using var metricProvider = Sdk.CreateMeterProviderBuilder()
            .AddCassandraInstrumentation()
            .AddConsoleExporter()
            .Build();

        var cluster = new Builder()
            .WithConnectionString(yourCassandraConnectionString)
            .WithOpenTelemetryMetrics()
            .Build();
    }
}
```

For an ASP.NET Core application, adding instrumentation is typically done in
the `ConfigureServices` of your `Startup` class. Refer to documentation for
[OpenTelemetry.Instrumentation.AspNetCore](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Instrumentation.AspNetCore/README.md).

For an ASP.NET application, adding instrumentation is typically done in the
`Global.asax.cs`. Refer to documentation for [OpenTelemetry.Instrumentation.AspNet](../OpenTelemetry.Instrumentation.AspNet/README.md).

## Configuration options

You are able to configure your own cassandra driver metrics options in
`WithOpenTelemetryMetrics` the by default configuration includes all the metrics
 that the driver can provide.

```csharp
var options = new DriverMetricsOptions();

options.SetEnabledNodeMetrics(new[] {NodeMetric.Gauges.InFlight});

var cluster = new Builder()
    .WithConnectionString(yourCassandraConnectionString)
    .WithOpenTelemetryMetrics(options)
    .Build();
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)

* [OpenTelemetry semantic conventions for database calls](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/database.md)

* [Cassandra C# Driver](https://github.com/datastax/csharp-driver)
