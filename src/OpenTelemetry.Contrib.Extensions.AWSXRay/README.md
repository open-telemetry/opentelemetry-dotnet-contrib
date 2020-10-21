# AWS Client instrumentation for OpenTelemetry .Net

Automatically instruments requests made by AWS SDK clients to donwstream AWS services

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

**Note:** If you are using the AWS client instrumentation along with
the HTTP client instrumentation, in addition to the AWS client
span you may see a HTTP span for the AWS request.
To avoid this, it is recommended to use the `AWSXRayPropagator`
with your HTTP client instrumentation:

```csharp
services.AddOpenTelemetryTracing((builder) => builder
    .AddAspNetCoreInstrumentation()
    .AddXRayActivityTraceIdGenerator()
    .AddAWSInstrumentation()
    .AddHttpClientInstrumentation(httpOptions =>
    {
        httpOptions.Propagator = new AWSXRayPropagator();
    })
    .AddJaegerExporter(jaegerOptions =>
    {
        // Configure Jaeger
    }));
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [AWS SDK for .Net](https://aws.amazon.com/sdk-for-net/)
