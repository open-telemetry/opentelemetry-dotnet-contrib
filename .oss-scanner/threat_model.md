# Threat model

## What this project does and where untrusted input enters

- This project is intended to provide helpful libraries and standalone
  OpenTelemetry-based utilities that don't fit the express scope of the .NET SDK
  for OpenTelemetry - it contains additional exporters, instrumentation, resource
  providers and libraries for Dynamic Control and OpAMP support. There are also
  experimental extensions for additional functionality and integrations that
  may be added to the core OpenTelemetry SDK in the future.
- Untrusted input enters through the data that the SDK receives from the
  applications and libraries it instruments. This data can come through either
  user-provided data to attributes or can come through untrusted input in the
  case of HTTP requests if used for baggage and trace propagation.

## Components that matter most / least

- The following components matter most:
  - OpenTelemetry.Exporter.Geneva - contains the implementation of the Geneva
    exporter, which serializes telemetry (including untrusted attribute values)
    into custom binary formats sent over ETW, Unix domain sockets or Linux
    user_events.
  - OpenTelemetry.Exporter.OneCollector - contains the implementation of the
    OneCollector exporter, which serializes telemetry into JSON and sends it to
    a remote endpoint over HTTP.
  - OpenTelemetry.Extensions.AWS - contains the AWS X-Ray propagator, which parses
    the untrusted `X-Amzn-Trace-Id` header, and the AWS X-Ray ID generator.
  - OpenTelemetry.Instrumentation.AspNetCore - instruments incoming HTTP requests
    to ASP.NET Core applications. Request headers (including trace context and
    baggage), paths, query strings and route values are attacker-controlled and
    are used to create spans, metrics and their attributes.
  - OpenTelemetry.Instrumentation.AspNet and
    OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule - instruments
    incoming HTTP requests to ASP.NET applications on .NET Framework, including
    extraction of trace context and baggage from request headers.
  - OpenTelemetry.Instrumentation.AWS, OpenTelemetry.Instrumentation.AWSLambda
    and OpenTelemetry.Instrumentation.ConfluentKafka - instruments messaging and
    cloud operations, including extraction of trace context from message headers,
    message attributes and event payloads that may originate from untrusted producers.
  - OpenTelemetry.Instrumentation.GrpcNetClient, OpenTelemetry.Instrumentation.GrpcCore
    and OpenTelemetry.Instrumentation.Wcf - instruments RPC calls,
    including extraction of trace context from untrusted request metadata/headers
    for server-side instrumentation.
  - OpenTelemetry.Instrumentation.Http - instruments outgoing HTTP requests made
    with `HttpClient` and `HttpWebRequest`, including injection of trace context
    and redaction of sensitive values (such as query strings) from URLs.
  - OpenTelemetry.Instrumentation.Owin - instruments incoming HTTP requests to
    OWIN applications, including extraction of trace context from request headers.
  - OpenTelemetry.Instrumentation.SqlClient and
    OpenTelemetry.Instrumentation.EntityFrameworkCore - instruments database
    calls. These rely on the shared SQL sanitizer (`src/Shared/SqlProcessor.cs`)
    to remove literal values from query text before it is recorded as an
    attribute, so a bypass of the sanitizer can leak sensitive data into telemetry.
  - OpenTelemetry.Instrumentation.StackExchangeRedis - instruments Redis commands,
    which may contain keys and values supplied by untrusted users.
  - OpenTelemetry.OpAmp.Client - contains the implementation of an OpAMP client
    that connects to a remote OpAMP server over HTTP or WebSockets and processes
    protobuf messages received from it.
  - OpenTelemetry.Resources.* - contains resource detectors that read data from
    the local environment (such as environment variables and files under `/proc`)
    or from cloud metadata endpoints (such as AWS, Azure and GCP) to populate
    resource attributes.
- The following components matter less as they are not yet declared as stable or
  have less usage than the components listed above:
  - OpenTelemetry.DynamicControl - contains an incubating, experimental
    implementation of dynamic control based on telemetry policies that does not
    yet expose any public APIs.
  - OpenTelemetry.Extensions - contains preview features such as processors that
    copy baggage (which may be received from untrusted callers) onto telemetry
    and additional samplers.
  - OpenTelemetry.Instrumentation.ServiceFabricRemoting - instruments RPC calls,
    including extraction of trace context from untrusted request metadata/headers
    for server-side instrumentation.
  - OpenTelemetry.PersistentStorage.Abstractions and
    OpenTelemetry.PersistentStorage.FileSystem - contains the implementation of
    on-disk persistent storage used by exporters to buffer telemetry, which
    creates, reads and deletes files on the local file system.
  - OpenTelemetry.Sampler.AWS - contains a sampler that periodically retrieves
    sampling rules from a remote AWS X-Ray endpoint and matches them (including
    wildcard patterns) against attributes of incoming requests.
- The following components matter least as they are not designed for production
  use:
  - OpenTelemetry.SemanticConventions - contains generated constants for the
    OpenTelemetry semantic conventions and no runtime logic.
  - The projects under the `examples` and `test` directories, which are only
    intended for demonstration and testing purposes.

## How to exercise it

- The examples directory contains some example usage applications.
- The test directory contains all of the unit and integration tests, including
  fuzz tests.

## How you rate severity

No special rules for rating severity are provided; use standard security severity
guidelines.

## Anything to leave alone

- Ignore any projects that are deprecated or intended only for development or testing
  purposes unless they are high or critical severity.
