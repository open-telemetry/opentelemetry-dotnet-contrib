# AWS SDK client instrumentation for OpenTelemetry

Download the `OpenTelemetry.Contrib.Instrumentation.AWS` package:

```shell
dotnet add package OpenTelemetry.Contrib.Instrumentation.AWS
```

Add the AWSXRayIdGenerator and AWSInstrumentation
to your application. The below example is for an ASP.Net Core application.

```csharp
using OpenTelemetry;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;
using OpenTelemetry.Trace;

public void ConfigureServices(IServiceCollection services)
{
    services.AddControllers();
    services.AddOpenTelemetryTracing((builder) => builder
        // for tracing calls to AWS services via AWS SDK for .Net
        .AddAWSInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());
}
```
