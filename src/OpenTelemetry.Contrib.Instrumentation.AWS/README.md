# AWS SDK client instrumentation for OpenTelemetry

[![nuget](https://img.shields.io/nuget/v/OpenTelemetry.Contrib.Instrumentation.AWS.svg)](https://www.nuget.org/packages/OpenTelemetry.Contrib.Instrumentation.AWS)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Contrib.Instrumentation.AWS.svg)](https://www.nuget.org/packages/OpenTelemetry.Contrib.Instrumentation.AWS)

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
