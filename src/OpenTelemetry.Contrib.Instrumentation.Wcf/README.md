# WCF Instrumentation for OpenTelemetry .NET

[![nuget](https://img.shields.io/nuget/v/OpenTelemetry.Contrib.Instrumentation.Wcf.svg)](https://www.nuget.org/packages/OpenTelemetry.Contrib.Instrumentation.Wcf/)

Instruments WCF clients and/or services using implementations of
`IClientMessageInspector` and `IDispatchMessageInspector` respectively.

## Installation

Add the OpenTelemetry.Contrib.Instrumentation.Wcf package via NuGet.

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
        options.Enrich = (activity,eventName,message) =>    
        {
            // For event names, please refer to string constants in 
            // WcfEnrichEventNames class.
            if(eventName == WcfEnrichEventNames.AfterReceiveRequest)  
            {
                activity.AddTag("companyname.customtag", Guid.NewGuid());
            }
        }
    })
    .Build();
```

## WCF Client Configuration (.NET Framework)

Add the `IClientMessageInspector` instrumentation via a behavior extension on
the clients you want to instrument:

<!-- markdownlint-disable MD013 -->
```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <system.serviceModel>
    <extensions>
      <behaviorExtensions>
        <add name="telemetryExtension" type="OpenTelemetry.Contrib.Instrumentation.Wcf.TelemetryBehaviourExtensionElement, OpenTelemetry.Contrib.Instrumentation.Wcf"   />
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
<!-- markdownlint-enable MD013 -->

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

Add the `IDispatchMessageInspector` instrumentation via a behavior extension on
the services you want to instrument:

<!-- markdownlint-disable MD013 -->
```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <system.serviceModel>
    <extensions>
      <behaviorExtensions>
        <add name="telemetryExtension" type="OpenTelemetry.Contrib.Instrumentation.Wcf.TelemetryBehaviourExtensionElement, OpenTelemetry.Contrib.Instrumentation.Wcf" />
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
<!-- markdownlint-enable MD013 -->

Example project available in
[examples/wcf/server-netframework](../../examples/wcf/server-netframework/)
folder.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
