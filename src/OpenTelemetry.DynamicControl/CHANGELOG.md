# Changelog

## Unreleased

* Added internal `ITelemetryPolicy` interface and `TraceSamplingRatePolicy`
  model and validation.
  ([#4950](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4950))

* Added internal `PolicyKey`, `SourceRegistrationId`, `PolicySourceKind`,
  and `PolicySourceMetadata` identity types for keying policies and
  configured sources.
  ([#5011](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5011))

* Added an internal copy-on-write `PolicyStore` with immutable per-source
  and store snapshots, sequence-based stale rejection, version-based suppression
  (`PolicySourceVersion`), metadata pinning, and deterministic
  policy/source ordering via `PolicyKeyComparer`.
  ([#5017](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5017))

For more details, please refer to the [README](README.md).
