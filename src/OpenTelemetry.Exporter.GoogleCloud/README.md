# Google Cloud Exporter for OpenTelemetry .NET

| Status        |           |
| ------------- |-----------|
| Stability     |  [Beta](../../README.md#beta)|
| Code Owners   |  [@SergeyKanzhelev](https://github.com/SergeyKanzhelev)|

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.GoogleCloudMonitoring )](https://www.nuget.org/packages/OpenTelemetry.Exporter.GoogleCloudMonitoring )
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.GoogleCloudMonitoring )](https://www.nuget.org/packages/OpenTelemetry.Exporter.GoogleCloudMonitoring )
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Exporter.GoogleCloudMonitoring )](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Exporter.GoogleCloudMonitoring )

**NOTE: This exporter is not affiliated with or officially supported by
Google.**

This sample assumes your code authenticates to GoogleCloudMonitoring  APIs using [service
account][gcp-auth] with credentials stored in environment variable
GOOGLE_APPLICATION_CREDENTIALS. When you run on [GAE][GAE], [GKE][GKE] or
locally with gcloud sdk installed - this is typically the case. There is also a
constructor for specifying path to the service account credential.

1. Add [Google Cloud Exporter
   package][OpenTelemetry-exporter-googlecloud-myget-url] reference.
2. Enable [Google Cloud Trace][googlecloud-trace-setup] API.
3. Enable [Google Cloud Monitoring][googlecloud-monitoring-setup] API.
4. Instantiate a new instance of `GoogleCloudExporter` with your Google Cloud's
   ProjectId

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.GoogleCloudMonitoring  --prerelease
```

## Traces

```csharp
var spanExporter = new GoogleCloudMonitoring TraceExporter(projectId);

using var tracerFactory = TracerFactory.Create(builder =>
    builder.AddProcessorPipeline(c => c.SetExporter(spanExporter)));
var tracer = tracerFactory.GetTracer("googlecloud-test");

using (tracer.StartActiveSpan("/getuser", out TelemetrySpan span))
{
    span.AddEvent("Processing video.");
    span.PutHttpMethodAttribute("GET");
    span.PutHttpHostAttribute("localhost", 8080);
    span.PutHttpPathAttribute("/resource");
    span.PutHttpStatusCodeAttribute(200);
    span.PutHttpUserAgentAttribute("Mozilla/5.0");

    Thread.Sleep(TimeSpan.FromMilliseconds(10));
}
```

## References

* [googlecloud-trace-setup](https://cloud.google.com/trace/docs/setup/)
* [googlecloud-monitoring-setup](https://cloud.google.com/monitoring/api/enable-api)
* [GAE](https://cloud.google.com/appengine/docs/flexible/dotnet/quickstart)
* [GKE](https://codelabs.developers.google.com/codelabs/cloud-kubernetes-aspnetcore/index.html)
* [gcp-auth](https://cloud.google.com/docs/authentication/getting-started)

[googlecloud-trace-setup]: https://cloud.google.com/trace/docs/setup/
[googlecloud-monitoring-setup]:
    https://cloud.google.com/monitoring/api/enable-api
[GAE]: https://cloud.google.com/appengine/docs/flexible/dotnet/quickstart
[GKE]:
    https://codelabs.developers.google.com/codelabs/cloud-kubernetes-aspnetcore/index.html
[gcp-auth]: https://cloud.google.com/docs/authentication/getting-started
[OpenTelemetry-exporter-googlecloud-myget-url]:
    https://www.nuget.org/packages/OpenTelemetry.Exporter.GoogleCloud
