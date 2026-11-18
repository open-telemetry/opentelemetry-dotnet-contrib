# Producer statistics fixtures

These are reduced, representative fixtures for the librdkafka 2.4.0 statistics
schema, not complete captures from a running broker. They cover only the fields
used by the producer bridge. The active fixture deliberately includes unknown
fields and topic totals that must not be counted alongside the client totals.

The idle fixture distinguishes an empty timing window (`cnt = 0`) from a sampled
zero. Keep these cases when adding fixtures for newer librdkafka versions.

The Docker-gated integration test validates the mapping against actual native
statistics. Fixture tests alone do not establish compatibility with every version
allowed by the Confluent.Kafka dependency range.
