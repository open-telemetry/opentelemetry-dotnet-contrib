# OWIN Instrumentation for OpenTelemetry .NET

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.Owin)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Owin)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.Owin)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Owin)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Instrumentation.Owin)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Instrumentation.Owin)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/glossary.md#instrumentation-library),
which instruments [OWIN/Katana](https://github.com/aspnet/AspNetKatana/) and
collects telemetry about incoming requests.

## Steps to enable OpenTelemetry.Instrumentation.Owin

An example project is available in the
[examples/owin](../../examples/owin/) folder.

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.Owin`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Owin)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Instrumentation.Owin
```

### Step 2: Configure OWIN middleware

Call the `UseOpenTelemetry` `IAppBuilder` extension to register OpenTelemetry
middleware which emits diagnostic events from th OWIN pipeline. This should be
done before any other middleware registrations.

```csharp
    using var host = WebApp.Start(
        "http://localhost:9000",
        appBuilder =>
        {
            appBuilder.UseOpenTelemetry();
        });
```

### Step 3: Enable OWIN Instrumentation at application startup

#### Configure OpenTelemetry TracerProvider

Call the `AddOwinInstrumentation` `TracerProviderBuilder` extension to register
OpenTelemetry instrumentation which listens to the OWIN diagnostic events.

```csharp
    using var openTelemetry = Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Owin-Example"))
        .AddOwinInstrumentation()
        .AddConsoleExporter()
        .Build();
```

#### Configure OpenTelemetry MeterProvider

Call the `AddOwinInstrumentation` `MeterProviderBuilder` extension to register
OpenTelemetry instrumentation which generates request duration metrics for OWIN requests.

The metric implemention does not rely on tracing, and will generate metrics
even if tracing is disabled.

```csharp
    using var openTelemetry = Sdk.CreateMeterProviderBuilder()
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Owin-Example"))
        .AddOwinInstrumentation()
        .AddConsoleExporter()
        .Build();
```

The instrumentation is implemented based on [metrics semantic
conventions](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/semantic_conventions/http-metrics.md#metric-httpserverduration).
Currently, the instrumentation supports the following metric.

| Name  | Instrument Type | Unit | Description |
|-------|-----------------|------|-------------|
| `http.server.request.duration` | Histogram | `s` | Duration of HTTP server requests. |

## Customize OWIN span names

The OpenTelemetry OWIN instrumentation will create spans with very generic names
based on the http method of the request. For example: `HTTP GET` or `HTTP POST`.
The reason for this is the [OpenTelemetry Specification http semantic
conventions](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/http.md#name)
call specifically for low cardinality values and OWIN does not expose any kind
of route template.

To change the span name set `Activity.Current.DisplayName` to the value you want
to display once a route has been resolved. Here is how this can be done using WebAPI:

```csharp
    using var host = WebApp.Start(
        "http://localhost:9000",
        appBuilder =>
        {
            appBuilder.UseOpenTelemetry();

            HttpConfiguration config = new HttpConfiguration();

            config.MessageHandlers.Add(new ActivityDisplayNameRouteEnrichingHandler());

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional });

            appBuilder.UseWebApi(config);
        });

    private class ActivityDisplayNameRouteEnrichingHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                var activity = Activity.Current;
                if (activity != null)
                {
                    var routeData = request.GetRouteData();
                    if (routeData != null)
                    {
                        activity.DisplayName = routeData.Route.RouteTemplate;
                    }
                }
            }
        }
    }
```

## References

* [Open Web Interface for .NET](http://owin.org/)
* [Katana Project](https://github.com/aspnet/AspNetKatana/)
* [OpenTelemetry Project](https://opentelemetry.io/)
