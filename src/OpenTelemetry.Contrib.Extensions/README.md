# OpenTelemetry .NET SDK Extensions

[![nuget](https://img.shields.io/nuget/v/OpenTelemetry.Contrib.Extensions.svg)](https://www.nuget.org/packages/OpenTelemetry.Contrib.Extensions/)

Contains useful extensions to the OpenTelemetry .NET SDK that are not part of
the official OpenTelemetry specification.

## Logging

### AttachLogsToActivityEvent

Adds a log processor which will convert log messages into events and attach them
to the currently running Activity.