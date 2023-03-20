# OpenTelemetry .NET SDK telemetry enrichment framework

[![nuget](https://img.shields.io/nuget/v/OpenTelemetry.Extensions.Enrichment.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Enrichment)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Extensions.Enrichment.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Enrichment)

Contains OpenTelemetry .NET SDK telemetry enrichment framework
which is used for enrichment of traces.

## Introduction

Telemetry enrichment attaches various types of information to traces.
Examples of the types of information that can be attached are any
dynamic or static information that is available in your application
or just any custom information which you would like every of your traces
to have and so on.

With the enrichment framework, you don't need to worry about attaching
the information carefully to each telemetry object you touch.
Instead, if you implement your enricher class, it  takes care of the details
automatically. You simply register your class with the enrichment framework
and the enrichment framework will make sure to call the `Enrich()` method of your
class every time there is an `Activity` in your app.

## Traces

Currently this package supports trace enrichment only.

### Installation

Download the `OpenTelemetry.Extensions.Enrichment` package:

```shell
dotnet add package OpenTelemetry.Extensions.Enrichment --prerelease
```

### Usage

Add your custom trace enricher:

```csharp
using OpenTelemetry.Extensions.Enrichment;
using OpenTelemetry.Trace;
using Microsoft.Extensions.DependencyInjection;

public void ConfigureServices(IServiceCollection services)
{
    services.AddControllers();
    services.AddOpenTelemetry().WithTracing((builder) => builder
        .AddTraceEnricher<MyTraceEnricher>());
}
```

```csharp
public class MyTraceEnricher : TraceEnricher
{
    public override void Enrich(TraceEnrichmentBag bag)
    {
        bag.Add("my key", "my value");
    }
}
```

> **Note**
> The `AddTraceEnricher()` method call should be done *before* registering exporter
related Activity processors.

For every `Activity`, the `public override void Enrich(TraceEnrichmentBag bag)`
method is guaranteed to be called exactly once. Semantically,
for the example above it means that a new [tag object](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activity.tagobjects?view=net-7.0)
with the `my key` key and the `my value` value will be added to every `Activity`
in your application.

### Extension methods

The following extension methods are available for registering your custom trace
enricher class:

```csharp
public static AddTraceEnricher(this IServiceCollection services, TraceEnricher enricher)
public static AddTraceEnricher(this IServiceCollection services, Action<TraceEnrichmentBag> enrichmentAction)
public static AddTraceEnricher(this IServiceCollection services, Func<IServiceProvider, TraceEnricher> enricherImplementationFactory)
public static AddTraceEnricher<T>(this IServiceCollection services)

public static AddTraceEnricher(this TracerProviderBuilder builder, TraceEnricher enricher)
public static AddTraceEnricher(this TracerProviderBuilder builder, Action<TraceEnrichmentBag> enrichmentAction)
public static AddTraceEnricher(this TracerProviderBuilder builder, Func<IServiceProvider, TraceEnricher> enricherImplementationFactory)
public static AddTraceEnricher<T>(this TracerProviderBuilder builder)
```

## Recommendation and best practices

There is no limit to the number of custom enrichers that you can add.
However, only add the properties that are really useful to avoid exploding
the number of dimensions that are included with each `Activity`.
