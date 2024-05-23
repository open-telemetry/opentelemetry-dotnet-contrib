# ASP.NET Instrumentation for OpenTelemetry

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.AspNet)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNet)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.AspNet)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNet)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Instrumentation.AspNet)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Instrumentation.AspNet)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments [ASP.NET](https://docs.microsoft.com/aspnet/overview) and
collect metrics and traces about incoming web requests.

> [!NOTE]
> This component is based on the OpenTelemetry semantic conventions for
[metrics](https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/metrics/semantic_conventions)
and
[traces](https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/trace/semantic_conventions).
These conventions are
[Experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/document-status.md),
and hence, this package is a [pre-release](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/VERSIONING.md#pre-releases).
Until a [stable
version](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/telemetry-stability.md)
is released, there can be breaking changes. You can track the progress from
[milestones](https://github.com/open-telemetry/opentelemetry-dotnet/milestone/23).

## Steps to enable OpenTelemetry.Instrumentation.AspNet

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.AspNet`](https://www.nuget.org/packages/opentelemetry.instrumentation.aspnet)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Instrumentation.AspNet
```

### Step 2: Modify Web.config

`OpenTelemetry.Instrumentation.AspNet` requires adding an additional HttpModule
to your web server. This additional HttpModule is shipped as part of
[`OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule/)
which is implicitly brought by `OpenTelemetry.Instrumentation.AspNet`. The
following shows changes required to your `Web.config` when using IIS web server.

```xml
<system.webServer>
    <modules>
        <add
            name="TelemetryHttpModule"
            type="OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule,
                OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"
            preCondition="integratedMode,managedHandler" />
    </modules>
</system.webServer>
```

### Step 3: Enable ASP.NET Instrumentation at application startup

ASP.NET instrumentation must be enabled at application startup. This is
typically done in the `Global.asax.cs`.

#### Traces

The following example demonstrates adding ASP.NET instrumentation with the
extension method `.AddAspNetInstrumentation()` on `TracerProviderBuilder` to
an application. This example also sets up
the OTLP (OpenTelemetry Protocol) exporter, which requires adding the package
[`OpenTelemetry.Exporter.OpenTelemetryProtocol`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
to the application.

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

public class WebApiApplication : HttpApplication
{
    private TracerProvider tracerProvider;
    protected void Application_Start()
    {
        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAspNetInstrumentation()
            .AddOtlpExporter()
            .Build();
    }
    protected void Application_End()
    {
        this.tracerProvider?.Dispose();
    }
}
```

#### Metrics

The following example demonstrates adding ASP.NET instrumentation with the
extension method `.AddAspNetInstrumentation()` on `MeterProviderBuilder` to
an application. This example also sets up
the OTLP (OpenTelemetry Protocol) exporter, which requires adding the package
[`OpenTelemetry.Exporter.OpenTelemetryProtocol`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
to the application.

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;

public class WebApiApplication : HttpApplication
{
    private MeterProvider meterProvider;
    protected void Application_Start()
    {
        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddAspNetInstrumentation()
            .AddOtlpExporter()
            .Build();
    }
    protected void Application_End()
    {
        this.meterProvider?.Dispose();
    }
}
```

#### List of metrics produced

The instrumentation is implemented based on [metrics semantic
conventions](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/semantic_conventions/http-metrics.md#metric-httpserverduration).
Currently, the instrumentation supports the following metric.

| Name  | Instrument Type | Unit | Description |
|-------|-----------------|------|-------------|
| `http.server.request.duration` | Histogram | `s` | Duration of HTTP server requests. |

## Advanced trace configuration

This instrumentation can be configured to change the default behavior by using
`AspNetTraceInstrumentationOptions`, which allows configuring `Filter` as explained
below.

### Trace Filter

This instrumentation by default collects all the incoming http requests. It
allows filtering of requests by using the `Filter` function in
`AspNetTraceInstrumentationOptions`. This defines the condition for allowable
requests. The Filter receives the `HttpContext` of the incoming request, and
does not collect telemetry about the request if the Filter returns false or
throws exception.

The following code snippet shows how to use `Filter` to only allow GET requests.

```csharp
this.tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddAspNetInstrumentation(
        (options) => options.Filter =
            (httpContext) =>
            {
                // only collect telemetry about HTTP GET requests
                return httpContext.Request.HttpMethod.Equals("GET");
            })
    .Build();
```

It is important to note that this `Filter` option is specific to this
instrumentation. OpenTelemetry has a concept of a
[Sampler](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#sampling),
and the `Filter` option does the filtering *before* the Sampler is invoked.

### Trace Enrich

This instrumentation library provides `EnrichWithHttpRequest`,
`EnrichWithHttpResponse` and `EnrichWithException` options that can be used to
enrich the activity with additional information from the raw `HttpRequest`,
`HttpResponse` and `Exception` objects respectively. These actions are called
only when `activity.IsAllDataRequested` is `true`. It contains the activity
itself (which can be enriched) and the actual raw object.

The following code snippet shows how to enrich the activity using all 3
different options.

```csharp
this.tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddAspNetInstrumentation(o =>
    {
        o.EnrichWithHttpRequest = (activity, httpRequest) =>
        {
            activity.SetTag("physicalPath", httpRequest.PhysicalPath);
        };
        o.EnrichWithHttpResponse = (activity, httpResponse) =>
        {
            activity.SetTag("responseType", httpResponse.ContentType);
        };
        o.EnrichWithException = (activity, exception) =>
        {
            activity.SetTag("exceptionType", exception.GetType().ToString());
        };
    })
    .Build();
```

[Processor](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/trace/extending-the-sdk/README.md#processor),
is the general extensibility point to add additional properties to any activity.
The `Enrich` option is specific to this instrumentation, and is provided to get
access to `HttpRequest` and `HttpResponse`.

### RecordException

This instrumentation automatically sets Activity Status to Error if an unhandled
exception is thrown. Additionally, `RecordException` feature may be turned on,
to store the exception to the Activity itself as ActivityEvent.

## Advanced metric configuration

This instrumentation can be configured to change the default behavior by using
`AspNetMetricsInstrumentationOptions` as explained below.

### Metric Enrich

This option allows one to enrich the metric with additional information from
the `HttpContext`. The `Enrich` action is always called unless the metric was
filtered. The callback allows for modifying the tag list. If the callback
throws an exception the metric will still be recorded.

```csharp
this.meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddAspNetInstrumentation(options => options.Enrich =
        (HttpContext context, ref TagList tags) =>
    {
        // Add request content type to the metric tags.
        if (!string.IsNullOrEmpty(context.Request.ContentType))
        {
            tags.Add("custom.content.type", context.Request.ContentType);
        }
    })
    .Build();
```

## References

* [ASP.NET](https://dotnet.microsoft.com/apps/aspnet)
* [OpenTelemetry Project](https://opentelemetry.io/)
