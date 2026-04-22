# Service Fabric Remoting Instrumentation for OpenTelemetry .NET

| Status | |
| ------ | --- |
| Stability | [Beta](../../README.md#beta) |
| Code Owners | [@sablancoleis](https://github.com/sablancoleis) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.ServiceFabricRemoting)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.ServiceFabricRemoting)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.ServiceFabricRemoting)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.ServiceFabricRemoting)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Instrumentation.ServiceFabricRemoting)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Instrumentation.ServiceFabricRemoting)

This is an [Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library)
that instruments [Service Fabric Remoting](https://learn.microsoft.com/azure/service-fabric/service-fabric-reliable-services-communication-remoting),
emitting distributed traces and RPC metrics for both client (outgoing) and
server (incoming) calls.

## Steps to enable OpenTelemetry.Instrumentation.ServiceFabricRemoting

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.ServiceFabricRemoting`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.ServiceFabricRemoting)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Instrumentation.ServiceFabricRemoting
```

### Step 2: Common configuration

These instructions are a modified version of the steps mentioned here:
[`Use an assembly attribute to use the V2 stack`](https://learn.microsoft.com/en-us/azure/service-fabric/service-fabric-reliable-services-communication-remoting#use-an-assembly-attribute-to-use-the-v2-stack)

a) Change the endpoint resource from "ServiceEndpoint" to "ServiceEndpointV2"
   in the service manifest.

```xml
    <Resources>
     <Endpoints>
       <Endpoint Name="ServiceEndpointV2" />
     </Endpoints>
    </Resources>
```

b) Use the Microsoft.ServiceFabric.Services.Remoting.Runtime
.CreateServiceRemotingInstanceListeners
extension method to create remoting listeners.

```csharp
    protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
    {
        return this.CreateServiceRemotingInstanceListeners();
    }
```

c) Mark the assembly that contains the remoting interfaces with a
**TraceContextEnrichedActorRemotingProvider** and/or a
**TraceContextEnrichedServiceRemotingProvider** attribute.
*Note that these attributes are not part of the Service Fabric SDK
and are provided by the OpenTelemetry.Instrumentation.ServiceFabricRemoting package.**

```csharp
   [assembly: TraceContextEnrichedActorRemotingProvider()]
   [assembly: TraceContextEnrichedServiceRemotingProvider()]
```

### Step 3: Distributed tracing

Call the `AddServiceFabricRemotingInstrumentation` extension method on the
`TracerProviderBuilder` to register the OpenTelemetry tracing instrumentation.

```csharp
    using TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ServiceFabricRemoting-Example"))
        .AddServiceFabricRemotingInstrumentation()
        .AddOtlpExporter()
        .Build();
```

This will register the `ServiceFabric.Remoting` ActivitySource and create
spans for incoming and outgoing remoting calls.

It will also automatically inject the trace context and Baggage into the
remoting headers, and extract them on the receiving side.

#### Configuration options

The `AddServiceFabricRemotingInstrumentation` extension method takes an optional
`ServiceFabricRemotingInstrumentationOptions` parameter that offers the following
configurable settings:

- **Filter** - A filter function that can be used to exclude certain remoting
calls from being **traced**. Metrics are always emitted regardless of this
filter, so rate and error-rate dashboards reflect all real traffic.
The function takes a `ServiceRemotingRequest` and returns a boolean value.
If the function returns `true`, the remoting call will be traced.
If the function returns `false`, the remoting call will not be traced.
By default, all remoting calls are traced.

```csharp
    TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ServiceFabricRemoting-Example"))
        .AddServiceFabricRemotingInstrumentation(options =>
        {
            options.Filter = requestMessage =>
            {
                // Exclude remoting calls to a specific method
                IServiceRemotingRequestMessageHeader requestMessageHeader = requestMessage?.GetHeader();
                if (requestMessageHeader?.MethodName == "SomeMethodToIgnore")
                {
                    return false;
                }
                return true;
            };
        })
        .AddOtlpExporter()
        .Build();
```

- **EnrichAtClientFromRequest** - A function that can be used to enrich the span
created for the client side of the remoting call.
The function takes a `Activity` and a `ServiceRemotingRequest` and returns the `Activity`.
By default, the client span is enriched with the service interface name
and method name.

```csharp
    TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ServiceFabricRemoting-Example"))
        .AddServiceFabricRemotingInstrumentation(options =>
        {
            options.EnrichAtClientFromRequest = (activity, requestMessage) =>
            {
                // Add custom attributes to the client span
                activity.SetTag("CustomAttribute", "CustomValue");
                return activity;
            };
        })
        .AddOtlpExporter()
        .Build();
```

- **EnrichAtServerFromRequest** - A function that can be used to enrich the span
created for the server side of the remoting call.
The function takes a `Activity` and a `ServiceRemotingRequest` and returns the `Activity`.
By default, the server span is enriched with the service interface
name and method name.

```csharp
    TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ServiceFabricRemoting-Example"))
        .AddServiceFabricRemotingInstrumentation(options =>
        {
            options.EnrichAtServerFromRequest = (activity, requestMessage) =>
            {
                // Add custom attributes to the server span
                activity.SetTag("CustomAttribute", "CustomValue");
                return activity;
            };
        })
        .AddOtlpExporter()
        .Build();
```

- **AddExceptionAtClient** - Gets or sets a value indicating whether
the exception will be recorded at the client as an `ActivityEvent` or not.

```csharp
    TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ServiceFabricRemoting-Example"))
        .AddServiceFabricRemotingInstrumentation(options =>
        {
            options.AddExceptionAtClient = true;
        })
        .AddOtlpExporter()
        .Build();
```

- **AddExceptionAtServer** - Gets or sets a value indicating whether
the exception will be recorded at the server as an `ActivityEvent` or not

```csharp
    TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ServiceFabricRemoting-Example"))
        .AddServiceFabricRemotingInstrumentation(options =>
        {
            options.AddExceptionAtServer = true;
        })
        .AddOtlpExporter()
        .Build();
```

### Step 4: Metrics

Call the `AddServiceFabricRemotingInstrumentation` extension method on the
`MeterProviderBuilder` to register the OpenTelemetry metrics instrumentation.

```csharp
using MeterProvider meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ServiceFabricRemoting-Example"))
    .AddServiceFabricRemotingInstrumentation()
    .AddOtlpExporter()
    .Build();
```

Two RPC duration histograms are emitted per the OpenTelemetry
[RPC metrics semantic conventions](https://github.com/open-telemetry/semantic-conventions/blob/v1.40.0/docs/rpc/rpc-metrics.md):

- `rpc.server.call.duration` - histogram of incoming remoting call durations
  (unit: `s`)
- `rpc.client.call.duration` - histogram of outgoing remoting call durations
  (unit: `s`)

Both histograms are tagged with:

- `rpc.system.name` = `"service_fabric_remoting"`
- `rpc.method` - the method name from the remoting request header
- `error.type` - the exception type fully-qualified name, set only when the
  call throws

The client histogram additionally includes `server.address` sourced from
`ResolvedServicePartition.ServiceName` (for example, `"fabric:/MyApp/MyService"`).

The following RPC semantic convention attributes are intentionally **not**
populated:

- **`rpc.response.status_code`** - Service Fabric Remoting has no native
  status-code concept; failures are serialized exceptions, and the failure
  axis is already captured via the `error.type` tag.
- **`server.port`** - Service Fabric assigns dynamic ports that change on
  failover or restart, and traffic typically flows through a reverse proxy
  or load balancer where the replica port is not visible to callers.
  `server.address` uses the stable logical SF service URI instead, which is
  how SF services are actually identified in practice.

The metrics instrumentation has no configuration options: metrics are emitted
automatically for all request/response calls on both Service and Actor
remoting paths. One-way calls (`SendOneWay`) are not recorded, consistent
with the tracing instrumentation.

## References

- [Azure Service Fabric documentation](https://learn.microsoft.com/azure/service-fabric/)
- [OpenTelemetry Project](https://opentelemetry.io/)
