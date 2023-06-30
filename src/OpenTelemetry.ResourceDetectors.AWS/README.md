# AWS Resource Detectors

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.ResourceDetectors.AWS)](https://www.nuget.org/packages/OpenTelemetry.ResourceDetectors.AWS)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.ResourceDetectors.AWS)](https://www.nuget.org/packages/OpenTelemetry.ResourceDetectors.AWS)

## Getting Started

You need to install the
`OpenTelemetry.ResourceDetectors.AWS` to be able to use the
AWS Resource Detectors.

The ADOT .NET SDK supports automatically recording metadata in
EC2, Elastic Beanstalk, ECS, and EKS environments.

```shell
dotnet add package OpenTelemetry.ResourceDetectors.AWS
```

## Usage

You can configure AWS resource detector to
the `TracerProvider` with the following EC2 example below.

```csharp
using OpenTelemetry;
using OpenTelemetry.ResourceDetectors.AWS;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        // other configurations
                        .SetResourceBuilder(ResourceBuilder
                            .CreateEmpty()
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

## References

- [OpenTelemetry Project](https://opentelemetry.io/)
- [AWS Distro for OpenTelemetry .NET](https://aws-otel.github.io/docs/getting-started/dotnet-sdk)
