# AWS X-Ray Remote Sampler

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Sampler.AWS.svg)](https://www.nuget.org/packages/OpenTelemetry.Sampler.AWS)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Sampler.AWS.svg)](https://www.nuget.org/packages/OpenTelemetry.Sampler.AWS)

This package provides a sampler which can get sampling
configurations from AWS X-Ray to make sampling decisions.
See: [AWS X-Ray Sampling](https://docs.aws.amazon.com/xray/latest/devguide/xray-concepts.html#xray-concepts-sampling)

Start with installing the package

```shell
dotnet add package OpenTelemetry.Sampler.AWS
```

You can configure the `AWSXRayRemoteSampler` as per the following example.
Note that you will need to configure your [OpenTelemetry Collector for
X-Ray remote sampling](https://aws-otel.github.io/docs/getting-started/remote-sampling)

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
