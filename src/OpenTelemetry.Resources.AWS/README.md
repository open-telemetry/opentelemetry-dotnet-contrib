# AWS Resource Detectors

| Status      |           |
| ----------- | --------- |
| Stability   | [Stable](../../README.md#stable) |
| Code Owners | [@srprash](https://github.com/srprash), [@normj](https://github.com/normj), [@lukeina2z](https://github.com/lukeina2z) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Resources.AWS)](https://www.nuget.org/packages/OpenTelemetry.Resources.AWS)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Resources.AWS)](https://www.nuget.org/packages/OpenTelemetry.Resources.AWS)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Resources.AWS)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Resources.AWS)

## Attribute Utilization

The below attributes from OpenTelemetry Semantic Conventions can/will be included
on telemetry signals when the corresponding resource detector is
added & enabled to the corresponding telemetry provider.

### AWS EC2 Detector

**Name:** AWSEC2Detector

**[`cloud`](https://opentelemetry.io/docs/specs/semconv/registry/entities/cloud/#cloud) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `cloud.account.id` | |
| `cloud.availability_zone` | |
| `cloud.platform` | Will be set to `aws_ec2` |
| `cloud.provider` | Will be set to `aws` |
| `cloud.region` | |

**[`host`](https://opentelemetry.io/docs/specs/semconv/registry/entities/host/#host) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `host.id` | |
| `host.name` | |
| `host.type` | |

### AWS EBS Detector

**Name:** AWSEBSDetector

**[`cloud`](https://opentelemetry.io/docs/specs/semconv/registry/entities/cloud/#cloud) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `cloud.platform` | Will be set to `aws_elastic_beanstalk` |
| `cloud.provider` | Will be set to `aws` |

**[`service`](https://opentelemetry.io/docs/specs/semconv/registry/entities/service/#service) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `service.instance.id` | |
| `service.name` | |
| `service.namespace` | |
| `service.version` | |

### AWS ECS Detector

**Name:** AWSECSDetector

**[`aws.ecs`](https://opentelemetry.io/docs/specs/semconv/registry/entities/aws/#aws-ecs) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `aws.ecs.cluster.arn` | |
| `aws.ecs.container.arn` | |
| `aws.ecs.launchtype` | |
| `aws.ecs.task.arn` | |
| `aws.ecs.task.family` | |
| `aws.ecs.task.revision` | |

**[`aws.log`](https://opentelemetry.io/docs/specs/semconv/registry/entities/aws/#aws-log) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `aws.log.group.arns` | |
| `aws.log.group.names` | |
| `aws.log.stream.arns` | |
| `aws.log.stream.names` | |

**[`cloud`](https://opentelemetry.io/docs/specs/semconv/registry/entities/cloud/#cloud) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `cloud.account.id` | |
| `cloud.availability_zone` | |
| `cloud.platform` | Will be set to `aws_ecs` |
| `cloud.provider` | Will be set to `aws` |
| `cloud.resource.id` | |
| `cloud.region` | |

**[`container`](https://opentelemetry.io/docs/specs/semconv/registry/entities/container/) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `container.id` | |

### AWS EKS Detector

**Name:** AWSEKSDetector

**[`cloud`](https://opentelemetry.io/docs/specs/semconv/registry/entities/cloud/#cloud) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `cloud.platform` | Will be set to `aws_eks` |
| `cloud.provider` | Will be set to `aws` |

**[`container`](https://opentelemetry.io/docs/specs/semconv/registry/entities/container/) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `container.id` | |

**[`k8s.cluster`](https://opentelemetry.io/docs/specs/semconv/registry/entities/k8s/#k8s-cluster) Entity Attributes:**

| Attribute | Comment |
| --- | --- |
| `k8s.cluster.name` | |

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
