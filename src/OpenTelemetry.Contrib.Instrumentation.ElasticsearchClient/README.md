# Elasticsearch Client Instrumentation for OpenTelemetry .NET

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
