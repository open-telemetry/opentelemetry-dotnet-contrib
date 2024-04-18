# Instana Exporter for OpenTelemetry .NET

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.SpanProcessor.Baggage)](https://www.nuget.org/packages/OpenTelemetry.SpanProcessor.Baggage)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.SpanProcessor.Baggage)](https://www.nuget.org/packages/OpenTelemetry.SpanProcessor.Baggage)

The BaggageSpanProcessor reads entries stored in Baggage from the parent context
and adds the baggage keys and values to the activity as attributes on start.

Add this span processor to a tracer provider.

Warning!

Do not put sensitive information in Baggage.

To repeat: a consequence of adding data to Baggage is that the keys and values
will appear in all outgoing HTTP headers from the application.

## Installation

```shell
dotnet add package OpenTelemetry.SpanProcessor.Baggage
```

### Configure Processor with Trace Provider

This snippet shows how to configure the Instana Exporter for Traces

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("DemoSource")
    .AddProcessor(new BaggageSpanProcessor())
    .Build();
```

The above code must be in application startup. In case of ASP.NET Core
applications, this should be in `ConfigureServices` of `Startup` class.
For ASP.NET applications, this should be in `Global.aspx.cs`.

## Troubleshooting

Before digging into a problem, check if you hit a known issue by looking at the
[GitHub issues](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues).
