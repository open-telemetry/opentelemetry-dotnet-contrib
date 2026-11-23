# Changelog

## Unreleased

* Initial release of an Azure Data Explorer (Kusto) exporter for traces,
  metrics and logs, compatible with the table schemas used by the
  OpenTelemetry Collector `azuredataexplorerexporter`. Data is ingested through
  the Kusto REST APIs without a dependency on the Kusto SDK, and the exporter
  is compatible with trimming and native AOT.
  ([#6112](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/6112))
