# EntityFrameworkCore Instrumentation for OpenTelemetry .NET

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.EntityFrameworkCore)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.EntityFrameworkCore)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.EntityFrameworkCore)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.EntityFrameworkCore)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments
[Microsoft.EntityFrameworkCore](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore)
and collects traces about outgoing requests.

**Note: This component is based on the OpenTelemetry semantic conventions for
[metrics](https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/metrics/semantic_conventions)
and
[traces](https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/trace/semantic_conventions).
These conventions are
[Experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/document-status.md),
and hence, this package is a
[pre-release](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/VERSIONING.md#pre-releases).
Until a [stable
version](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/telemetry-stability.md)
is released, there can be [breaking changes](./CHANGELOG.md). You can track the
progress from
[milestones](https://github.com/open-telemetry/opentelemetry-dotnet/milestone/23).**

## Steps to enable OpenTelemetry.Instrumentation.EntityFrameworkCore

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.EntityFrameworkCore`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.EntityFrameworkCore)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package --prerelease OpenTelemetry.Instrumentation.EntityFrameworkCore
```

### Step 2: Enable EntityFrameworkCore Instrumentation at application startup

`EntityFrameworkCore` instrumentation must be enabled at application startup.

The following example demonstrates adding `EntityFrameworkCore`
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
            .AddEntityFrameworkCoreInstrumentation()
            .AddConsoleExporter()
            .Build();
    }
}
```

For an ASP.NET Core application, adding instrumentation is typically done in
the `ConfigureServices` of your `Startup` class. Refer to documentation for
[OpenTelemetry.Instrumentation.AspNetCore](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Instrumentation.AspNetCore/README.md).

For an ASP.NET application, adding instrumentation is typically done in the
`Global.asax.cs`. Refer to the documentation for
[OpenTelemetry.Instrumentation.AspNet](../OpenTelemetry.Instrumentation.AspNet/README.md).

## Advanced configuration

This instrumentation can be configured to change the default behavior by using
`EntityFrameworkInstrumentationOptions`.

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddEntityFrameworkCoreInstrumentation(options =>
        {
            options.EnrichWithIDbCommand = (activity, command) =>
            {
                var stateDisplayName = $"{command.CommandType} main";
                activity.DisplayName = stateDisplayName;
                activity.SetTag("db.name", stateDisplayName);
            };
        })
        .AddConsoleExporter());
```

When used with
[`OpenTelemetry.Extensions.Hosting`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Extensions.Hosting/README.md),
all configurations to `EntityFrameworkInstrumentationOptions`
can be done in the `ConfigureServices` method of you applications `Startup`
class as shown below.

```csharp
// Configure
services.Configure<EntityFrameworkInstrumentationOptions>(options =>
{
    options.EnrichWithIDbCommand = (activity, command) =>
    {
        var stateDisplayName = $"{command.CommandType} main";
        activity.DisplayName = stateDisplayName;
        activity.SetTag("db.name", stateDisplayName);
    };
});

services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddEntityFrameworkCoreInstrumentation()
        .AddConsoleExporter());
```

### Filter

This option can be used to filter out activities based on the provider name and
the properties of the db command object being instrumented
using a `Func<string, IDbCommand, bool>`. The function receives a provider name
and an instance of the db command and should return `true`
if the telemetry is to be collected, and `false` if it should not.

The following code snippet shows how to use `Filter` to collect traces
for stored procedures only.

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddEntityFrameworkCoreInstrumentation(options =>
        {
            options.Filter = (providerName, command) =>
            {
                return command.CommandType == CommandType.StoredProcedure;
            };
        })
        .AddConsoleExporter());
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
