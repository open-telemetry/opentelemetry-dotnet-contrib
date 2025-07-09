# Simple Console Exporter for OpenTelemetry .NET

| Status        |           |
| ------------- |-----------|
| Stability     |  [Alpha](../../README.md#alpha)|
| Code Owners   |  [@sgryphon](https://github.com/sgryphon)|

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.SimpleConsole)](https://www.nuget.org/packages/OpenTelemetry.Exporter.SimpleConsole)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.SimpleConsole)](https://www.nuget.org/packages/OpenTelemetry.Exporter.SimpleConsole)

The Simple Console exporter is a basic exporter that writes OpenTelemetry logs
to the console. It is designed to be a simple, human-readable exporter for
debugging and development purposes.

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.SimpleConsole
```

## Configuration

You can configure the `SimpleConsoleExporter` using the
`AddSimpleConsoleExporter` extension method on `LoggerProviderBuilder`.

### Example with `OpenTelemetry.Extensions.Hosting`

```csharp
services.AddOpenTelemetry()
    .WithLogging(builder => builder
        .AddSimpleConsoleExporter(options =>
        {
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
            options.UseUtcTimestamp = true;
        }));
```

### Example with `Sdk.CreateLoggerProviderBuilder`

```csharp
var loggerProvider = Sdk.CreateLoggerProviderBuilder()
    .AddSimpleConsoleExporter(options =>
    {
        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
        options.UseUtcTimestamp = true;
    })
    .Build();
```

## Configuration Options

The `SimpleConsoleExporter` can be configured with the following options:

* `Console`: The `IConsole` implementation to use for writing to the console.
  Defaults to `SystemConsole`, which uses `System.Console`.
* `IncludeSpanId`: A boolean value that indicates whether to include the span
  ID in the output. Defaults to `false`.
* `IncludeTraceId`: A boolean value that indicates whether to include the
  trace ID in the output. Defaults to `true`.
* `TimestampFormat`: The format string to use for timestamps. If `null`, no
  timestamp is written.
* `TraceIdLength`: The length of the trace ID to display. Must be between 1
  and 32. Defaults to 8.
* `UseUtcTimestamp`: A boolean value that indicates whether to use UTC
  timestamps. Defaults to `false`.
