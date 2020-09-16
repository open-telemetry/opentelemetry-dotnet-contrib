# Elasticsearch Client Instrumentation for OpenTelemetry .NET

Automatically instruments
[DiagnosticSource](https://www.elastic.co/guide/en/elasticsearch/client/net-api/current/diagnostic-source.html)
events emitted by [Elasticsearch.Net](https://www.elastic.co/guide/en/elasticsearch/client/net-api/current/index.html) library.

## Installation

```shell
dotnet add package OpenTelemetry.Contrib.Instrumentation.ElasticsearchClient
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
* [Elasticsearch.Net Client](https://www.elastic.co/guide/en/elasticsearch/client/net-api/current/index.html)
