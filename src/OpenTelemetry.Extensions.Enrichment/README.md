# OpenTelemetry .NET SDK telemetry enrichment framework

[![nuget](https://img.shields.io/nuget/v/OpenTelemetry.Extensions.Enrichment.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Enrichment)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Extensions.Enrichment.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Enrichment)

Contains OpenTelemetry .NET SDK telemetry enrichment framework
which is used for enrichment of traces.

## Introduction

Telemetry enrichment attaches various types of information to traces.
You can use the telemetry enrichment framework to attach any custom information that you would
like to be present in all of your traces.

With telemetry enrichment framework, you don't need to worry about attaching
the information carefully to each telemetry object you touch.
Instead, if you implement your enricher class, it  takes care of the details
automatically. You simply register your class with the enrichment framework
and the enrichment framework will make sure to call the `Enrich()` method of your
class every time there is an `Activity` in your app.

## Traces

Currently this package supports trace enrichment only.

### Steps to enable OpenTelemetry.Extensions.Enrichment

You can view an example project using Enrichment at
`/examples/enrichment/Examples.Enrichment`.

### Step 1: Install package

Download the `OpenTelemetry.Extensions.Enrichment` package:

```shell
dotnet add package OpenTelemetry.Extensions.Enrichment --prerelease
```

### Step 2: Create enricher class

Create your custom enricher class that inherits from the `TraceEnricher` class
and override the `public override void Enrich(TraceEnrichmentBag bag)` method:

```csharp
public class MyTraceEnricher : TraceEnricher
{
    public override void Enrich(TraceEnrichmentBag bag)
    {
        bag.Add("my key", "my value");
    }
}
```

For every `Activity`, the `public override void Enrich(TraceEnrichmentBag bag)`
method is guaranteed to be called exactly once. Semantically,
for the example above it means that a new [tag object](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activity.tagobjects?view=net-7.0)
with the `my key` key and the `my value` value will be added to every `Activity`
in your application.

### Step 3: Register enricher class

Add your custom enricher class to the `TracerProviderBuilder` by calling the
`AddTraceEnricher<T>()` method. Configure `ActivitySource` and an exporter as usual:

```csharp
using var MyActivitySource = new ActivitySource("MyCompany.MyProduct.MyLibrary");
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("MyCompany.MyProduct.MyLibrary")
    .AddTraceEnricher<MyTraceEnricher>()
    .AddConsoleExporter()
    .Build();
```

> **Note**
> The `AddTraceEnricher()` method call should be done *before* registering exporter
related Activity processors.

### Step 4: Usage

Create an `Activity` and add tags as usual:

```csharp
using var activity = myActivitySource.StartActivity("SayHello");
activity?.SetTag("hello", "world");
```

Run your application and verify that the `my key` tag is added to `Activity`:

```shell
Activity.TraceId:            0e1dc24e2e63796bfc8186e24f916f5f
Activity.SpanId:             bfcbf9d5746009d6
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: MyCompany.MyProduct.MyLibrary
Activity.DisplayName:        SayHello
Activity.Kind:               Internal
Activity.StartTime:          2023-03-20T09:39:21.9642338Z
Activity.Duration:           00:00:00.0016887
Activity.Tags:
    hello: world
    my key: my value
Resource associated with Activity:
    service.name: unknown_service:Examples.Enrichment
```

### Extension methods

There are a bunch of extension methods available for registering your custom trace
enricher class. The methods that utilize `IServiceCollection` are tailored for
Dependency Injection-based situations, allowing for registration of
`TracerProviderBuilder` and `IServiceCollection` at different locations
within your codebase. Conversely, the methods relying on `TracerProviderBuilder`
are meant to be employed in non-DI scenarios.

In case you would like to use a comprehensive enricher class that may require
injection or interaction with other classes, you may utilize either of these
two methods. Both methods take a type T parameter to specify the type of your
enricher class. In this case, the enricher class will be created for you by
Dependency Injection:

```csharp
public static AddTraceEnricher<T>(this IServiceCollection services)
public static AddTraceEnricher<T>(this TracerProviderBuilder builder)
```

If you prefer to instantiate your enricher class on your own, you may use one of
these methods which allow for the usage of pre-existing enricher objects:

```csharp
public static AddTraceEnricher(this IServiceCollection services, TraceEnricher enricher)
public static AddTraceEnricher(this TracerProviderBuilder builder, TraceEnricher enricher)
```

If you only need to enrich a small amount of data, it may not be necessary to create
an enricher class. Instead, you can make use of the following methods which accept an
`Action<TraceEnrichmentBag>` delegate:

```csharp
public static AddTraceEnricher(this IServiceCollection services, Action<TraceEnrichmentBag> enrichmentAction)
public static AddTraceEnricher(this TracerProviderBuilder builder, Action<TraceEnrichmentBag> enrichmentAction)
```

If you would rather use a factory method to instantiate your enricher class,
with the possibility of interacting with `IServiceProvider,` you can utilize one
of these two methods:

```csharp
public static AddTraceEnricher(this IServiceCollection services, Func<IServiceProvider, TraceEnricher> enricherImplementationFactory)
public static AddTraceEnricher(this TracerProviderBuilder builder, Func<IServiceProvider, TraceEnricher> enricherImplementationFactory)
```

## Recommendations and best practices

You can add any number of custom enrichers, but it is advisable to only include
properties that are truly beneficial to prevent an excessive increase in the
number of dimensions associated with each `Activity`.
