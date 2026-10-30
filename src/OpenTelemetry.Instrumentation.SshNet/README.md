# SSH.NET Instrumentation for OpenTelemetry

| Status | |
| ------ | --- |
| Stability | [Development](../../README.md#development) |
| Code Owners | [@YoussefElshemi](https://github.com/YoussefElshemi) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.SshNet)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.SshNet)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.SshNet)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.SshNet)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Instrumentation.SshNet)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Instrumentation.SshNet)

This is an
[Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments [SSH.NET](https://github.com/sshnet/SSH.NET)
(`Renci.SshNet`) and collects telemetry about outgoing SFTP and SSH
operations.

SSH.NET has no built-in `ActivitySource` or diagnostic hooks, and its
operations are not virtual, so this library works by **wrapping** an
`ISftpClient` / `ISshClient` in an instrumented decorator.

## Steps to enable OpenTelemetry.Instrumentation.SshNet

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.SshNet`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.SshNet)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Instrumentation.SshNet --prerelease
```

### Step 2: Enable SSH.NET Instrumentation at application startup

SSH.NET instrumentation must be enabled at application startup.

#### Traces

The following example demonstrates adding SSH.NET traces instrumentation
to a console application. This example also sets up the OpenTelemetry Console
exporter, which requires adding the package
[`OpenTelemetry.Exporter.Console`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Console/README.md)
to the application.

```csharp
using OpenTelemetry.Trace;

public class Program
{
    public static void Main(string[] args)
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSshNetInstrumentation()
            .AddConsoleExporter()
            .Build();
    }
}
```

#### Metrics

The following example demonstrates adding SSH.NET metrics instrumentation
to a console application. This example also sets up the OpenTelemetry Console
exporter, which requires adding the package
[`OpenTelemetry.Exporter.Console`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Console/README.md)
to the application.

```csharp
using OpenTelemetry.Metrics;

public class Program
{
    public static void Main(string[] args)
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSshNetInstrumentation()
            .AddConsoleExporter()
            .Build();
    }
}
```

### Step 3: Wrap your clients

Only operations called through a wrapped client are instrumented:

```csharp
using OpenTelemetry.Instrumentation.SshNet;
using Renci.SshNet;

using ISftpClient sftp = new SftpClient("sftp.example.com", "user", "password")
    .WithOpenTelemetry();

await sftp.ConnectAsync(cancellationToken);
await sftp.UploadFileAsync(stream, "/inbox/report.csv", cancellationToken);
```

`ISshClient` works the same way:

```csharp
using ISshClient ssh = new SshClient(connectionInfo).WithOpenTelemetry();
ssh.Connect();
ssh.RunCommand("uptime");
```

Disposing the wrapper disposes the inner client.

## Telemetry

### Spans

Spans have kind `Client` and are named `{protocol} {operation}`, e.g.
`sftp UploadFile`, `sftp ListDirectory`, `ssh RunCommand`, `sftp Connect`.
Sync and async variants of a method produce the same operation name.

| Attribute | Description | Example |
| --- | --- | --- |
| `network.protocol.name` | `sftp` or `ssh` | `sftp` |
| `ssh.operation.name` | SSH.NET operation | `UploadFile` |
| `server.address` | Host from `ConnectionInfo` | `sftp.example.com` |
| `server.port` | Port from `ConnectionInfo` | `22` |
| `file.path` | Remote path[^1] | `/inbox/report.csv` |
| `sftp.target.path` | Destination of rename / symlink[^1] | `/archive/report.csv` |
| `sftp.transfer.bytes` | Bytes moved by `UploadFile` / `DownloadFile`[^2] | `1048576` |
| `ssh.command.text` | Command text[^3] | `uptime` |
| `ssh.command.exit_code` | Exit status of `RunCommand` | `0` |
| `error.type` | Exception type full name on failure | `Renci.SshNet.Common.SftpPathNotFoundException` |

[^1]: Only included when `RecordFilePath` is enabled
[^2]: Only included for seekable streams
[^3]: Only included when `RecordCommandText` is enabled

Failed operations get status `Error` with the exception message as the
description.

### List of metrics produced

| Name | Instrument Type | Unit | Description | Attributes |
| --- | --- | --- | --- | --- |
| `ssh.client.operation.duration` | Histogram | `s` | Duration of SSH and SFTP client operations. | `network.protocol.name`, `ssh.operation.name`, `server.address`, `server.port`, `error.type`[^4] |

[^4]: `error.type` is only included when an error occurs

File paths and command text are never metric attributes.

## Advanced configuration

This instrumentation can be configured to change the default behavior by using
`SshNetInstrumentationOptions`, passed per client to `WithOpenTelemetry`.

### RecordFilePath

This option controls whether remote paths are recorded as the `file.path` and
`sftp.target.path` span attributes.

The default value is `true` and can be changed by the code like below.

```csharp
var client = new SftpClient(connectionInfo)
    .WithOpenTelemetry(options => options.RecordFilePath = false);
```

### RecordCommandText

This option controls whether the text of commands executed with `RunCommand`
is recorded as the `ssh.command.text` span attribute. Command text may contain
sensitive data, so it is not recorded by default.

The default value is `false` and can be changed by the code like below.

```csharp
var client = new SshClient(connectionInfo)
    .WithOpenTelemetry(options => options.RecordCommandText = true);
```

### RecordException

This option controls whether an `exception` event is added to the span when an
operation fails.

The default value is `false` and can be changed by the code like below.

```csharp
var client = new SftpClient(connectionInfo)
    .WithOpenTelemetry(options => options.RecordException = true);
```

## Limitations

* Only operations called through the wrapper are traced.
* Methods returning a stream, reader or writer (`Open*`, `Create*`,
  `AppendText`, `ReadLines`) are traced only for opening the file; reads and
  writes on the returned stream are not.
* `SshCommand` is sealed, so commands created with `CreateCommand` and
  executed later are not traced. Use `RunCommand` to get a span.
* The legacy `Begin*`/`End*` APM methods, shells and port forwarding are
  passed through uninstrumented.
* Because the wrapper implements SSH.NET's interfaces, a newer SSH.NET that
  adds interface members requires a matching release of this package.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [SSH.NET](https://github.com/sshnet/SSH.NET)
