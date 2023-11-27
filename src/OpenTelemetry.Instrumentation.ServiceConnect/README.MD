# ServiceConnect Instrumentation for OpenTelemetry .NET

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.ServiceConnect)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.ServiceConnect)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.ServiceConnect)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.ServiceConnect)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments
[ServiceConnect](https://github.com/R-Suite/ServiceConnect-CSharp)
and collects traces about published and received messages.

## Steps to enable OpenTelemetry.Instrumentation.ServiceConnect

### Step 1: Install Package

Add a reference to the [`OpenTelemetry.Instrumentation.ServiceConnect`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.ServiceConnect) package.

```shell
dotnet add package OpenTelemetry.Instrumentation.ServiceConnect
```

### Step 2: Enable ServiceConnect Instrumentation

ServiceConnect instrumentation must be enabled at application startup using the `AddServiceConnectInstrumentation` extension method on the `TracerProviderBuilder` instance.

```csharp
using OpenTelemetry.Trace;

services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
        tracerProviderBuilder.AddServiceConnectInstrumentation());
```

## Advanced configuration

This instrumentation can be configured to change the default behavior by using the `ServiceConnectInstrumentationOptions` parameter on the `AddServiceConnectInstrumentation` extension method.

```csharp
using OpenTelemetry.Trace;

services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
        tracerProviderBuilder.AddServiceConnectInstrumentation(options =>
        {
            options.EnrichWithMessage = (activity, message) =>
            {
                activity.SetTag("message.correlationid", message.CorrelationId);
            };
            options.EnrichWithMessageBytes = (activity, message) =>
            {
                activity.SetTag("message.correlationid", JsonConvert.DeserializeObject<Message>(Encoding.UTF8.GetString(message))?.CorrelationId.ToString());
            };
        }));
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
