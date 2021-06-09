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

## Filter traced operations

For example you can trace only consume and handle operations using this snippet:

```csharp
// ...
using OpenTelemetry.Instrumentation.Quartz.Implementation;
// ...
x.AddQuartzInstrumentation(
    opts =>
        opts.TracedOperations = new HashSet<string>(new[] {
            OperationName.Job.Execute,
            OperationName.Job.Veto
}));
```

For full operation list please see: [OperationName](../OpenTelemetry.Instrumentation.Quartz/Implementation/OperationName.cs).

All operations are enabled by default.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Quartz.NET Project](https://www.quartz-scheduler.net/)

