# Hangfire Instrumentation for OpenTelemetry .NET

Automatically instruments the Hangfire jobs from
[Hangfire](https://www.nuget.org/packages/Hangfire/).


## Configuration

ASP.NET Core instrumentation example:

```csharp
// Add OpenTelemetry and Hangfire instrumentation
services.AddOpenTelemetryTracing(x =>
{
    x.AddHangfireInstrumentation();
    x.UseJaegerExporter(config => {
      // Configure Jaeger
    });
});
```


## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Hangfire Project](https://www.hangfire.io/)
