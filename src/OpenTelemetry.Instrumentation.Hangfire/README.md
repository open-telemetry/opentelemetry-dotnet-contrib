# Hangfire Instrumentation for OpenTelemetry .NET

| Status      |           |
| ----------- | --------- |
| Stability   | [Beta](../../README.md#beta) |
| Code Owners | [@fred2u](https://github.com/fred2u) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.Hangfire)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Hangfire)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.Hangfire)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Hangfire)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Instrumentation.Hangfire)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Instrumentation.Hangfire)

This is an
[Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments
[Hangfire](https://www.nuget.org/packages/Hangfire/)
and collects traces and metrics about BackgroundJob executions.

## Steps to enable OpenTelemetry.Instrumentation.Hangfire

> [!NOTE]
> The following steps show how to enable **tracing**. For metrics, see the
> [Metrics](#metrics) section below.

### Step 1: Install and configure Hangfire

[Getting Started](https://docs.hangfire.io/en/latest/getting-started/index.html)

### Step 2: Install Hangfire instrumentation Package

Add a reference to the
[`OpenTelemetry.Instrumentation.Hangfire`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Hangfire)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Instrumentation.Hangfire --prerelease
```

### Step 3: Enable Hangfire Instrumentation at application startup

Hangfire instrumentation must be enabled at application startup.

The following example demonstrates adding Hangfire instrumentation to a
console application. This example also sets up the OpenTelemetry Console
exporter, which requires adding the package
[`OpenTelemetry.Exporter.Console`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Console/README.md)
to the application.

```csharp
using OpenTelemetry.Trace;

public class Program
{
    public static void Main(string[] args)
    {
        using var tracerProvider = Sdk
            .CreateTracerProviderBuilder()
            .AddHangfireInstrumentation()
            .AddConsoleExporter()
            .Build();
    }
}
```

For an ASP.NET Core application, adding instrumentation is typically done in
the `ConfigureServices` of your `Startup` class. Refer to [example](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/examples/AspNetCore/Program.cs).

For an ASP.NET application, adding instrumentation is typically done in the
`Global.asax.cs`. Refer to [example](../../examples/AspNet/Global.asax.cs).

## Advanced configuration

This instrumentation can be configured to change the default behavior by using
`HangfireInstrumentationOptions`.

```csharp
using var tracerProvider = Sdk
    .CreateTracerProviderBuilder()
    .AddHangfireInstrumentation(options =>
    {
        options.DisplayNameFunc = job => $"JOB {job.Id}";
        options.Filter = job => job.Id == "Filter this job";
        options.RecordException = true;
    })
    .AddConsoleExporter()
    .Build();
```

When used with
[`OpenTelemetry.Extensions.Hosting`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Extensions.Hosting/README.md),
all configurations to `HangfireInstrumentationOptions`
can be done in the `ConfigureServices` method of your applications `Startup`
class as shown below.

```csharp
// Configure
services.Configure<HangfireInstrumentationOptions>(options =>
{
    options.DisplayNameFunc = job => $"JOB {job.Id}";
    options.Filter = job => job.Id == "Filter this job";
    options.RecordException = true;
});

services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddHangfireInstrumentation()
        .AddConsoleExporter());
```

### RecordException

Configures a value indicating whether the exception will be recorded as
ActivityEvent or not. See
[Semantic Conventions for Exceptions on Spans](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/exceptions/exceptions-spans.md)

```csharp
using var tracerProvider = Sdk
    .CreateTracerProviderBuilder()
    .AddHangfireInstrumentation(options =>
    {
        options.RecordException = true;
    })
    .AddConsoleExporter()
    .Build();
```

### DisplayNameFunc

This option allows changing activity display name.

```C#
using var tracerProvider = Sdk
    .CreateTracerProviderBuilder()
    .AddHangfireInstrumentation(options =>
    {
        options.DisplayNameFunc = job => $"JOB {job.Id}";
    })
    .AddConsoleExporter()
    .Build();
```

If not configured the default is

```C#
$"JOB {BackgroundJob.Job.Type.Name}.{BackgroundJob.Job.Method.Name}"
```

### Filter

This option can be used to filter out activities based on the `BackgroundJob`
being executed. The `Filter` function should return `true` if the telemetry is
to be collected, and `false` if it should not.

The following code snippet shows how to use `Filter` to filter out traces for
job with a specified job id.

```csharp
using var tracerProvider = Sdk
    .CreateTracerProviderBuilder()
    .AddHangfireInstrumentation(options =>
    {
        options.Filter = job => job.Id == "Filter this job";
    })
    .AddConsoleExporter()
    .Build();
```

## Metrics

This instrumentation library collects metrics following the OpenTelemetry
[workflow semantic conventions](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/workflow/workflow-metrics.md).
In Hangfire, each background job execution is modeled as a **workflow task execution**.

### Enabling Metrics

Metrics are enabled by adding Hangfire instrumentation to the `MeterProviderBuilder`:

```csharp
using OpenTelemetry.Metrics;

var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddHangfireInstrumentation()
    .AddConsoleExporter()
    .Build();
```

The above example also sets up the OpenTelemetry Console exporter, which requires
adding the package
[`OpenTelemetry.Exporter.Console`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Console/README.md)
to the application.

### Enabling both Traces and Metrics

To collect both traces and metrics from Hangfire:

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddHangfireInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddHangfireInstrumentation()
        .AddOtlpExporter());
```

### Metrics Configuration

The same `HangfireInstrumentationOptions` class is used to configure both tracing
and metrics behavior.

#### RecordQueueLatency

By default, the instrumentation records only the execution phase duration (time spent
actually running the job). To also track the pending phase duration (time spent waiting
in the queue before execution), enable the `RecordQueueLatency` option:

```csharp
var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddHangfireInstrumentation(options =>
    {
        options.RecordQueueLatency = true;
    })
    .AddConsoleExporter()
    .Build();
```

When enabled, `workflow.execution.duration` is recorded with `workflow.execution.state="pending"`
representing queue wait time, in addition to `workflow.execution.state="executing"` for actual
execution time.

> [!WARNING]
> Enabling `RecordQueueLatency` requires an additional database query per job execution
> to retrieve the enqueue timestamp. In high-throughput scenarios, this may impact
> performance.

### Available Metrics

The following metrics are emitted by this instrumentation:

#### workflow.execution.count

The number of task executions which have been initiated.

| Units          | Instrument Type | Value Type |
|----------------|-----------------|------------|
| `{executions}` | Counter         | `Int64`    |

> [!NOTE]
> This metric does NOT include the `workflow.execution.state` attribute, as state differentiation
> is not needed for counting completed executions.

**Attributes:**

| Attribute                     | Type   | Description                                   | Requirement Level        | Values                  |
|-------------------------------|--------|-----------------------------------------------|--------------------------|-------------------------|
| `workflow.task.name`          | string | Name of the task (Hangfire job method)        | Required                 | e.g., `MyJob.Execute`   |
| `workflow.execution.outcome`  | string | The outcome of executing the task             | Required                 | `success`, `failure`    |
| `workflow.platform.name`      | string | The workflow platform being used              | Recommended              | `hangfire`              |
| `error.type`                  | string | The type of error that occurred               | Conditionally Required¹  | Exception type name     |

¹ Required if and only if the task execution failed.

#### workflow.execution.duration

Duration of an execution grouped by task, state, and result. Records duration for different
execution phases:
- **state=pending**: Time spent waiting in queue (only when `RecordQueueLatency` is enabled)
- **state=executing**: Time spent in actual execution

| Units | Instrument Type | Value Type |
|-------|-----------------|------------|
| `s`   | Histogram       | `Double`   |

> [!NOTE]
> The `workflow.execution.state` attribute is an extension to the current semantic conventions
> to support measuring different execution phases (pending vs executing). This attribute is
> being proposed for inclusion in the official semantic conventions.

**Attributes:**

| Attribute                     | Type   | Description                                   | Requirement Level        | Values                  |
|-------------------------------|--------|-----------------------------------------------|--------------------------|-------------------------|
| `workflow.task.name`          | string | Name of the task (Hangfire job method)        | Required                 | e.g., `MyJob.Execute`   |
| `workflow.execution.outcome`  | string | The outcome of executing the task             | Required                 | `success`, `failure`    |
| `workflow.execution.state`    | string | The execution phase being measured            | Required                 | `pending`, `executing`  |
| `workflow.platform.name`      | string | The workflow platform being used              | Recommended              | `hangfire`              |
| `error.type`                  | string | The type of error that occurred               | Conditionally Required¹  | Exception type name     |

¹ Required if and only if the task execution failed.

#### workflow.execution.status

The number of actively running tasks grouped by task and the current state.

| Units          | Instrument Type  | Value Type |
|----------------|------------------|------------|
| `{executions}` | UpDownCounter    | `Int64`    |

> [!NOTE]
> This metric tracks the current state of job executions. When a job transitions
> to a new state, the previous state is decremented and the new state is incremented.

**Attributes:**

| Attribute                     | Type   | Description                                   | Requirement Level        | Values                                    |
|-------------------------------|--------|-----------------------------------------------|--------------------------|-------------------------------------------|
| `workflow.task.name`          | string | Name of the task (Hangfire job method)        | Required                 | e.g., `MyJob.Execute`                     |
| `workflow.execution.state`    | string | Current state of the execution                | Required                 | `pending`, `executing`, `completed`       |
| `workflow.platform.name`      | string | The workflow platform being used              | Recommended              | `hangfire`                                |
| `error.type`                  | string | The type of error that occurred               | Conditionally Required¹  | Exception type name                       |

¹ Required if and only if the task execution failed.

**Hangfire State Mapping:**

Hangfire job states are mapped to workflow semantic convention states as follows:

| Hangfire State                     | Workflow State |
|------------------------------------|----------------|
| Scheduled, Enqueued, Awaiting      | `pending`      |
| Processing                         | `executing`    |
| Succeeded, Failed, Deleted         | `completed`    |

#### workflow.execution.errors

The number of errors encountered in task executions.

| Units     | Instrument Type | Value Type |
|-----------|-----------------|------------|
| `{error}` | Counter         | `Int64`    |

**Attributes:**

| Attribute                     | Type   | Description                                   | Requirement Level | Values                  |
|-------------------------------|--------|-----------------------------------------------|-------------------|-------------------------|
| `error.type`                  | string | The type of error that occurred               | Required          | Exception type name     |
| `workflow.task.name`          | string | Name of the task (Hangfire job method)        | Required          | e.g., `MyJob.Execute`   |
| `workflow.platform.name`      | string | The workflow platform being used              | Recommended       | `hangfire`              |

#### workflow.count

The number of workflow instances which have been initiated. In Hangfire, this tracks
individual job completions.

| Units          | Instrument Type | Value Type |
|----------------|-----------------|------------|
| `{workflows}`  | Counter         | `Int64`    |

**Attributes:**

| Attribute                     | Type   | Description                                   | Requirement Level        | Values                  |
|-------------------------------|--------|-----------------------------------------------|--------------------------|-------------------------|
| `workflow.definition.name`    | string | Name of the workflow (Hangfire job method)    | Required                 | e.g., `MyJob.Execute`   |
| `workflow.outcome`            | string | The outcome of the workflow                   | Required                 | `success`, `failure`    |
| `workflow.trigger.type`       | string | Type of trigger that initiated the workflow   | Required                 | `api`, `cron`           |
| `workflow.platform.name`      | string | The workflow platform being used              | Recommended              | `hangfire`              |
| `error.type`                  | string | The type of error that occurred               | Conditionally Required¹  | Exception type name     |

¹ Required if and only if the workflow execution failed.

### Semantic Conventions

This instrumentation follows the OpenTelemetry semantic conventions for workflows:

- **workflow.platform.name**: Always set to `"hangfire"`
- **workflow.task.name** / **workflow.definition.name**: Derived from the Hangfire job method (e.g., `ClassName.MethodName`)
- **workflow.trigger.type**: Set to `"cron"` for recurring jobs, `"api"` for fire-and-forget,
  scheduled, and continuation jobs. Only included on workflow-level metrics (`workflow.count`),
  not on execution-level metrics (`workflow.execution.*`)
- **workflow.execution.outcome** / **workflow.outcome**: Set to `"success"` when jobs complete without errors,
  `"failure"` when an exception is thrown
- **workflow.execution.state**: Maps Hangfire states to semantic convention states
  (see table above). Also used to distinguish between pending (queue wait) and executing (actual execution) phases in duration metrics
- **error.type**: Set to the full type name of the exception when a job fails

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Hangfire Project](https://www.hangfire.io/)
* [OpenTelemetry Workflow Semantic Conventions](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/workflow/workflow-metrics.md)
