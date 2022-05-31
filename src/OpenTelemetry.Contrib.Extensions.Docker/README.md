
### Docker Resource Detectors

You can configure Docker resource detector to 
the `TracerProvider` with the following example below.

```csharp
using OpenTelemetry;
using OpenTelemetry.Contrib.Extensions.Docker;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        // other configurations
                        .SetResourceBuilder(ResourceBuilder
                            .CreateDefault()
                            .AddDockerDetector())
                        .Build();
```

The resource detectors will record the following metadata based on where
your application is running:

- **DockerResourceDetector**: container id.

## References

- [OpenTelemetry Project](https://opentelemetry.io/)
