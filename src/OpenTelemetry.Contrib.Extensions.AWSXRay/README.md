# Tracing with AWS Distro for OpenTelemetry .Net SDK

If you want to send the traces to AWS X-Ray, you can do so
by using AWS Distro with the OpenTelemetry SDK.

## Getting Started

The OpenTelemetry SDK generates traces with W3C random ID which X-Ray
backend doesn't currently support. You need to install the
`OpenTelemetry.Contrib.Extensions.AWSXRay` to be able to use the
AWS X-Ray id generator which generates X-Ray compatible trace IDs.
If you plan to call another application instrumented with AWS X-Ray SDK,
you'll need to configure the AWS X-Ray propagator as well.

```shell
dotnet add package OpenTelemetry.Contrib.Extensions.AWSXRay
```

## Usage

### AWS X-Ray Id Generator and Propagator

Configure AWS X-Ray ID generator and propagator globally in your
application as follows. Make sure to call `AddXRayTraceId()` in the
very beginning when creating `TracerProvider`.

```csharp
using OpenTelemetry;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;
using OpenTelemetry.Trace;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .AddXRayTraceId()
                        // other instrumentations
                        ...
                        .Build();

Sdk.SetDefaultTextMapPropagator(new AWSXRayPropagator());
```

### AWS Resource Detectors

The ADOT .NET SDK supports automatically recording metadata in
EC2, Elastic Beanstalk, ECS, and EKS environments. You can configure
the corresponding resource detector to the `TracerProvider` following
the EC2 example below.

```csharp
using OpenTelemetry;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Resources;
using OpenTelemetry.Resources;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        // other configurations
                        .SetResourceBuilder(ResourceBuilder
                            .CreateDefault()
                            .AddDetector(new AWSEC2ResourceDetector()))
                        .Build();
```

The resource detectors will record the following metadata based on where
your application is running:

- **AWSEC2ResourceDetector**: cloud provider, cloud platform, account id,
cloud available zone, host id, host type, aws region, host name.
- **AWSEBSResourceDetector**: cloud provider, cloud platform, service name,
service namespace, instance id, service version.
- **AWSECSResourceDetector**: cloud provider, cloud platform, container id.
- **AWSEKSResourceDetector**: cloud provider, cloud platform, cluster name,
container id.
- **AWSLambdaResourceDetector**: cloud provider, cloud platform, aws region,
function name, function version.

### AWS X-Ray Remote Sampler

The ADOT .Net SDK provides a sampler which can get sampling
configurations from AWS X-Ray to make sampling decisions.
See: [AWS X-Ray Sampling](https://docs.aws.amazon.com/xray/latest/devguide/xray-concepts.html#xray-concepts-sampling)

You can configure the `AWSXRayRemoteSampler` as per the following example.

```csharp
using OpenTelemetry;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;
using OpenTelemetry.Trace;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        // other configurations
                        .SetSampler(AWSXRayRemoteSampler.Builder()
                                                        .SetPollingInterval(TimeSpan.FromSeconds(10))
                                                        .SetEndpoint("http://localhost:2000)
                                                        .Build())
                        .Build();
```

## References

- [OpenTelemetry Project](https://opentelemetry.io/)
- [AWS Distro for OpenTelemetry .NET](https://aws-otel.github.io/docs/getting-started/dotnet-sdk)
