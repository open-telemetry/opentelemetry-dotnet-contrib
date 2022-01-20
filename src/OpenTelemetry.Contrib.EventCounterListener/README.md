# MySqlData Instrumentation for OpenTelemetry

This is an
[Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which republishes EventCounters using Metrics Api. 

## Steps to enable OpenTelemetry.Contrib.Instrumentation.EventCounterListener

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Contrib.EventCounterListener`](https://www.nuget.org/packages/OpenTelemetry.Contrib.EventCounterListener)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Contrib.EventCounterListener
```

### Step 2: Enable EventCounterListener Instrumentation at application startup

EventCounterListener instrumentation must be enabled at application startup.

The following example demonstrates adding EventCounter events to a
console application. This example also sets up the OpenTelemetry Console
exporter, which requires adding the package
[`OpenTelemetry.Exporter.Console`](https://www.nuget.org/packages/OpenTelemetry.Exporter.Console)
to the application.

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Contrib.Instrumentation.EventCounterListener;

namespace DotnetMetrics
{
    public class Program
    {

        public static void Main(string[] args)
        {
            using var meterprovider = Sdk.CreateMeterProviderBuilder()
                    .AddEventCounterListener(options =>
                    {
                        options.Sources = null;
                    })
                    .AddConsoleExporter()
                    .Build();

            System.Threading.Thread.Sleep(15000); // Give it some time to record metrics

        }
    }
}
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
