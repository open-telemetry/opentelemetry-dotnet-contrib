# AWS Resource Detectors

| Status        |           |
| ------------- |-----------|
| Stability     |  [Beta](../../README.md#beta)|
| Code Owners   |  [@srprash](https://github.com/srprash), [@ppittle](https://github.com/ppittle)|

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Resources.AWS)](https://www.nuget.org/packages/OpenTelemetry.Resources.AWS)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Resources.AWS)](https://www.nuget.org/packages/OpenTelemetry.Resources.AWS)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Resources.AWS)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Resources.AWS)

## Getting Started

You need to install the
`OpenTelemetry.Resources.AWS` to be able to use the
AWS Resource Detectors.

The ADOT .NET SDK supports automatically recording metadata in
EC2, Elastic Beanstalk, ECS, and EKS environments.

```shell
dotnet add package OpenTelemetry.Resources.AWS
```

## Usage

You can configure AWS resource detector to
the `ResourceBuilder` with the following EC2 example.

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(resource => resource.AddAWSEC2Detector())
    // other configurations
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .ConfigureResource(resource => resource.AddAWSEC2Detector())
    // other configurations
    .Build();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddAWSEC2Detector());
    });
});
```

The resource detectors will record the following metadata based on where
your application is running:

- **AWSEC2Detector**: cloud provider, cloud platform, account id,
cloud availability zone, host id, host type, aws region, host name.
- **AWSEBSDetector**: cloud provider, cloud platform, service name,
service namespace, instance id, service version.
- **AWSECSDetector**: cloud provider, cloud platform, cloud resource id,
account id, cloud availability zone, cloud region, container id, cluster arn,
task arn, task family, task revision, launch type, container arn, log group names,
log group ids, log stream names, log stream ids.
- **AWSEKSDetector**: cloud provider, cloud platform, cluster name,
container id.

## References

- [OpenTelemetry Project](https://opentelemetry.io/)
- [AWS Distro for OpenTelemetry .NET](https://aws-otel.github.io/docs/getting-started/dotnet-sdk)
