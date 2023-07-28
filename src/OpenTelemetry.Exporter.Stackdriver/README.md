# Stackdriver Exporter for OpenTelemetry .NET

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Stackdriver)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Stackdriver)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Stackdriver)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Stackdriver)

**NOTE: This exporter is not affiliated with or officially supported by
Google.**

This sample assumes your code authenticates to Stackdriver APIs using [service
account][gcp-auth] with credentials stored in environment variable
GOOGLE_APPLICATION_CREDENTIALS. When you run on [GAE][GAE], [GKE][GKE] or
locally with gcloud sdk installed - this is typically the case. There is also a
constructor for specifying path to the service account credential.

1. Add [Stackdriver Exporter
   package][OpenTelemetry-exporter-stackdriver-myget-url] reference.
2. Enable [Stackdriver Trace][stackdriver-trace-setup] API.
3. Enable [Stackdriver Monitoring][stackdriver-monitoring-setup] API.
4. Instantiate a new instance of `StackdriverExporter` with your Google Cloud's
   ProjectId

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.Stackdriver --prerelease
```

## Traces

```csharp
var spanExporter = new StackdriverTraceExporter(projectId);

using var tracerFactory = TracerFactory.Create(builder =>
    builder.AddProcessorPipeline(c => c.SetExporter(spanExporter)));
var tracer = tracerFactory.GetTracer("stackdriver-test");

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

* [stackdriver-trace-setup](https://cloud.google.com/trace/docs/setup/)
* [stackdriver-monitoring-setup](https://cloud.google.com/monitoring/api/enable-api)
* [GAE](https://cloud.google.com/appengine/docs/flexible/dotnet/quickstart)
* [GKE](https://codelabs.developers.google.com/codelabs/cloud-kubernetes-aspnetcore/index.html)
* [gcp-auth](https://cloud.google.com/docs/authentication/getting-started)

[stackdriver-trace-setup]: https://cloud.google.com/trace/docs/setup/
[stackdriver-monitoring-setup]:
    https://cloud.google.com/monitoring/api/enable-api
[GAE]: https://cloud.google.com/appengine/docs/flexible/dotnet/quickstart
[GKE]:
    https://codelabs.developers.google.com/codelabs/cloud-kubernetes-aspnetcore/index.html
[gcp-auth]: https://cloud.google.com/docs/authentication/getting-started
[OpenTelemetry-exporter-stackdriver-myget-url]:
    https://www.nuget.org/packages/OpenTelemetry.Exporter.Stackdriver
