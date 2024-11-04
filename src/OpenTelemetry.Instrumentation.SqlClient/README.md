# SqlClient Instrumentation for OpenTelemetry

| Status        |           |
| ------------- |-----------|
| Stability     |  [Beta](../../README.md#beta)|
| Code Owners   |  [@open-telemetry/dotnet-contrib-maintainers](https://github.com/orgs/open-telemetry/teams/dotnet-contrib-maintainers)|

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.SqlClient.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.SqlClient)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.SqlClient.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.SqlClient)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Instrumentation.SqlClient)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Instrumentation.SqlClient)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments
[Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient)
and
[System.Data.SqlClient](https://www.nuget.org/packages/System.Data.SqlClient)
and collects traces about database operations.

> [!WARNING]
> Instrumentation is not working with `Microsoft.Data.SqlClient` v3.* due to
the [issue](https://github.com/dotnet/SqlClient/pull/1258). It was fixed in 4.0
and later.
<!-- This comment is to make sure the two notes above and below are not merged -->
> [!CAUTION]
> This component is based on the OpenTelemetry semantic conventions for
[traces](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/database/database-spans.md).
These conventions are
[Experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/document-status.md),
and hence, this package is a [pre-release](../../VERSIONING.md#pre-releases).
Until a [stable
version](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/telemetry-stability.md)
is released, there can be breaking changes. You can track the progress from
[milestones](https://github.com/open-telemetry/opentelemetry-dotnet/milestone/23).

## Steps to enable OpenTelemetry.Instrumentation.SqlClient

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.SqlClient`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.SqlClient)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package --prerelease OpenTelemetry.Instrumentation.SqlClient
```

### Step 2: Enable SqlClient Instrumentation at application startup

SqlClient instrumentation must be enabled at application startup.

The following example demonstrates adding SqlClient instrumentation to a console
application. This example also sets up the OpenTelemetry Console exporter, which
requires adding the package
[`OpenTelemetry.Exporter.Console`](../OpenTelemetry.Exporter.Console/README.md)
to the application.

```csharp
using OpenTelemetry.Trace;

public class Program
{
    public static void Main(string[] args)
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation()
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
[OpenTelemetry.Instrumentation.AspNet](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/src/OpenTelemetry.Instrumentation.AspNet/README.md).

## Advanced configuration

This instrumentation can be configured to change the default behavior by using
`SqlClientTraceInstrumentationOptions`.

### SetDbStatementForText

Capturing the text of a database query may run the risk of capturing sensitive data.
`SetDbStatementForText` controls whether the
[`db.statement`](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/database/database-spans.md#call-level-attributes)
attribute is captured in scenarios where there could be a risk of exposing
sensitive data. The behavior of `SetDbStatementForText` depends on the runtime
used.

#### .NET

On .NET, `SetDbStatementForText` controls whether or not
this instrumentation will set the
[`db.statement`](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/database/database-spans.md#call-level-attributes)
attribute to the `CommandText` of the `SqlCommand` being executed when the `CommandType`
is `CommandType.Text`. The
[`db.statement`](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/database/database-spans.md#call-level-attributes)
attribute is always captured for `CommandType.StoredProcedure` because the `SqlCommand.CommandText`
only contains the stored procedure name.

`SetDbStatementForText` is _false_ by default. When set to `true`, the
instrumentation will set
[`db.statement`](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/database/database-spans.md#call-level-attributes)
attribute to the text of the SQL command being executed.

To enable capturing of `SqlCommand.CommandText` for `CommandType.Text` use the
following configuration.

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSqlClientInstrumentation(
        options => options.SetDbStatementForText = true)
    .AddConsoleExporter()
    .Build();
```

#### .NET Framework

On .NET Framework, there is no way to determine the type of command being
executed, so the `SetDbStatementForText` property always controls whether
or not this instrumentation will set the
[`db.statement`](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/database/database-spans.md#call-level-attributes)
attribute to the `CommandText` of the `SqlCommand` being executed. The
`CommandText` could be the name of a stored procedure (when
`CommandType.StoredProcedure` is used) or the full text of a `CommandType.Text`
query.

`SetDbStatementForText` is _false_ by default. When set to `true`, the
instrumentation will set
[`db.statement`](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/database/database-spans.md#call-level-attributes)
attribute to the text of the SQL command being executed.

To enable capturing of `SqlCommand.CommandText` for `CommandType.Text` use the
following configuration.

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSqlClientInstrumentation(
        options => options.SetDbStatementForText = true)
    .AddConsoleExporter()
    .Build();
```

> [!NOTE]
> When using the built-in `System.Data.SqlClient` only stored procedure
command names will ever be captured. When using the `Microsoft.Data.SqlClient`
NuGet package (v1.1+) stored procedure command names, full query text, and other
command text will be captured.

### EnableConnectionLevelAttributes

> [!NOTE]
> EnableConnectionLevelAttributes is supported on all runtimes.

By default, `EnabledConnectionLevelAttributes` is enabled.
When `EnabledConnectionLevelAttributes` is enabled,
the [`DataSource`](https://docs.microsoft.com/dotnet/api/system.data.common.dbconnection.datasource)
will be parsed and the server name or IP address will be sent as
the `server.address` attribute, the instance name will be sent as
the `db.mssql.instance_name` attribute, and the port will be sent as the
`server.port` attribute if it is not 1433 (the default port).

The following example shows how to use `EnableConnectionLevelAttributes`.

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSqlClientInstrumentation(
        options => options.EnableConnectionLevelAttributes = true)
    .AddConsoleExporter()
    .Build();
```

### Enrich

> [!NOTE]
> Enrich is supported on .NET and .NET Core runtimes only.

This option can be used to enrich the activity with additional information from
the raw `SqlCommand` object. The `Enrich` action is called only when
`activity.IsAllDataRequested` is `true`. It contains the activity itself (which
can be enriched), the name of the event, and the actual raw object.

Currently there is only one event name reported, "OnCustom". The actual object
is `Microsoft.Data.SqlClient.SqlCommand` for `Microsoft.Data.SqlClient` and
`System.Data.SqlClient.SqlCommand` for `System.Data.SqlClient`.

The following code snippet shows how to add additional tags using `Enrich`.

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSqlClientInstrumentation(opt => opt.Enrich
        = (activity, eventName, rawObject) =>
    {
        if (eventName.Equals("OnCustom"))
        {
            if (rawObject is SqlCommand cmd)
            {
                activity.SetTag("db.commandTimeout", cmd.CommandTimeout);
            }
        };
    })
    .Build();
```

[Processor](../../docs/trace/extending-the-sdk/README.md#processor), is the
general extensibility point to add additional properties to any activity. The
`Enrich` option is specific to this instrumentation, and is provided to get
access to `SqlCommand` object.

### RecordException

> [!NOTE]
> RecordException is supported on .NET and .NET Core runtimes only.

This option can be set to instruct the instrumentation to record SqlExceptions
as Activity
[events](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/exceptions/exceptions-spans.md).

The default value is `false` and can be changed by the code like below.

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSqlClientInstrumentation(
        options => options.RecordException = true)
    .AddConsoleExporter()
    .Build();
```

### Filter

> [!NOTE]
> Filter is supported on .NET and .NET Core runtimes only.

This option can be used to filter out activities based on the properties of the
`SqlCommand` object being instrumented using a `Func<object, bool>`. The
function receives an instance of the raw `SqlCommand` and should return `true`
if the telemetry is to be collected, and `false` if it should not. The parameter
of the Func delegate is of type `object` and needs to be cast to the appropriate
type of `SqlCommand`, either `Microsoft.Data.SqlClient.SqlCommand` or
`System.Data.SqlClient.SqlCommand`. The example below filters out all commands
that are not stored procedures.

```csharp
using var traceProvider = Sdk.CreateTracerProviderBuilder()
   .AddSqlClientInstrumentation(
       opt =>
       {
           opt.Filter = cmd =>
           {
               if (cmd is SqlCommand command)
               {
                   return command.CommandType == CommandType.StoredProcedure;
               }

               return false;
           };
       })
   .AddConsoleExporter()
   .Build();
{
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)

* [OpenTelemetry semantic conventions for database
  calls](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/database/database-spans.md)
