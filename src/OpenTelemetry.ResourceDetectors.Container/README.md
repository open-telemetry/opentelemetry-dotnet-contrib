# Container resource detector

## Get started

The ``CONTAINER`` resource detector retrieves the ``container.id``
from the container environment using `OpenTelemetry.ResourceDetectors.Container`.

## Usage

You can configure the container resource detector through
the `TracerProvider` as in the following example:

```csharp
using OpenTelemetry;
using OpenTelemetry.ResourceDetectors.Container;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        // other configurations
                        .SetResourceBuilder(ResourceBuilder
                            .CreateEmpty()
                            .AddDetector(new ContainerResourceDetector()))
                        .Build();
```

The resource detector records the following metadata based on where
your application is running:

- **ContainerResourceDetector**: container.id.

## References

- [OpenTelemetry Project](https://opentelemetry.io/)
