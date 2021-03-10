# gRPC Core-based Client and Server Interceptors for OpenTelemetry .NET

Adds OpenTelemetry instrumentation for gRPC Core-based client and server calls.

gRPC Core is the predecessor to ASP.NET Core gRPC. See <https://github.com/grpc/grpc/tree/master/src/csharp>

For ASP.NET Core gRPC client instrumentation see <https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Instrumentation.GrpcNetClient/README.md>

For ASP.NET Core gRPC server instrumentation is bundled within the AspNetCore
instrumentation.

Each inbound or outbound gRPC call will generate a Span which follows the
semantic RPC specification <https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/rpc.md>

## Installation

```shell
dotnet add package OpenTelemetry.Contrib.Instrumentation.GrpcCore
```

## Configuration

ASP.NET Core instrumentation example:

```csharp
// Add OpenTelemetry and gRPC Core instrumentation
services.AddOpenTelemetryTracing(x =>
{
    x.AddGrpcCoreInstrumentation();
    ...
    // Add exporter, etc.
});
```

Once configured, the OpenTelemetry SDK will listen for the activities created
by the client and server interceptors.

Create a client interceptor:

```csharp
var clientInterceptor = new ClientTracingInterceptor(new ClientTracingInterceptorOptions());
```

Create a server interceptor:

```csharp
var serverInterceptor = new ServerTracingInterceptor(new ServerTracingInterceptorOptions());
```

Then simply add them as you would any other gRPC Core interceptor.
