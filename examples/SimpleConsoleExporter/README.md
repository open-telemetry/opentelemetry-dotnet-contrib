# SimpleConsoleExporter Example for OpenTelemetry .NET

Project structure:

* Examples.SimpleConsoleExporter

This example demonstrates how to use the SimpleConsoleExporter for
OpenTelemetry logging in a .NET Generic Host application.

The SimpleConsoleExporter is designed as a drop in replacement for the
default .NET console logger provider, and the example allows you to
compare their output.

The Worker logs messages at all levels, uses structured logging, exception
logging, logging with scopes, custom state, and ActivitySource tracing.
All output is sent to the console using the selected logger.

## Usage

You can compare the output with the default logger provider by passing the
`--logger` flag, e.g. `--logger DEFAULT`:

* nothing / `OTEL-SIMPLECONSOLE`: OpenTelemetry SimpleConsoleExporter
* `DEFAULT` / `DOTNET`: .NET Console logger provider

You can also compare with other formats, such as the original OTEL console
logger or other formatters:

* `OTEL-CONSOLE`: OpenTelemetry ConsoleExporter
* `DOTNET-JSON`: .NET Console logger provider (JSON formatter)
* `DOTNET-SYSTEMD`: .NET Console logger provider (Systemd/Syslog formatter,
  .NET 8+)

### Timestamp Output

You can add a timestamp to each log line by passing the `--timestamp` argument
with a format string (e.g., `"HH:mm:ss "`). This applies to both the .NET
default console and the OTEL simple console exporter.

### Example commands

```shell
dotnet run --project examples/SimpleConsoleExporter/Examples.SimpleConsoleExporter.csproj -- --timestamp "HH:mm:ss "

dotnet run --project examples/SimpleConsoleExporter/Examples.SimpleConsoleExporter.csproj -- --logger DEFAULT --timestamp "HH:mm:ss "

dotnet run --project examples/SimpleConsoleExporter/Examples.SimpleConsoleExporter.csproj -- --logger OTEL-CONSOLE

dotnet run --project examples/SimpleConsoleExporter/Examples.SimpleConsoleExporter.csproj -- --logger DOTNET-JSON

dotnet run --project examples/SimpleConsoleExporter/Examples.SimpleConsoleExporter.csproj -- --logger DOTNET-SYSTEMD
```
