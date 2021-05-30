# MySqlData Instrumentation for OpenTelemetry

This is an
[Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments [MySql.Data](https://www.nuget.org/packages/MySql.Data)
and collects telemetry about database operations.

## Steps to enable OpenTelemetry.Contrib.Instrumentation.MySqlData

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Contrib.Instrumentation.MySqlData`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.MySqlData)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Contrib.Instrumentation.MySqlData
```

### Step 2: Enable MySqlData Instrumentation at application startup

MySqlData instrumentation must be enabled at application startup.

The following example demonstrates adding MySqlData instrumentation to a
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
            .AddMySqlDataInstrumentation()
            .AddConsoleExporter()
            .Build();
    }
}
```

For an ASP.NET Core application, adding instrumentation is typically done in
the `ConfigureServices` of your `Startup` class. Refer to documentation for
[OpenTelemetry.Instrumentation.AspNetCore](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Instrumentation.AspNetCore/README.md).

For an ASP.NET application, adding instrumentation is typically done in the
`Global.asax.cs`. Refer to documentation for [OpenTelemetry.Instrumentation.AspNet](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Instrumentation.AspNet/README.md).

## Advanced configuration

This instrumentation can be configured to change the default behavior by using
`MySqlDataInstrumentationOptions`.

### Capturing 'db.statement'

The `MySqlDataInstrumentationOptions` class exposes several properties that can be
used to configure how the [`db.statement`](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/database.md#call-level-attributes)
attribute is captured upon execution of a query.

#### SetDbStatement

The `SetDbStatement` property can be used to control whether this instrumentation
should set the [`db.statement`](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/database.md#call-level-attributes)
attribute to the text of the `MySqlCommand` being executed.

Since `CommandType.Text` might contain sensitive data, SQL capturing is
_disabled_ by default to protect against accidentally sending full query text
to a telemetry backend. If you are only using stored procedures or have no
sensitive data in your `sqlCommand.CommandText`, you can enable SQL capturing
using the options like below:

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddMySqlDataInstrumentation(
        options => options.SetDbStatement = true)
    .AddConsoleExporter()
    .Build();
```

## EnableConnectionLevelAttributes

By default, `EnabledConnectionLevelAttributes` is disabled and this
instrumentation sets the `peer.service` attribute to the
[`DataSource`](https://docs.microsoft.com/dotnet/api/system.data.common.dbconnection.datasource)
property of the connection. If `EnabledConnectionLevelAttributes` is enabled,
the `DataSource` will be parsed and the server name will be sent as the
`net.peer.name` or `net.peer.ip` attribute, and the port will be sent as the
`net.peer.port` attribute.

The following example shows how to use `EnableConnectionLevelAttributes`.

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddMySqlDataInstrumentation(
        options => options.EnableConnectionLevelAttributes = true)
    .AddConsoleExporter()
    .Build();
```

### RecordException

This option can be set to instruct the instrumentation to record Exceptions
as Activity [events](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/exceptions.md).

> Due to the limitation of this library's implementation, We cannot get the raw `MysqlException`,
> only exception message is available.

The default value is `false` and can be changed by the code like below.

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddMySqlDataInstrumentation(
        options => options.RecordException = true)
    .AddConsoleExporter()
    .Build();
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)

* [OpenTelemetry semantic conventions for database calls](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/database.md)
