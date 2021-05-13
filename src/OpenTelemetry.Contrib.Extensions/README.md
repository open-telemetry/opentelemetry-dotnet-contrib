# OpenTelemetry .NET SDK Extensions

Contains useful extensions to the OpenTelemetry .NET SDK that are not part of
the official OpenTelemetry specification.

## Logging

### AttachLogsToActivityEvent

Adds a log processor which will convert log messages into events and attach them
to the currently running Activity.