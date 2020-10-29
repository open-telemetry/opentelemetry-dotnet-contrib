# AWS Client instrumentation for OpenTelemetry .Net

Automatically instruments requests made by AWS SDK clients to downstream AWS services

## Installation

```shell
dotnet add package OpenTelemetry.Contrib.Extensions.AWSXRay
```

## Configuration

ASP.NetCore example for OpenTelemetry instrumentation:

```csharp
services.AddOpenTelemetryTracing((builder) => builder
    .AddAspNetCoreInstrumentation()
    .AddXRayActivityTraceIdGenerator()
    .AddAWSInstrumentation()
    .AddJaegerExporter(jaegerOptions =>
    {
        // Configure Jaeger
    }));
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [AWS SDK for .Net](https://aws.amazon.com/sdk-for-net/)
