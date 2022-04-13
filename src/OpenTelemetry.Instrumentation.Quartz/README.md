# QuartzNET Instrumentation for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.Quartz.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Quartz)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.Quartz.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Quartz)

Automatically instruments the Quartz jobs from
[Quartz](https://www.nuget.org/packages/Quartz/).

## Supported Frameworks

QuartzNET Instrumentation is only supported when using .NET Framework >=
`net472` and netstandard >= `netstandard2.0`. Quartz`net461` support for
activity sources has been removed, more information can be found
[here](https://www.quartz-scheduler.net/2021/04/07/quartznet-3-3-released/).

## Installation

```shell
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package --prerelease OpenTelemetry.Instrumentation.Quartz
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
services.AddOpenTelemetryTracing(x =>
{
    x.AddQuartzInstrumentation();
    x.UseJaegerExporter(config => {
      // Configure Jaeger
    });
});
```

## Filter traced operations

This option allows you to filter trace operations.

For example you can trace only execute operations using this snippet:

```csharp
// ...
using OpenTelemetry.Instrumentation.Quartz.Implementation;
// ...
x.AddQuartzInstrumentation(
    opts =>
        opts.TracedOperations = new
        HashSet<string>(new[] {
            OperationName.Job.Execute,
}));
```

For full operation list please see:
[OperationName](../OpenTelemetry.Instrumentation.Quartz/Implementation/OperationName.cs).

All operations are enabled by default.

## Enrich

This option allows one to enrich the activity with additional information from
the raw `JobDetail` object. The `Enrich` action is called only when
`activity.IsAllDataRequested` is `true`. It contains the activity itself (which
can be enriched), the name of the event, and the actual raw object.

For event names "OnStartActivity", "OnStopActivity", the actual object will be
`IJobDetail`.

For event name "OnException", the actual object will be the exception thrown

The following code snippet shows how to add additional tags using `Enrich`.

```csharp
// ...
using OpenTelemetry.Instrumentation.Quartz.Implementation;
using Quartz;
// ...
// Enable enriching an activity after it is created.
x.AddQuartzInstrumentation(opt =>
{
    opt.Enrich = (activity, eventName, quartzJobDetails) =>
    {
        // update activity
        if (quartzJobDetails is IJobDetail jobDetail)
        {
            activity.SetTag("customProperty", jobDetail.JobDataMap["customProperty"]);
            ...
        }
    };
})
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Quartz.NET Project](https://www.quartz-scheduler.net/)
