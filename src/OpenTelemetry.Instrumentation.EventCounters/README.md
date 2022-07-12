# Event counter instrumentation for OpenTelemetry

This is an [Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which republishes EventCounters using Metrics Api.

## Steps to enable OpenTelemetry.Instrumentation.EventCounters

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.EventCounters`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.EventCounters)
package. Also, add any other instrumentation & exporters you will need.

```shell
dotnet add package OpenTelemetry.Instrumentation.EventCounters
```

### Step 2: Enable EventCounters Instrumentation at application startup

EventCounters instrumentation must be enabled at application startup.

The following example demonstrates adding EventCounter events to a
console application. This example also sets up the OpenTelemetry Console
exporter, which requires adding the package
[`OpenTelemetry.Exporter.Console`](https://www.nuget.org/packages/OpenTelemetry.Exporter.Console)
to the application.

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace DotnetMetrics;

public class Program
{
    public static void Main(string[] args)
    {
        using var meterprovider = Sdk.CreateMeterProviderBuilder()
                .AddEventCounterMetrics(options =>
                {
                   options.RefreshIntervalSecs = 5;
                })
                .AddConsoleExporter()
                .Build();
    }
}
```

Console Output:

```console

Export cpu-usage, CPU Usage, Meter: OpenTelemetry.Instrumentation.EventCounters/0.0.0.0
(2022-07-12T16:40:37.2639447Z, 2022-07-12T16:40:42.2533747Z] DoubleGauge
Value: 0

Export working-set, Working Set, Meter: OpenTelemetry.Instrumentation.EventCounters/0.0.0.0
(2022-07-12T16:40:37.2666398Z, 2022-07-12T16:40:42.2534452Z] DoubleGauge
Value: 38

Export gc-heap-size, GC Heap Size, Meter: OpenTelemetry.Instrumentation.EventCounters/0.0.0.0
(2022-07-12T16:40:37.2667389Z, 2022-07-12T16:40:42.2534456Z] DoubleGauge
Value: 7


```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
