# OpenSearch Client Instrumentation for OpenTelemetry .NET

| Status      |                              |
| ----------- |------------------------------|
| Stability   | [Beta](../../README.md#beta) |
| Code Owners | [@open-telemetry/dotnet-contrib-maintainers](https://github.com/orgs/open-telemetry/teams/dotnet-contrib-maintainers) |

## OpenSearch.Net/OpenSearch.Client

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.OpenSearchClient)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.OpenSearchClient)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.OpenSearchClient)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.OpenSearchClient)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Instrumentation.OpenSearchClient)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Instrumentation.OpenSearchClient)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments [OpenSearch.Client](https://www.nuget.org/packages/OpenSearch.Client) and [OpenSearch.Net](https://www.nuget.org/packages/OpenSearch.Net)
and collects traces about outgoing requests.

> [!NOTE]
> This component is based on the OpenTelemetry semantic conventions for
[metrics](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/database/elasticsearch.md)
and
[traces](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/database/elasticsearch.md).
These conventions are
[Experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/document-status.md),
and hence, this package is a
[pre-release](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/VERSIONING.md#pre-releases).
Until a [stable
version](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/telemetry-stability.md)
is released, there can be [breaking changes](./CHANGELOG.md).

## Steps to enable OpenTelemetry.Instrumentation.OpenSearchClient

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.OpenSearchClient`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.OpenSearchClient)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package --prerelease OpenTelemetry.Instrumentation.OpenSearchClient
```

### Step 2: Enable OpenSearch.Client Instrumentation at application startup

`OpenSearch.Client` instrumentation must be enabled at application startup.

The following example demonstrates adding `OpenSearch.Client`
instrumentation to a console application. This example also sets up the
OpenTelemetry Console exporter, which requires adding the package
[`OpenTelemetry.Exporter.Console`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Console/README.md)
to the application.

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

public class Program
{
    public static void Main(string[] args)
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddOpenSearchClientInstrumentation()
            .AddConsoleExporter()
            .Build();
    }
}
```

For an ASP.NET Core application, adding instrumentation is typically done in the
`ConfigureServices` of your `Startup` class. Refer to documentation for
[OpenTelemetry.Instrumentation.AspNetCore](../OpenTelemetry.Instrumentation.AspNetCore/README.md).

For an ASP.NET application, adding instrumentation is typically done in the
`Global.asax.cs`. Refer to the documentation for
[OpenTelemetry.Instrumentation.AspNet](../OpenTelemetry.Instrumentation.AspNet/README.md).

## Advanced configuration

This instrumentation can be configured to change the default behavior by using
`OpenSearchClientInstrumentationOptions`.

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddOpenSearchClientInstrumentation(options =>
        {
            // add request json as db.statement attribute tag
            options.SetDbStatementForRequest = true;
        })
        .AddConsoleExporter());
```

When used with
[`OpenTelemetry.Extensions.Hosting`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Extensions.Hosting/README.md),
all configurations to `OpenSearchClientInstrumentationOptions`
can be done in the `ConfigureServices` method of you applications `Startup`
class as shown below.

```csharp
// Configure
services.Configure<OpenSearchClientInstrumentationOptions>(options =>
{
    // add request json as db.statement attribute tag
    options.SetDbStatementForRequest = true;
});

services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddOpenSearchClientInstrumentation()
        .AddConsoleExporter());
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [OpenSearch](https://opensearch.org/)
* [OpenSearch.Client](https://www.nuget.org/packages/OpenSearch.Client)
* [OpenSearch.Net](https://www.nuget.org/packages/OpenSearch.Net)
