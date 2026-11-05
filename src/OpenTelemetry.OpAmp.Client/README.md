# OpAMP Client for OpenTelemetry .NET

| Status | |
| ------ | --- |
| Stability | [Development](../../README.md#Development) |
| Code Owners | [@RassK](https://github.com/RassK), [@stevejgordon](https://github.com/stevejgordon) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.OpAmp.Client)](https://www.nuget.org/packages/OpenTelemetry.OpAmp.Client)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.OpAmp.Client)](https://www.nuget.org/packages/OpenTelemetry.OpAmp.Client)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-OpAmp.Client)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-OpAmp.Client)

## Steps to use OpenTelemetry.OpAmp.Client

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.OpAmp.Client`](https://www.nuget.org/packages/OpenTelemetry.OpAmp.Client)
package.

```shell
dotnet add package --prerelease OpenTelemetry.OpAmp.Client
```

### Step 2: Create the OpAMP Client at application startup

```csharp
using OpenTelemetry.OpAmp.Client;
using OpenTelemetry.OpAmp.Client.Settings;

using var client = new OpAmpClient(opts =>
{
    // Set up the OpAMP server connection.
    // Supported options are HTTP (polling) and WebSocket connection.
    opts.ServerUrl = new Uri("wss://localhost:4320/v1/opamp");
    opts.ConnectionType = ConnectionType.WebSocket;

    // Add custom resources to help the server identify your client.
    opts.Identification.AddIdentifyingAttribute("application.name", "my-application");
    opts.Identification.AddNonIdentifyingAttribute("application.version", "1.0.0");
});

// Start the client to send the identification message and get ready to receive messages.
await client.StartAsync();

// The client also supports StopAsync() for proper shutdown and to unregister from the server.
await client.StopAsync();
```

## Supported Features

### Transport and Identification

The client supports HTTP polling and WebSocket transport. Configure the transport
with `ConnectionType` and `ServerUrl`; use `HttpClientFactory` or
`ClientWebSocketFactory` when the underlying client needs custom headers,
handlers, certificates, proxy settings, or other transport-specific options.

### Redirects

HTTP redirects are not followed by default, for both HTTP polling and the
WebSocket handshake; a redirect response is treated as a failure. Set
`AllowRedirects = true` to follow them, only for servers you trust over validated
TLS. Clients returned from `HttpClientFactory` or `ClientWebSocketFactory`
control their own redirect behavior, and WebSocket redirects cannot be disabled
in the `net462` and `netstandard2.0` builds.

Each client instance has an `InstanceUid` and may report identifying and
non-identifying resource attributes through `Identification`. Attribute values
may be strings, integers, doubles, booleans, or collections of those types.

### Message Subscriptions

Server-to-agent messages are delivered to typed `IOpAmpListener<TMessage>`
instances registered with `Subscribe<T>()`. Use `Unsubscribe<T>()` when a
listener should no longer receive messages.

Public server message types include `ServerCapabilitiesMessage`, `FlagsMessage`,
`RemoteConfigMessage`, `CustomCapabilitiesMessage`, and `CustomMessageMessage`.

### Remote Configuration

Enable `RemoteConfiguration.AcceptsRemoteConfig` to advertise that the client
can receive remote configuration from the OpAMP server. Remote configuration is
delivered as a `RemoteConfigMessage`, which contains the configuration hash and
an `AgentConfigMap` of named `AgentConfigFile` values.

Enable `RemoteConfiguration.ReportsRemoteConfigStatus` to report configuration
processing status back to the server. Use `SendRemoteConfigStatus()` with a
`RemoteConfigStatusReport` to report `Applying`, `Applied`, or `Failed` for the
last received configuration hash.

### Effective Configuration Reports

Enable `EffectiveConfigurationReporting.EnableReporting` when the client should
report its current effective configuration to the server. Use
`SendEffectiveConfig()` with one or more `EffectiveConfigFile` values. Call
`FlushAsync()` when the queued report must be sent before continuing.
`FlushAsync()` waits only for messages pending when it is called; messages
queued afterwards (for example heartbeats) do not extend it.

`EffectiveConfigFile` can be created from in-memory content or from a bounded
stream using `CreateFromStream()` or `CreateFromStreamAsync()`.

### Custom Capabilities and Messages

Use `SendCustomCapabilities()` to advertise custom capability names
supported by the client. Use `SendCustomMessage()` to send a typed payload
for one of those capabilities.

By default, the client retains up to 2,048 custom messages with up to 64 MiB of
aggregate data while another message is being sent. The byte limit counts custom
message payloads and any state reports (for example the effective configuration)
sent with them. Configure these limits with
`OpAmpClientSettings.MaxPendingCustomMessages` and
`OpAmpClientSettings.MaxPendingMessageBytes`. `SendCustomMessage()` throws
`InvalidOperationException` when either limit would be exceeded; a rejected
message leaves any pending reports intact. Await `FlushAsync()` before retrying
the rejected message. The byte limit is approximate and is not a strict bound on
memory use.

Server-provided custom capabilities and custom messages are exposed through
`CustomCapabilitiesMessage` and `CustomMessageMessage` subscriptions.

### Heartbeat and Shutdown

Heartbeat reporting is enabled by default. Configure it with
`Heartbeat.IsEnabled` and `Heartbeat.Interval`.

Call `StopAsync()` before disposal when the client should unregister cleanly
from the server. `Dispose()` performs synchronous, best-effort cleanup of
services and transport resources, but it does not send the agent disconnect
message.

## Security Considerations

### Effective Configuration Reporting

When effective configuration reporting is enabled, file contents are read in
full and transmitted verbatim to the OpAMP server. There is no automatic
redaction.

- **Use TLS**: Configure `ServerUrl` with a `wss://` or `https://` scheme so
  that configuration data is encrypted in transit.
- **Avoid sensitive files**: Do not report files that contain secrets such as
  passwords, API tokens, or private keys unless you fully trust the OpAMP server
  and the network path to it.

## References

- [Open Agent Management Protocol](https://opentelemetry.io/docs/specs/opamp/)
