# .NET Remoting Instrumentation for OpenTelemetry .NET

| Status | |
| ------ | --- |
| Stability | [Alpha](../../README.md#alpha) |
| Code Owners | [@lewis800](https://github.com/lewis800) |

[![NuGet version badge](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.Remoting)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Remoting)
[![NuGet download count badge](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.Remoting)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Remoting)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib/branch/main/graphs/badge.svg?flag=unittests-Instrumentation.Remoting)](https://app.codecov.io/gh/open-telemetry/opentelemetry-dotnet-contrib?flags[0]=unittests-Instrumentation.Remoting)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments [.NET Remoting](https://docs.microsoft.com/previous-versions/dotnet/netframework-3.0/72x4h507(v=vs.85))
and collects telemetry about incoming and outgoing requests on client
and server objects.

.NET Remoting is a [legacy technology](https://docs.microsoft.com/previous-versions/dotnet/netframework-3.0/kwdt6w2k(v=vs.85))
that shouldn't be used for new .NET applications and [doesn't exist](https://docs.microsoft.com/dotnet/core/porting/net-framework-tech-unavailable#remoting)
in .NET 6 and later versions. However, if you do have a legacy application you are
looking to instrument, consider using this package.

## References

* [.NET Remoting Overview](https://docs.microsoft.com/previous-versions/dotnet/articles/ms973857(v=msdn.10))
* [Remoting Sinks and Dynamic Sinks](https://docs.microsoft.com/previous-versions/dotnet/netframework-1.1/xec2wbt4(v=vs.71))
* [OpenTelemetry Project](https://opentelemetry.io/)
