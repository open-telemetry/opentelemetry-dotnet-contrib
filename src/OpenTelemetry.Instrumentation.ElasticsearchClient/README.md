# Elasticsearch Client Instrumentation for OpenTelemetry .NET

## NEST/Elasticsearch.Net

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.ElasticsearchClient.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.ElasticsearchClient)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.ElasticsearchClient.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.ElasticsearchClient)

Automatically instruments events emitted by the [NEST/Elasticsearch.Net](https://www.nuget.org/packages/NEST)
client library.

### Installation

```shell
dotnet add package OpenTelemetry.Instrumentation.ElasticsearchClient
```

### Configuration

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

## Elastic.Clients.Elasticsearch

[Elastic.Clients.Elasticsearch](https://www.nuget.org/packages/Elastic.Clients.Elasticsearch),
that deprecates `NEST/Elasticsearch.Net`,
brings native support for OpenTelemetry. To instrument it you need
to configure the OpenTelemetry SDK to listen to the `ActivitySource`
used by the library by calling `AddSource("Elastic.Clients.Elasticsearch.ElasticsearchClient")`
on the `TracerProviderBuilder`.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Elasticsearch](https://www.elastic.co/)
* [NEST Client](https://www.nuget.org/packages/NEST/)
* [Elasticsearch.Net Client](https://www.nuget.org/packages/Elasticsearch.Net/)
* [Elastic.Clients.Elasticsearch](https://www.nuget.org/packages/Elastic.Clients.Elasticsearch/)
