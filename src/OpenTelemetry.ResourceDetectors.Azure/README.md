# Resource Detectors for Azure cloud environments

This Resource Detector takes the various Environment Variables that are passed into your applications and sets them as resource attributes.

Currently, this works with Azure AppService, however, a lot these environment variables are reused across other services such as Azure Virtual Machines.

## Usage

```csharp

    builder.Services.TryAddSingleton<AzureResourceDetector>();
    builder.Services.ConfigureOpenTelemetryTracerProvider((serviceProvider, tracerProvider) =>
        tracerProvider
            .ConfigureResource(resourceBuilder => resourceBuilder.AddDetector(
                serviceProvider.GetRequiredService<AzureResourceDetector>()
            ))
```
