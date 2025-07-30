# AWS Resource Detectors

| Status      |           |
| ----------- | --------- |
| Stability   | [Stable](../../README.md#stable) |
| Code Owners | [@srprash](https://github.com/srprash), [@normj](https://github.com/normj), [@lukeina2z](https://github.com/lukeina2z) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Resources.AWS)](https://www.nuget.org/packages/OpenTelemetry.Resources.AWS)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Resources.AWS)](https://www.nuget.org/packages/OpenTelemetry.Resources.AWS)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Resources.AWS)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Resources.AWS)

## Attribute Utilization

The below Attributes from OpenTelemetry Semantic Convention's can/will be included
on telemetry signals when the corresponding resource detector is
added & enabled in your project.

### AWS EC2 Detector

| Attribute | Comment |
| --- | --- |
|[`cloud.region`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-region) | |
| [`cloud.provider`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-provider) | Will be set to `aws` |
| [`cloud.platform`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-platform) | Will be set to `aws_ec2` |
| [`cloud.account.id`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-account-id) | |
| [`cloud.availability_zone`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-availability-zone) | |
|[`host.id`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/host/#host-id)| |
|[`host.type`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/host/#host-type)| |
|[`host.name`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/host/#host-name)| |

### AWS EBS Detector

| Attribute | Comment |
| --- | --- |
| [`cloud.provider`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-provider) | Will be set to `aws` |
| [`cloud.platform`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-platform) | Will be set to `aws_elastic_beanstalk` |
| [`service.instance.id`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/service/#service-instance-id) | |
| [`service.name`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/service/#service-name) | |
| [`service.namespace`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/service/#service-namespace) | |
| [`service.version`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/service/#service-version) | |

### AWS ECS Detector

| Attribute | Comment |
| --- | --- |
| [`cloud.provider`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-provider) | Will be set to `aws` |
| [`cloud.platform`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-platform) | Will be set to `aws_ecs` |
| [`cloud.account.id`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-account-id) | |
| [`cloud.availability_zone`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-availability-zone) | |
|[`container.id`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/container/#container-id) | |
|[`cloud.resource.id`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-resource-id) | |
|[`cloud.region`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-region) | |
|[`aws.ecs.cluster.arn`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/aws/#aws-ecs-cluster-arn) | |
|[`aws.ecs.task.arn`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/aws/#aws-ecs-task-arn) | |
|[`aws.ecs.task.family`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/aws/#aws-ecs-task-family) | |
|[`aws.task.revision`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/aws/#aws-ecs-task-revision) | |
|[`aws.ecs.launchtype`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/aws/#aws-ecs-launchtype) | |
|[`aws.ecs.container.arn`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/aws/#aws-ecs-container-arn) | |
|[`aws.log.group.names`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/aws/#aws-log-group-names) | |
|[`aws.log.group.arns`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/aws/#aws-log-group-arns) | |
|[`aws.log.stream.names`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/aws/#aws-log-stream-names) | |
|[`aws.log.stream.arns`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/aws/#aws-log-stream-arns) | |

### AWS EKS Detector

| Attribute | Comment |
| --- | --- |
| [`cloud.provider`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-provider) | Will be set to `aws` |
| [`cloud.platform`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/cloud/#cloud-platform) | Will be set to `aws_eks` |
|[`container.id`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/container/#container-id) | |
|[`k8s.cluster.name`](https://opentelemetry.io/docs/specs/semconv/registry/attributes/k8s/#k8s-cluster-name) | |

## Getting Started

### Installation

You need to install the
`OpenTelemetry.Resources.AWS` to be able to use the
AWS Resource Detectors.

The ADOT .NET SDK supports automatically recording metadata in
EC2, Elastic Beanstalk, ECS, and EKS environments.

```shell
dotnet add package OpenTelemetry.Resources.AWS
```

### Adding & Configuring Detector

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

### Semantic Conventions

Future versions the OpenTelemetry.*.AWS libraries will include updates to the
Semantic Convention, which may break compatibility with a previous version.

The default will remain as `V1_28_0` until the next major version bump.

To opt in to automatic upgrades, you can use `SemanticConventionVersion.Latest`
or you can specify a specific version:

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(resource => resource.AddAWSEC2Detector(
        opt => {
            // pin to a specific Semantic Convention version
            opt.SemanticConventionVersion = SemanticConventionVersion.V1_29_0;
        }
    ))
    // other configurations
    .Build();
```

**NOTE:** Once a Semantic Convention becomes Stable, OpenTelemetry.*.AWS
libraries will remain on that version until the
next major version bump.

## References

- [OpenTelemetry Project](https://opentelemetry.io/)
- [AWS Distro for OpenTelemetry .NET](https://aws-otel.github.io/docs/getting-started/dotnet-sdk)
