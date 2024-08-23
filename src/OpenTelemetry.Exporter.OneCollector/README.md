# OneCollector Exporter for OpenTelemetry .NET

| Status        |           |
| ------------- |-----------|
| Stability     |  [Stable](../../README.md#stable)|
| Code Owners   |  [@codeblanch](https://github.com/codeblanch), [@reyang](https://github.com/reyang)|

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.OneCollector)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OneCollector)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.OneCollector)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OneCollector)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Exporter.OneCollector)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Exporter.OneCollector)

The OneCollectorExporter is designed for Microsoft products to send data to
public-facing end-points which route to Microsoft's internal data pipeline. It
is not meant to be used outside of Microsoft products and is open sourced to
demonstrate best practices and to be transparent about what is being collected.

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.OneCollector
```

## Basic usage

```csharp
using var logFactory = LoggerFactory.Create(builder => builder
    .AddOpenTelemetry(builder =>
    {
        builder.IncludeScopes = true;
        builder.AddOneCollectorExporter("InstrumentationKey=instrumentation-key-here");
    }));

var logger = logFactory.CreateLogger<MyService>();

using var scope = logger.BeginScope("{requestContext}", Guid.NewGuid());

logger.LogInformation("Request received {requestId}!", 1);
logger.LogWarning("Warning encountered {error_code}!", 0xBAADBEEF);
```

## Table Mapping

When TableMapping is enabled the default table name used for logs is `Log`. 
Mappings can be specified universally or for individual log message [category](https://docs.microsoft.com/dotnet/core/extensions/logging#log-category) values.

* To change the default table name for Logs add an entry with the key `*` and set the value to the desired custom table name.

* To change the table name for a specific log [category](https://docs.microsoft.com/dotnet/core/extensions/logging#log-category) add an entry with the key set to the full "category" of the log messages or a prefix that will match the starting portion of the log message "category". Set the value of the key to either the desired custom table name

  For example, given the configuration...

  ```csharp
    using var logFactory = LoggerFactory.Create(builder => builder
        .AddOpenTelemetry(builder =>
        {
            builder.IncludeScopes = true;
            builder.AddOneCollectorExporter("InstrumentationKey=instrumentation-key-here", exporterBuilder => {
                exporterBuilder.ConfigureTableMappingOptions(tableMappingOptions =>
                    {
                        tableMappingOptions.UseTableMapping = true;
                        tableMappingOptions.TableMappings = TableMappings = new Dictionary<string, string>()
                        {
                            ["*"] = "DefaultLogs",
                            ["MyCompany"] = "InternalLogs",
                            ["MyCompany.Product1"] = "InternalProduct1Logs",
                            ["MyCompany.Product2"] = "InternalProduct2Logs",
                            ["MyCompany.Product2.Security"] = "InternalSecurityLogs",
                        },
                    });
            });
        }));

  ```

  ...log category mapping would be performed as such:

  * `ILogger<ThirdParty.Thing>`: This would go to "DefaultLogs"
  * `ILogger<MyCompany.ProductX.Thing>`: This would go to "InternalLogs"
  * `ILogger<MyCompany.Product1.Thing>`: This would go to "InternalProduct1Logs"
  * `ILogger<MyCompany.Product2.Thing>`: This would go to "InternalProduct2Logs"
  * `ILogger<MyCompany.Product2.Security.Alert>`: This would go to
    "InternalSecurityLogs"