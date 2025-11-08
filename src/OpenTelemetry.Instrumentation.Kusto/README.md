# Kusto Instrumentation for OpenTelemetry

| Status      |           |
| ----------- | --------- |
| Stability   | [Experimental](../../README.md#experimental) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.Kusto)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Kusto)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.Kusto)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Kusto)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Instrumentation.Kusto)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Instrumentation.Kusto)

This is an
[Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments Azure Data Explorer (Kusto) client libraries
and collects telemetry about Kusto operations.

## Steps to enable OpenTelemetry.Instrumentation.Kusto

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.Kusto`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Kusto)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Instrumentation.Kusto
```

### Step 2: Enable Kusto Instrumentation at application startup

Kusto instrumentation must be enabled at application startup.

The following example demonstrates adding Kusto instrumentation to a
console application. This example also sets up the OpenTelemetry Console
exporter, which requires adding the package
[`OpenTelemetry.Exporter.Console`](https://www.nuget.org/packages/OpenTelemetry.Exporter.Console)
to the application.

```csharp
using OpenTelemetry.Trace;

public class Program
{
    public static void Main(string[] args)
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddKustoInstrumentation()
            .AddConsoleExporter()
            .Build();
    }
}
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Azure Data Explorer (Kusto)](https://docs.microsoft.com/azure/data-explorer/)
