# Docker Resource Detectors

## Getting Started

You need to install the
`OpenTelemetry.ResourceDetector.Container` to be able to use the
Container Resource Detectors. It detects container.id from
Container environment.

```shell
dotnet add package OpenTelemetry.ResourceDetector.Container
```

## Usage

You can configure Docker resource detector to
the `TracerProvider` with the following example below.

```csharp
using OpenTelemetry;
using OpenTelemetry.ResourceDetector.Container;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        // other configurations
                        .SetResourceBuilder(ResourceBuilder
                            .CreateEmpty()
                            .AddDetector(new ContainerResourceDetector()))
                        .Build();
```

The resource detectors will record the following metadata based on where
your application is running:

- **DockerResourceDetector**: container.id.

## References

- [OpenTelemetry Project](https://opentelemetry.io/)
