# .NET Remoting Instrumentation for OpenTelemetry .NET

| Status | |
| ------ | --- |
| Stability | [Alpha](../../README.md#beta) |
| Code Owners | [@lewis800](https://github.com/lewis800) |

This package scaffolds the repository wiring for future .NET Remoting instrumentation.

.NET Remoting is a [legacy technology](https://docs.microsoft.com/previous-versions/dotnet/netframework-3.0/kwdt6w2k(v=vs.85))
that should not be used for new .NET applications and [does not exist](https://docs.microsoft.com/dotnet/core/porting/net-framework-tech-unavailable#remoting)
in .NET 6 and later versions.

The initial implementation is intentionally split into a follow-up change so the
project setup, ownership, CI wiring, packaging, and release metadata can be
reviewed separately from the instrumentation code.