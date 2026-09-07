# OpenTelemetry .NET SDK HTTP telemetry enrichment

| Status | |
| ------ | --- |
| Stability | [Development](../../README.md#development) |
| Code Owners | [@evgenyfedorov2](https://github.com/evgenyfedorov2), [@dariusclay](https://github.com/dariusclay) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Extensions.Enrichment.Http)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Enrichment.Http)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Extensions.Enrichment.Http)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Enrichment.Http)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Extensions.Enrichment.Http)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Extensions.Enrichment.Http)

Contains OpenTelemetry .NET SDK HTTP telemetry enrichment extensions
which are used for enrichment of logs, metrics, and traces in outbound HTTP requests.

## Introduction

HTTP Telemetry enrichment attaches various types of information to traces
generated for outgoing HTTP requests.
You can use the HTTP Telemetry enrichment framework to attach any custom
information that you would like to be present in traces for outgoing HTTP requests.

With the HTTP Telemetry enrichment framework, you don't need to worry
about attaching the information carefully to each telemetry object you touch.
Instead, if you implement your enricher class inherited from `HttpClientTraceEnricher`,
it  takes care of the details automatically. You simply register your class with
the enrichment framework and the enrichment framework will make sure to call the
enrichment methods of your class for every outgoing HTTP request in your app.

## Traces

Currently this package supports trace enrichment only.

### Steps to enable OpenTelemetry.Extensions.Enrichment.Http

### Step 1: Install package

Download the `OpenTelemetry.Extensions.Enrichment.Http` package:

```shell
dotnet add package OpenTelemetry.Extensions.Enrichment.Http --prerelease
```

### Step 2: Create enricher class

Create your custom enricher class that inherits from the `HttpClientTraceEnricher`
class and override one or more of the following virtual methods, as needed:

* `EnrichWithRequest(in TraceEnrichmentBag bag, HttpRequestMessage request)`
* `EnrichWithResponse(in TraceEnrichmentBag bag, HttpResponseMessage response)`
* `EnrichWithException(in TraceEnrichmentBag bag, Exception exception)`

> [!NOTE]
> On .NET Framework targets, `EnrichWithRequest` and `EnrichWithResponse` instead
> take an `HttpWebRequest` and `HttpWebResponse` respectively.

Each method receives a `TraceEnrichmentBag`, the same lightweight `readonly struct`
used by `OpenTelemetry.Extensions.Enrichment`, which exposes a single
`Add(string key, object? value)` method for adding tags.

```csharp
internal sealed class MyHttpClientTraceEnricher : HttpClientTraceEnricher
{
    public override void EnrichWithRequest(in TraceEnrichmentBag bag, HttpRequestMessage request)
    {
        if (request.Headers.TryGetValues("x-my-custom-header", out var values))
        {
            bag.Add("http.request.header.x-my-custom-header", string.Join(",", values));
        }
    }

    public override void EnrichWithResponse(in TraceEnrichmentBag bag, HttpResponseMessage response)
    {
        bag.Add("http.response.content-length", response.Content.Headers.ContentLength);
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
`TryAddHttpClientTraceEnricher<T>()` method:

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddHttpClientInstrumentation()
    .TryAddHttpClientTraceEnricher<MyHttpClientTraceEnricher>()
    .AddConsoleExporter()
    .Build();
```

Alternatively, you can add your custom enricher to the `IServiceCollection`
directly:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.TryAddHttpClientTraceEnricher<MyHttpClientTraceEnricher>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

var app = builder.Build();

app.Run();
```

### Step 4: Usage

Once registered, the enrichment methods of your class are called automatically
for every outgoing HTTP request made through an instrumented `HttpClient` -
no additional code is required at the call site. Issue an outgoing request and
the tags added in your enricher will appear on the resulting `Activity`.
