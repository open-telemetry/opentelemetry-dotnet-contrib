# EventCounters Instrumentation for OpenTelemetry .NET

| Status      |                                                                                  |
| ----------- | -------------------------------------------------------------------------------- |
| Stability   | [Alpha](../../README.md#alpha)                                                   |
| Code Owners | [@hananiel](https://github.com/hananiel), [@mic-max](https://github.com/mic-max) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.EventCounters)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.EventCounters)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.EventCounters)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.EventCounters)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Instrumentation.EventCounters)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Instrumentation.EventCounters)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library)
, which republishes
[EventCounters](https://learn.microsoft.com/dotnet/core/diagnostics/event-counters)
using OpenTelemetry Metrics API.

## Steps to enable OpenTelemetry.Instrumentation.EventCounters

You can view an example project using EventCounters at
`/examples/event-counters/Examples.EventCounters`.

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.EventCounters`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.EventCounters)
package.

```shell
dotnet add package OpenTelemetry.Instrumentation.EventCounters --prerelease
```

### Step 2: Enable EventCounters Instrumentation

#### Using Direct Configuration

EventCounters instrumentation should be enabled at application startup using the
`AddEventCountersInstrumentation` extension on the `MeterProviderBuilder`:

```csharp
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddEventCountersInstrumentation(options => {
        options.RefreshIntervalSecs = 1;
        options.AddEventSources("MyEventSource");
    })
    .AddPrometheusHttpListener()
    .Build();
```

Additionally, the above snippet sets up the OpenTelemetry Prometheus exporter, which
requires adding the package
[`OpenTelemetry.Exporter.Prometheus`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Prometheus.HttpListener/README.md)
to the application.

#### Using Dependency Injection (DI)

To enable EventCounters instrumentation via DI, configure it as follows:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddOpenTelemetry().WithMetrics(builder =>
    {
        builder.AddEventCountersInstrumentation(options =>
        {
            options.RefreshIntervalSecs = 1;
            options.AddEventSources("MyEventSource");
        })
        .AddConsoleExporter();
    });
}
```

This method allows providing options via `IConfigurationSection`
and integrates seamlessly with DI-based setups.

Alternatively, you can configure the EventCounters instrumentation using settings
from an IConfigurationSection. This allows you to integrate configuration with
your app's settings (e.g., appsettings.json or environment variables).

Example using IConfigurationSection:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddOpenTelemetryMetrics((builder, config) =>
    {
        var eventCountersSection = config.GetSection("EventCountersOptions");
        builder.AddEventCountersInstrumentation(eventCountersSection);
    });
}
```

This method allows seamless integration with DI-based setups and leverages
configuration files or environment variables:

| Parameter                                         | Type                 | Description                                        | Example                         |
| ------------------------------------------------- | -------------------- | -------------------------------------------------- | ------------------------------- |
| `OTEL_DOTNET_EVENTCOUNTERS_REFRESH_INTERVAL_SECS` | Number               | Specifies the refresh interval for event counters. | 1                               |
| `OTEL_DOTNET_EVENTCOUNTERS_SOURCES`               | Comma-separated list | Defines the sources for event counters.            | "MyEventSource1,MyEventSource2" |

### Step 3: Create EventCounters

Learn about [EventCounters in
.NET](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/event-counters) .

```csharp
EventSource eventSource = new("MyEventSource");

EventCounter eventCounter = new("MyEventCounterName", eventSource);
eventCounter.WriteMetric(0);
eventCounter.WriteMetric(1000);

PollingCounter pollingCounter = new("MyPollingCounterName", eventSource, () => new Random().NextDouble());
```

## Notes

The metrics will only be available after `EventCounterIntervalSec` seconds.
Before that nothing will be exported, if anything is present at the Prometheus
metrics endpoint it is from a prior execution. This is more evident when using
longer polling intervals.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
