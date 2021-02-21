# WCF Instrumentation for OpenTelemetry .NET - Examples

Project structure:

* Examples.Wcf.Client.Core

  An example of how to call a WCF service with OpenTelemetry instrumentation on
  .NET Core.

* Examples.Wcf.Client.NetFramework

  An example of how to call a WCF service with OpenTelemetry instrumentation on
  .NET Framework.

* Examples.Wcf.Server.NetFramework

  An example of how to implement a WCF service with OpenTelemetry
  instrumentation on .NET Framework.

  Note: There is no .NET Core server example because only the client libraries
  for WCF are available on .NET Core / .NET 5.

* Examples.Wcf.Shared

  Contains the shared contract and request & response objects.