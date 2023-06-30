# Hangfire Instrumentation for OpenTelemetry .NET

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.Hangfire)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Hangfire)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.Hangfire)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Hangfire)

This is an
[Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments
[Hangfire](https://www.nuget.org/packages/Hangfire/)
and collects telemetry about BackgroundJob.

## Steps to enable OpenTelemetry.Instrumentation.Hangfire

### Step 1: Install and configure Hangfire

[Getting Started](https://docs.hangfire.io/en/latest/getting-started/index.html)

### Step 2: Install Hangfire instrumentation Package

Add a reference to the
[`OpenTelemetry.Instrumentation.Hangfire`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Hangfire)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Instrumentation.Hangfire --prerelease
```

### Step 3: Enable Hangfire Instrumentation at application startup

Hangfire instrumentation must be enabled at application startup.

The following example demonstrates adding Hangfire instrumentation to a
console application. This example also sets up the OpenTelemetry Console
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
            .AddHangfireInstrumentation()
            .AddConsoleExporter()
            .Build();
    }
}
```

For an ASP.NET Core application, adding instrumentation is typically done in
the `ConfigureServices` of your `Startup` class. Refer to [example](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/examples/AspNetCore/Program.cs).

For an ASP.NET application, adding instrumentation is typically done in the
`Global.asax.cs`. Refer to [example](../../examples/AspNet/Global.asax.cs).

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Hangfire Project](https://www.hangfire.io/)
