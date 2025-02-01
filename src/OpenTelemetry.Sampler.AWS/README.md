# AWS X-Ray Remote Sampler

| Status        |           |
| ------------- |-----------|
| Stability     |  [Alpha](../../README.md#alpha)|
| Code Owners   |  [@srprash](https://github.com/srprash), [@ppittle](https://github.com/ppittle)|

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Sampler.AWS)](https://www.nuget.org/packages/OpenTelemetry.Sampler.AWS)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Sampler.AWS)](https://www.nuget.org/packages/OpenTelemetry.Sampler.AWS)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Sampler.AWS)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Sampler.AWS)

This package provides a sampler which can get sampling
configurations from AWS X-Ray to make sampling decisions.
See: [AWS X-Ray Sampling](https://docs.aws.amazon.com/xray/latest/devguide/xray-concepts.html#xray-concepts-sampling)

Start with installing the package

```shell
dotnet add package OpenTelemetry.Sampler.AWS --prerelease
```

You can configure the `AWSXRayRemoteSampler` as per the following example.
Note that you will need to configure your [OpenTelemetry Collector for
X-Ray remote sampling](https://aws-otel.github.io/docs/getting-started/remote-sampling).
This example also sets up the Console Exporter,
which requires adding the package [`OpenTelemetry.Exporter.Console`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Console/README.md)
to the application.

```csharp
using OpenTelemetry;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Resources;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;
using OpenTelemetry.Sampler.AWS;
using OpenTelemetry.Trace;

var serviceName = "MyServiceName";

var resourceBuilder = ResourceBuilder
    .CreateDefault()
    .AddService(serviceName: serviceName)
    .AddAWSEC2Detector();

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(serviceName)
    .SetResourceBuilder(resourceBuilder)
    .AddConsoleExporter()
    .SetSampler(
        new AWSXRayRemoteSampler(
            // you must provide a resource
            resourceBuilder.Build(),
            cfg =>
            {
                cfg.PollingInterval = pollingInterval;
                cfg.Endpoint = endpoint;
            }))
    .Build();
```

## Semantic Conventions

_For an overview on Semantic Conventions, see
[OpenTelemetery - Semantic Conventions](https://opentelemetry.io/docs/concepts/semantic-conventions/)_.

While this library is intended for production use, it relies on several
Semantic Conventions that are still considered Experimental, meaning
they may undergo additional changes before becoming Stable.  This can impact
the aggregation and analysis of telemetry signals in environments with
multiple applications or microservices.

For example, a microservice using an older version of the Semantic Conventions
for Http Attributes may emit `"http.method"` with a value of GET, while a
different microservice, using a new version of Semantic Convention may instead
emit the GET as `"http.request.method"`.

Future versions the OpenTelemetry.*.AWS libraries will include updates to the
Semantic Convention, which may break compatibility with a previous version.

The default will remain as `V1_28_0` until the next major version bump.

To opt in to automatic upgrades, you can use `SemanticConventionVersion.Latest`
or you can specify a specific version:

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(serviceName)
    .SetResourceBuilder(resourceBuilder)
    .SetSampler(
        new AWSXRayRemoteSampler(
            // you must provide a resource
            resourceBuilder.Build(),
            cfg =>
            {
               // pin to a specific Semantic Convention version
                cfg.SemanticConventionVersion = SemanticConventionVersion.V1_29_0
            }))
    .Build();
```

**NOTE:** Once a Semantic Convention becomes Stable, OpenTelemetry.*.AWS
libraries will remain on that version until the
next major version bump.

## References

- [OpenTelemetry Project](https://opentelemetry.io/)
- [AWS Distro for OpenTelemetry .NET](https://aws-otel.github.io/docs/getting-started/dotnet-sdk)
