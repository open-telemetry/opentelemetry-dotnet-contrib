# Changelog

## 1.0.0-alpha.1

Released 2025-Jul-10

* This is the first release of the `OpenTelemetry.Exporter.SimpleConsole`
  package.
* Drop-in replacement for the default .NET console log provider, but as an
  OpenTelemetry exporter.
* Currently only supports logging.
* Coloured level indicators, source, event ID, and formatted message
* Additionally outputs the trace ID / truncated trace ID (and span ID if
  configured)
* Output exceptions if needed
* Optionally output a timestamp prefix
* The exporter is thread-safe.
