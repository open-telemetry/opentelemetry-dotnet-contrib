# OpenTelemetry .NET SDK preview features and extensions

[![nuget](https://img.shields.io/nuget/v/OpenTelemetry.Contrib.Preview.svg)](https://www.nuget.org/packages/OpenTelemetry.Contrib.Preview/)

Contains useful features and extensions to the OpenTelemetry .NET SDK that are
not part of the official OpenTelemetry specification but might be added in the
future.

## Logging

### AttachLogsToActivityEvent

Adds a log processor which will convert log messages into events and attach them
to the currently running Activity.
