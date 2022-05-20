# Instana Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Instana.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Instana)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Instana.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Instana)

The Instana Exporter exports telemetry to Instana backand.

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.Instana
```

## Configuration

The trace exporter is supported.

To report to Instana backend correct agent key and backend URL must be configured.
These values can be configured by environment variables INSTANA_AGENT_KEY
and  INSTANA_ENDPOINT_URL.
Optionally backend communication timeout can be configured by environment
variable INSTANA_TIMEOUT.

### Enable Traces

This snippet shows how to configure the Instana Exporter for Traces

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("DemoSource")
    .AddInstanaExporter()
    .Build();
```

The above code must be in application startup. In case of ASP.NET Core
applications, this should be in `ConfigureServices` of `Startup` class.
For ASP.NET applications, this should be in `Global.aspx.cs`.

## Troubleshooting

Before digging into a problem, check if you hit a known issue by looking at the [GitHub
issues](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues).
