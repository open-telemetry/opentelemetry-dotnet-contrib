# Docker Resource Detectors

## Getting Started

You need to install the
`OpenTelemetry.Extensions.Docker` to be able to use the
Docker Resource Detectors. It detects container.id from
Docker environment.

```shell
dotnet add package OpenTelemetry.Extensions.Docker
```

## Usage

You can configure Docker resource detector to
the `TracerProvider` with the following example below.

```csharp
using OpenTelemetry;
using OpenTelemetry.Extensions.Docker.Resources;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        // other configurations
                        .SetResourceBuilder(ResourceBuilder
                            .CreateEmpty()
                            .AddDetector(new DockerResourceDetector()))
                        .Build();
```

The resource detectors will record the following metadata based on where
your application is running:

- **DockerResourceDetector**: container id.

## References

- [OpenTelemetry Project](https://opentelemetry.io/)
