# Tracing with AWS Distro for OpenTelemetry .Net SDK

If you want to send the traces to AWS X-Ray, you can do so by using AWS Distro with the OpenTelemetry SDK.

## Getting Started

In order to instrument your .Net application for tracing, start by downloading the OpenTelemetry nuget package

```
dotnet add package OpenTelemetry
```

By default, the OpenTelemetry SDK generates traces with W3C random ID which X-Ray backend doesn’t support yet. You need to install the `OpenTelemetry.Contrib.Extensions.AWSXRay` to be able to use the `AWSXRayIdGenerator` which generates X-Ray compatible trace IDs. If you plan to call an AWS service or another application instrumented with AWS X-Ray SDK, you’ll need to use the `AWSXRayPropagator` as well.

```
dotnet add package OpenTelemetry.Contrib.Extensions.AWSXRay
```

### Note

* You’ll also need to have the [AWS Distro for OpenTelemetry Collector](https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/master/exporter/awsxrayexporter) running to export traces to X-Ray.

### Instrumenting .Net applications

#### ASP.Net Core

Start by downloading the ASP.Net Core and OTLP exporter instrumentation packages

```
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

Next, in your application’s **Startup.cs** add the instrumentation and the OTLP exporter as services in the `ConfigureServices` method. Make sure to call `AddXRayTraceId()` in the **beginning** when building `TracerProviderBuilder`. If you want to trace AWS services, make sure to configure `AWSXRayPropagator`.

```
using OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;
using OpenTelemetry.Trace;

public void ConfigureServices(IServiceCollection services)
{
    services.AddOpenTelemetryTracing((builder) => builder
        .AddXRayTraceId() // for generating AWS X-Ray compliant trace IDs
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());
    Sdk.SetDefaultTextMapPropagator(new AWSXRayPropagator()); // configure AWSXRayPropagator()            
}
```

By default the OTLP exporter sends data to an OpenTelemetry collector at **localhost:55681**

#### ASP.Net

Download the ASP.Net and OTLP exporter instrumentation packages

```
dotnet add package OpenTelemetry.Instrumentation.AspNet
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

The ASP.Net instrumentation requires [modification](https://github.com/open-telemetry/opentelemetry-dotnet/blob/master/src/OpenTelemetry.Instrumentation.AspNet/README.md#step-2-modify-webconfig) to Web.config to add a HttpModule to your web server.

```xml
<system.webServer>
    <modules>
    <add name="TelemetryCorrelationHttpModule"
    type="Microsoft.AspNet.TelemetryCorrelation.TelemetryCorrelationHttpModule,
    Microsoft.AspNet.TelemetryCorrelation"
    preCondition="integratedMode,managedHandler" />
    </modules>
</system.webServer>
```

Now all you need to do is enable the instrumentation for the application startup. This is done in the **Global.asax.cs** as shown below. Make sure to call `AddXRayTraceId()` in the **beginning** when building `TracerProviderBuilder`. If you want to trace AWS services, make sure to configure `AWSXRayPropagator`.

```
using OpenTelemetry;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;
using OpenTelemetry.Trace;

public class WebApiApplication : HttpApplication
{
    private TracerProvider tracerProvider;
    protected void Application_Start()
    {
        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddXRayTraceId() // for generating AWS X-Ray compliant trace IDs
            .AddAspNetInstrumentation()
            .AddOtlpExporter()
            .Build();
        Sdk.SetDefaultTextMapPropagator(new AWSXRayPropagator()); // configure AWSXRayPropagator()            
    }
    
    protected void Application_End()
    {
        this.tracerProvider?.Dispose();
    }
}
```

#### Console

Make sure to call `AddXRayTraceIdWithSampler()` in the **beginning** when building `TracerProviderBuilder`. You’ll need to pass the sampler you’re using in your application. If you’re using the default sampler, just pass `new ParentBasedSampler(new AlwaysOnSampler())`. If you want to trace AWS services, make sure to configure `AWSXRayPropagator`.

```
using OpenTelemetry;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;
using OpenTelemetry.Trace;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddXRayTraceIdWithSampler(your_sampler) // for generating AWS X-Ray compliant trace IDs
                .AddOtlpExporter()
                .Build();
Sdk.SetDefaultTextMapPropagator(new AWSXRayPropagator()); // configure AWSXRayPropagator()
```

### Instrumenting AWS SDK

For tracing downstream call to AWS services from your .Net application, you will need three components: the `AWSXRayIdGenerator`, the `AWSXRayPropagator`, and the AWS client instrumentation. 

 Download the OpenTelemetry.Contrib.Instrumentation.AWS package:

```
dotnet add package OpenTelemetry.Contrib.Extensions.AWSXRay
```

Add the `AWSXRayIdGenerator`, `AWSXRayPropagator` and `AWSInstrumentation` to your application. The below example is for an ASP.Net Core application.

```
using OpenTelemetry;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;
using OpenTelemetry.Trace;

public void ConfigureServices(IServiceCollection services)
{
    services.AddControllers();
    services.AddOpenTelemetryTracing((builder) => builder
        .AddXRayTraceId() // for generating AWS X-Ray compliant trace IDs
        .AddAWSInstrumentation() // for tracing calls to AWS services via AWS SDK for .Net
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());   
    Sdk.SetDefaultTextMapPropagator(new AWSXRayPropagator()); // configure AWSXRayPropagator()            
}
```

### Adding Custom Attributes

You can add custom attributes to an `Activity` by calling `SetTag` method on the current activity.

```
var currentActivity = Activity.Current;
currentActivity.SetTag("key", "val");
```

#### Note:
* When using AWS X-Ray as your tracing backend, you can control whether attributes are uploaded as annotations or metadata by configuring the AWS OTel Collector’s indexed [keys](https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/master/exporter/awsxrayexporter#exporter-configuration). By default, all attributes will be metadata.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [AWS Distro for OpenTelemetry Collector](https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/master/exporter/awsxrayexporter)
