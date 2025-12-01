# Kusto Instrumentation for OpenTelemetry

| Status      |           |
| ----------- | --------- |
| Stability   | [Alpha](../../README.md#alpha) |

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

#### Traces

The following example demonstrates adding Kusto traces instrumentation
to a console application. This example also sets up the OpenTelemetry Console
exporter, which requires adding the package
[`OpenTelemetry.Exporter.Console`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Console/README.md)
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

#### Metrics

The following example demonstrates adding Kusto metrics instrumentation
to a console application. This example also sets up the OpenTelemetry Console
exporter, which requires adding the package
[`OpenTelemetry.Exporter.Console`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Console/README.md)
to the application.

```csharp
using OpenTelemetry.Metrics;

public class Program
{
    public static void Main(string[] args)
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddKustoInstrumentation()
            .AddConsoleExporter()
            .Build();
    }
}
```

## Advanced configuration

This instrumentation can be configured to change the default behavior by using
`KustoInstrumentationOptions`.

### RecordQueryText

This option can be set to instruct the instrumentation to record the sanitized
query text as an attribute on the activity. Query text is
sanitized to remove literal values and replace them with a placeholder character.

The default value is `false` and can be changed by the code like below.

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddKustoInstrumentation(
        options => options.RecordQueryText = true)
    .AddConsoleExporter()
    .Build();
```

### RecordQuerySummary

This option can be set to instruct the instrumentation to record a query
summary as an attribute on the activity. The query summary
is automatically generated from the query text and contains the operation type
and relevant object names.

The default value is `true` and can be changed by the code like below.

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddKustoInstrumentation(
        options => options.RecordQuerySummary = false)
    .AddConsoleExporter()
    .Build();
```

### Enrich

This option can be used to enrich the activity with additional information from
the raw `TraceRecord` object. The `Enrich` action is called only when
`activity.IsAllDataRequested` is `true`. It contains the activity itself (which
can be enriched) and the actual `TraceRecord` from the Kusto client library.

The following code snippet shows how to add additional tags using `Enrich`.

```csharp
using OpenTelemetry.Instrumentation.Kusto.Implementation;
using KustoUtils = Kusto.Cloud.Platform.Utils;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddKustoInstrumentation(opt => opt.Enrich = (activity, record) =>
    {
        // Add custom tags based on the TraceRecord
        activity.SetTag("kusto.activity_id", record.Activity.ActivityId);
        activity.SetTag("kusto.activity_type", record.Activity.ActivityType);
    })
    .Build();
```

[Processor](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/docs/trace/extending-the-sdk/README.md#processor),
is the general extensibility point to add additional properties to any activity.
The `Enrich` option is specific to this instrumentation, and is provided to get
access to the `TraceRecord` object.

#### Custom Query Summarization

The `Enrich` callback can be used to implement custom query summarization logic.
For example, you can extract summary information from query comments:

```csharp
using OpenTelemetry.Instrumentation.Kusto.Implementation;
using KustoUtils = Kusto.Cloud.Platform.Utils;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddKustoInstrumentation(opt =>
    {
        // Disable automatic summarization
        opt.RecordQuerySummary = false;

        // Extract custom summary from query comments
        opt.Enrich = (activity, record) =>
        {
            const string key = "// otel-custom-summary=";
            var message = record.Message.AsSpan();
            var begin = message.IndexOf(key, StringComparison.Ordinal);

            if (begin < 0)
            {
                return;
            }

            var summary = message.Slice(begin + key.Length);
            var end = summary.IndexOfAny('\r', '\n');
            if (end < 0)
            {
                end = summary.Length;
            }

            summary = summary.Slice(0, end).Trim();
            var summaryString = summary.ToString();

            activity.SetTag(SemanticConventions.AttributeDbQuerySummary, summaryString);
            activity.DisplayName = summaryString;
        };
    })
    .Build();
```

With this configuration, a query like:

```kql
// otel-custom-summary=Get active users
Users
| where IsActive == true
| take 100
```

Would result in an activity with the summary set to `"Get active users"`
and the activity display name set to the same value.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Azure Data Explorer (Kusto)](https://docs.microsoft.com/azure/data-explorer/)
* [OpenTelemetry semantic conventions for database
  calls](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/database/database-spans.md)
