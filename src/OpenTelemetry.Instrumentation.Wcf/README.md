# WCF Instrumentation for OpenTelemetry .NET

[![nuget](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.Wcf.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Wcf/)
[![nuget](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.Wcf.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Wcf/)

Instruments WCF clients and/or services using implementations of
`IClientMessageInspector` and `IDispatchMessageInspector` respectively.

## Installation

Add the OpenTelemetry.Instrumentation.Wcf package via NuGet.

## OpenTelemetry Configuration

Use the `AddWcfInstrumentation` extension method to add WCF instrumentation to
an OpenTelemetry TracerProvider:

```csharp
using var openTelemetry = Sdk.CreateTracerProviderBuilder()
    .AddWcfInstrumentation()
    .Build();
```

Instrumentation can be configured via options overload for
`AddWcfInstrumentation` method:

```csharp
using var openTelemetry = Sdk.CreateTracerProviderBuilder()
    .AddWcfInstrumentation(options =>
    {
        options.SuppressDownstreamInstrumentation = false;

        // Enable enriching an activity after it is created.
        options.Enrich = (activity, eventName,  message) =>
        {
            var wcfMessage = message as Message;

            // For event names, please refer to string constants in
            // WcfEnrichEventNames class.
            if (eventName == WcfEnrichEventNames.AfterReceiveReply)
            {
                activity.SetTag(
                    "companyname.messagetag",
                    wcfMessage.Properties["CustomProperty"]);
            }
        };
    })
    .Build();
```

## WCF Client Configuration (.NET Framework)

Add the `IClientMessageInspector` instrumentation via a behavior extension on
the clients you want to instrument:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <system.serviceModel>
    <extensions>
      <behaviorExtensions>
        <add name="telemetryExtension" type="OpenTelemetry.Instrumentation.Wcf.TelemetryEndpointBehaviorExtensionElement, OpenTelemetry.Instrumentation.Wcf"   />
      </behaviorExtensions>
    </extensions>
    <behaviors>
      <endpointBehaviors>
        <behavior name="telemetry">
          <telemetryExtension />
        </behavior>
      </endpointBehaviors>
    </behaviors>
    <bindings>
      <netTcpBinding>
        <binding name="netTCPConfig">
          <security mode="None" />
        </binding>
      </netTcpBinding>
    </bindings>
    <client>
      <endpoint address="net.tcp://localhost:9090/Telemetry" binding="netTcpBinding" bindingConfiguration="netTCPConfig" behaviorConfiguration="telemetry" contract="Examples.Wcf.IStatusServiceContract" name="StatusService_Tcp" />
    </client>
  </system.serviceModel>
</configuration>
```

Example project available in
[examples/wcf/client-netframework](../../examples/wcf/client-netframework/)
folder.

## WCF Client Configuration (.NET Core)

Add the `IClientMessageInspector` instrumentation as an endpoint behavior on the
clients you want to instrument:

```csharp
StatusServiceClient client = new StatusServiceClient(binding, remoteAddress);
client.Endpoint.EndpointBehaviors.Add(new TelemetryEndpointBehavior());
```

Example project available in
[examples/wcf/client-core](../../examples/wcf/client-core/) folder.

## WCF Server Configuration (.NET Framework)

### Option 1: Instrument by endpoint

To add the `IDispatchMessageInspector` instrumentation to select endpoints of a
service, use the endpoint behavior extension on the service endpoints you want
to instrument:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <system.serviceModel>
    <extensions>
      <behaviorExtensions>
        <add name="telemetryExtension" type="OpenTelemetry.Instrumentation.Wcf.TelemetryEndpointBehaviorExtensionElement, OpenTelemetry.Instrumentation.Wcf" />
      </behaviorExtensions>
    </extensions>
    <behaviors>
      <endpointBehaviors>
        <behavior name="telemetry">
          <telemetryExtension />
        </behavior>
      </endpointBehaviors>
    </behaviors>
    <bindings>
      <netTcpBinding>
        <binding name="netTCPConfig">
          <security mode="None" />
        </binding>
      </netTcpBinding>
    </bindings>
    <services>
      <service name="Examples.Wcf.Server.StatusService">
        <endpoint binding="netTcpBinding" bindingConfiguration="netTCPConfig" behaviorConfiguration="telemetry" contract="Examples.Wcf.IStatusServiceContract" />
        <host>
          <baseAddresses>
            <add baseAddress="net.tcp://localhost:9090/Telemetry" />
          </baseAddresses>
        </host>
      </service>
    </services>
  </system.serviceModel>
</configuration>
```

Example project available in
[examples/wcf/server-netframework](../../examples/wcf/server-netframework/)
folder.

### Option 2: Instrument by service

To add the `IDispatchMessageInspector` instrumentation for all endpoints of a
service, use the service behavior extension on the services you want to
instrument:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <system.serviceModel>
    <extensions>
      <behaviorExtensions>
        <add name="telemetryExtension" type="OpenTelemetry.Instrumentation.Wcf.TelemetryServiceBehaviorExtensionElement, OpenTelemetry.Instrumentation.Wcf" />
      </behaviorExtensions>
    </extensions>
    <behaviors>
      <serviceBehaviors>
        <behavior name="telemetry">
          <telemetryExtension />
        </behavior>
      </serviceBehaviors>
    </behaviors>
    <bindings>
      <netTcpBinding>
        <binding name="netTCPConfig">
          <security mode="None" />
        </binding>
      </netTcpBinding>
    </bindings>
    <services>
      <service name="Examples.Wcf.Server.StatusService" behaviorConfiguration="telemetry">
        <endpoint binding="netTcpBinding" bindingConfiguration="netTCPConfig" contract="Examples.Wcf.IStatusServiceContract" />
        <host>
          <baseAddresses>
            <add baseAddress="net.tcp://localhost:9090/Telemetry" />
          </baseAddresses>
        </host>
      </service>
    </services>
  </system.serviceModel>
</configuration>
```

## WCF Programmatic Configuration Server and/or Client (.NET Framework + .NET Core)

To add `IDispatchMessageInspector` for servers (.NET Framework only) and/or
`IClientMessageInspector` for clients (.NET Framework & .NET Core)
programmatically, use the `TelemetryContractBehaviorAttribute` on the service
contracts you want to instrument:

```csharp
    [ServiceContract(
        Namespace = "http://opentelemetry.io/",
        Name = "StatusService",
        SessionMode = SessionMode.Allowed)]
    [TelemetryContractBehavior]
    public interface IStatusServiceContract
    {
        [OperationContract]
        Task<StatusResponse> PingAsync(StatusRequest request);
    }
```

## Known issues

WCF library does not provide any extension points to handle exception thrown on
communication (e.g. EndpointNotFoundException). Because of that in case of such
an event the `Activity` will not be stopped correctly. This can be handled in
an application code by catching the exception and stopping the `Activity` manually.

```csharp
    StatusResponse? response = null;
    try
    {
        response = await client.PingAsync(statusRequest).ConfigureAwait(false);
    }
    catch (Exception)
    {
        var activity = Activity.Current;
        if (activity != null && activity.Source.Name.Contains("OpenTelemetry.Instrumentation.Wcf"))
        {
            activity.Stop();
        }
    }
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
