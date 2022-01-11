# OWIN Instrumentation for OpenTelemetry .NET

[![nuget](https://img.shields.io/nuget/v/OpenTelemetry.Contrib.Instrumentation.Own.svg)](https://www.nuget.org/packages/OpenTelemetry.Contrib.Instrumentation.Owin/)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/glossary.md#instrumentation-library),
which instruments [OWIN/Katana](https://github.com/aspnet/AspNetKatana/) and
collects telemetry about incoming requests.

## Steps to enable OpenTelemetry.Contrib.Instrumentation.Owin

An example project is available in the
[examples/owin](../../examples/owin/) folder.

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Contrib.Instrumentation.Owin`](https://www.nuget.org/packages/opentelemetry.contrib.instrumentation.owin)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Contrib.Instrumentation.Owin
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

### Step 3: Configure OpenTelemetry TracerProvider

Call the `AddOwinInstrumentation` `TracerProviderBuilder` extension to register
OpenTelemetry instrumentation which listens to the OWIN diagnostic events.

```csharp
    using var openTelemetry = Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Owin-Example"))
        .AddOwinInstrumentation()
        .AddConsoleExporter()
        .Build();
```

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
