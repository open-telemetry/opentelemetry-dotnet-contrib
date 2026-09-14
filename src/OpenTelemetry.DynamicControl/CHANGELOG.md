# Changelog

## Unreleased

* Added internal `ITelemetryPolicy` interface and `TraceSamplingRatePolicy`
  model and validation.
  ([#4950](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4950))

* Added internal `PolicyKey`, `ProviderRegistrationId`, `PolicyProviderKind`,
  and `PolicyProviderMetadata` identity types for keying policies and
  configured providers.
  ([#5011](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5011))

* Added an internal copy-on-write `PolicyStore` with immutable per-provider
  and store snapshots, sequence-based stale rejection, version-based suppression
  (`PolicyProviderVersion`), metadata pinning, and deterministic
  policy/provider ordering via `PolicyKeyComparer`.
  ([#5017](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5017))

* Added internal `PolicyType` and `PolicyId` identity value types and policy
  readers, which turn a value carried in a policy payload into a validated
  policy model, so that how a policy type is decoded is defined once by that
  policy type rather than by each provider that carries it.
  ([#5122](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5122))

* Added ordered, store-change subscriptions to the internal `PolicyStore` via a
  new `PolicyChangeNotifier`/`PolicyChangeSubscription` pair.
  ([#5135](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5135))

For more details, please refer to the [README](README.md).
