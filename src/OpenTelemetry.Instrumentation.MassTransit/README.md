# MassTransit Instrumentation for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.MassTransit.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.MassTransit)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.MassTransit.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.MassTransit)

Automatically instruments
[DiagnosticSource](https://masstransit-project.com/advanced/monitoring/diagnostic-source.html)
events emitted by [MassTransit](https://masstransit-project.com/) library.

## Installation

```shell
dotnet add package OpenTelemetry.Instrumentation.MassTransit
```

## Configuration

ASP.NET Core instrumentation example:

```csharp
// Add MassTransit inside ConfigureServices
services.AddMassTransit(x =>
{
    x.AddConsumer<OrderConsumer>();

    x.AddBus(context => Bus.Factory.CreateUsingRabbitMq(cfg =>
    {
        cfg.Host("rabbitmq://localhost");

        cfg.ReceiveEndpoint("submit-order", ep =>
        {
            ep.PrefetchCount = 16;
            ep.UseMessageRetry(r => r.Interval(2, 100));

            ep.ConfigureConsumer<OrderConsumer>(context);
        });
    }));
});
services.AddMassTransitHostedService();

// Add OpenTelemetry and MassTransit instrumentation
services.AddOpenTelemetrySdk(x =>
{
    x.AddMassTransitInstrumentation();
    x.UseJaegerExporter(config => {
      // Configure Jaeger
    });
});
```

## Filter traced operations

For example you can trace only consume and handle operations using this snippet:

```csharp
// ...
using OpenTelemetry.Instrumentation.MassTransit.Implementation;
// ...
x.AddMassTransitInstrumentation(
    opts =>
        opts.TracedOperations = new HashSet<string>(new[] {
            OperationName.Consumer.Consume,
            OperationName.Consumer.Handle
}));
```

For full operation list please see: [OperationName](OperationName.cs).

All operations are enabled by default.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [MassTransit Project](https://masstransit-project.com/)
