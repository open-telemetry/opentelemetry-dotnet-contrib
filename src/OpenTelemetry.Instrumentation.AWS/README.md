# AWS SDK client instrumentation for OpenTelemetry

| Status        |           |
| ------------- |-----------|
| Stability     |  [Beta](../../README.md#beta)|
| Code Owners   |  [@srprash](https://github.com/srprash), [@ppittle](https://github.com/ppittle)|

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.AWS)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AWS)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.AWS)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AWS)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Instrumentation.AWS)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Instrumentation.AWS)

Download the `OpenTelemetry.Instrumentation.AWS` package:

```shell
dotnet add package OpenTelemetry.Instrumentation.AWS
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
