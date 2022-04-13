# Elasticsearch Client Instrumentation for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.ElasticsearchClient.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.ElasticsearchClient)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.ElasticsearchClient.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.ElasticsearchClient)

Automatically instruments events emitted by the NEST/Elasticsearch.Net client library.

## Installation

```shell
dotnet add package OpenTelemetry.Instrumentation.ElasticsearchClient
```

## Configuration

ASP.NET Core instrumentation example:

```csharp
// Add OpenTelemetry and Elasticsearch client instrumentation
services.AddOpenTelemetryTracing(x =>
{
    x.AddElasticsearchClientInstrumentation();
    x.UseJaegerExporter(config => {
      // Configure Jaeger
    });
});
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Elasticsearch](https://www.elastic.co/)
* [NEST Client](https://www.nuget.org/packages/NEST/)
* [Elasticsearch.Net Client](https://www.nuget.org/packages/Elasticsearch.Net/)
