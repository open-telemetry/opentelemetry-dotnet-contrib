# QuartzNET Instrumentation for OpenTelemetry .NET

Automatically instruments the Quartz jobs from
[Quartz](https://www.nuget.org/packages/Quartz/).

## Installation

```shell
dotnet add package OpenTelemetry.Contrib.Instrumentation.Quartz
```

## Configuration

ASP.NET Core instrumentation example:

```csharp
// Add QuartzNET inside ConfigureServices
public void ConfigureServices(IServiceCollection services)
{
    services.AddQuartz(q =>
    {
        // base quartz scheduler, job and trigger configuration
    });

    // ASP.NET Core hosting
    services.AddQuartzServer(options =>
    {
        // when shutting down we want jobs to complete gracefully
        options.WaitForJobsToComplete = true;
    });
}

// Add OpenTelemetry and Quartz instrumentation
services.AddOpenTelemetrySdk(x =>
{
    x.AddQuartzInstrumentation();
    x.UseJaegerExporter(config => {
      // Configure Jaeger
    });
});
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Quartz.NET Project](https://www.quartz-scheduler.net/)

