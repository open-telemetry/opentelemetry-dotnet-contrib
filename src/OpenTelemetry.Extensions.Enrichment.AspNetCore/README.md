# OpenTelemetry .NET SDK ASP.NET Core telemetry enrichment

| Status | |
| ------ | --- |
| Stability | [Development](../../README.md#development) |
| Code Owners | [@evgenyfedorov2](https://github.com/evgenyfedorov2), [@dariusclay](https://github.com/dariusclay) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Extensions.Enrichment.AspNetCore)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Enrichment.AspNetCore)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Extensions.Enrichment.AspNetCore)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Enrichment.AspNetCore)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Extensions.Enrichment.AspNetCore)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Extensions.Enrichment.AspNetCore)

Contains OpenTelemetry .NET SDK ASP.NET Core telemetry enrichment extensions
which are used for enrichment of logs, metrics, and traces in inbound HTTP requests.

## Introduction

ASP.NET Core Telemetry enrichment attaches various types of information to traces
generated for incoming HTTP requests.
You can use the ASP.NET Core Telemetry enrichment framework to attach any custom
information that you would like to be present in traces for incoming HTTP requests.

With the ASP.NET Core Telemetry enrichment framework, you don't need to worry
about attaching the information carefully to each telemetry object you touch.
Instead, if you implement your enricher class inherited from `AspNetCoreTraceEnricher`,
it  takes care of the details automatically. You simply register your class with
the enrichment framework and the enrichment framework will make sure to call the
enrichment methods of your class for every incoming HTTP request in your app.

## Traces

Currently this package supports trace enrichment only.

### Steps to enable OpenTelemetry.Extensions.Enrichment.AspNetCore

### Step 1: Install package

Download the `OpenTelemetry.Extensions.Enrichment.AspNetCore` package:

```shell
dotnet add package OpenTelemetry.Extensions.Enrichment.AspNetCore --prerelease
```

### Step 2: Create enricher class

Create your custom enricher class that inherits from the `AspNetCoreTraceEnricher`
class and override one or more of the following virtual methods, as needed:

* `EnrichWithHttpRequest(in TraceEnrichmentBag bag, HttpRequest request)`
* `EnrichWithHttpResponse(in TraceEnrichmentBag bag, HttpResponse response)`
* `EnrichWithException(in TraceEnrichmentBag bag, Exception exception)`

Each method receives a `TraceEnrichmentBag`, the same lightweight `readonly struct`
used by `OpenTelemetry.Extensions.Enrichment`, which exposes a single
`Add(string key, object? value)` method for adding tags.

```csharp
internal sealed class MyAspNetCoreTraceEnricher : AspNetCoreTraceEnricher
{
    public override void EnrichWithHttpRequest(in TraceEnrichmentBag bag, HttpRequest request)
    {
        bag.Add("http.request.header.x-my-custom-header", request.Headers["x-my-custom-header"].ToString());
    }

    public override void EnrichWithHttpResponse(in TraceEnrichmentBag bag, HttpResponse response)
    {
        bag.Add("http.response.content-length", response.ContentLength);
    }

    public override void EnrichWithException(in TraceEnrichmentBag bag, Exception exception)
    {
        bag.Add("exception.source", exception.Source);
    }
}
```

Optionally, inject other services your enricher class depends on via its constructor,
the same way you would with `TraceEnricher` (see the
[OpenTelemetry.Extensions.Enrichment README](../OpenTelemetry.Extensions.Enrichment/README.md#step-2-create-enricher-class)
for an example).

### Step 3: Register enricher class

Add your custom enricher class to the `TracerProviderBuilder` by calling the
`TryAddAspNetCoreTraceEnricher<T>()` method:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .TryAddAspNetCoreTraceEnricher<MyAspNetCoreTraceEnricher>()
        .AddConsoleExporter());

var app = builder.Build();

app.MapGet("/", () => "Hello OpenTelemetry!");

app.Run();
```

Alternatively, you can add your custom enricher to the `IServiceCollection`
directly:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.TryAddAspNetCoreTraceEnricher<MyAspNetCoreTraceEnricher>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());

var app = builder.Build();

app.MapGet("/", () => "Hello OpenTelemetry!");

app.Run();
```

> [!NOTE]
> The `TryAddAspNetCoreTraceEnricher()` call should be done *before* exporter
> related Activity processors are added.

### Step 4: Usage

Once registered, the enrichment methods of your class are called automatically
for every incoming HTTP request handled by ASP.NET Core - no additional code is
required at the request-handling site. Run your application and issue a request;
the tags added in your enricher will appear on the resulting `Activity`.
