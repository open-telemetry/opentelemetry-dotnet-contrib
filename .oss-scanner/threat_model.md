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

## Repository structure

- Each directory under `src` is a separate, independently versioned NuGet
  package with its own owners, changelog and release cadence. The packages are
  loosely coupled: most have no dependency on any other package in this
  repository, and a small number depend on one or two others (noted per package
  below). Each package should be scanned and assessed separately, taking into
  account any in-repository dependencies it has.
- `src/Shared` is not a package. It contains helper source files that are
  compiled directly into the packages that reference them (using
  `<Compile Include="..."/>`). A defect in a shared file therefore affects every
  package that links it. Notable shared files include:
  - `SqlProcessor.cs` and `SqlParameterProcessor.cs` - sanitize SQL query text
    before it is recorded as an attribute. Used by
    `OpenTelemetry.Instrumentation.SqlClient` and
    `OpenTelemetry.Instrumentation.EntityFrameworkCore`.
  - `RedactionHelper.cs` and `RequestDataHelper.cs` - redact sensitive values
    (such as query strings) from URLs and normalize HTTP request data. Used by
    the HTTP-related instrumentation packages.
  - `HttpClientHelpers.cs`, `ServerCertificateValidationHandler.cs` and
    `ServerCertificateValidationProvider.cs` - create HTTP clients and perform
    custom TLS server certificate validation for outbound requests.
  - `CgroupContainerIdParser.cs` - parses container IDs from `/proc` cgroup
    files.
  - `AWS/*.cs` - shared AWS semantic convention helpers.
- The `test` directory contains the unit and integration tests (including fuzz
  tests) and the `examples` directory contains sample applications. Neither
  contains shipped code.

## Packages

The primary purpose(s) of each package, grouped by type, together with the
stability level declared in its README and any dependencies on other packages
in this repository.

### Exporters

- `OpenTelemetry.Exporter.Geneva` (stable) - exports telemetry to the Microsoft
  Geneva monitoring platform. Serializes telemetry (including untrusted
  attribute values) into binary formats (MessagePack, TraceLogging and OTLP
  protobuf) and writes them to ETW on Windows, or Unix domain sockets or
  `user_events` on Linux.
- `OpenTelemetry.Exporter.InfluxDB` (alpha) - exports metrics to an InfluxDB
  server over HTTP using the InfluxDB line protocol.
- `OpenTelemetry.Exporter.OneCollector` (stable) - exports logs to the
  Microsoft OneCollector service. Serializes telemetry into JSON and sends it to
  a remote endpoint over HTTP.

### Extensions

- `OpenTelemetry.Extensions` (beta) - preview features and extensions for the
  OpenTelemetry SDK, including processors that copy baggage (which may be
  received from untrusted callers) onto spans and logs, additional samplers,
  and an auto-flush processor.
- `OpenTelemetry.Extensions.AWS` (stable) - the AWS X-Ray trace context
  propagator, which parses the untrusted `X-Amzn-Trace-Id` header, and the AWS
  X-Ray trace ID generator. Depended on by `OpenTelemetry.Instrumentation.AWS`
  and `OpenTelemetry.Instrumentation.AWSLambda`.
- `OpenTelemetry.Extensions.Enrichment` (beta) - abstractions and a processor
  for enriching traces with additional attributes. Depended on by
  `OpenTelemetry.Extensions.Enrichment.AspNetCore` and
  `OpenTelemetry.Extensions.Enrichment.Http`.
- `OpenTelemetry.Extensions.Enrichment.AspNetCore` (development) - enriches
  ASP.NET Core server spans with additional attributes derived from the
  incoming request. Depends on `OpenTelemetry.Extensions.Enrichment` and
  `OpenTelemetry.Instrumentation.AspNetCore`.
- `OpenTelemetry.Extensions.Enrichment.Http` (development) - enriches
  `HttpClient` spans with additional attributes derived from the outgoing
  request and response. Depends on `OpenTelemetry.Extensions.Enrichment` and
  `OpenTelemetry.Instrumentation.Http`.

### Instrumentation - incoming requests (server-side)

- `OpenTelemetry.Instrumentation.AspNet` (stable) - instruments incoming HTTP
  requests to ASP.NET applications on .NET Framework, creating spans and
  metrics from request headers (including trace context and baggage), paths,
  query strings and route values, all of which are attacker-controlled. Depends
  on `OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule`.
- `OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule` (stable) - an
  `IHttpModule` for ASP.NET on .NET Framework that starts and stops an
  `Activity` for each incoming request and extracts trace context and baggage
  from request headers. Depended on by `OpenTelemetry.Instrumentation.AspNet`.
- `OpenTelemetry.Instrumentation.AspNetCore` (stable) - instruments incoming
  HTTP and gRPC requests to ASP.NET Core applications, creating spans and
  metrics from request headers (including trace context and baggage), paths,
  query strings and route values, all of which are attacker-controlled.
- `OpenTelemetry.Instrumentation.Owin` (beta) - instruments incoming HTTP
  requests to OWIN applications, including extraction of trace context from
  request headers.
- `OpenTelemetry.Instrumentation.Remoting` (alpha) - instruments .NET Remoting
  calls on .NET Framework, including propagating trace context through the
  remoting call context.
- `OpenTelemetry.Instrumentation.ServiceFabricRemoting` (beta) - instruments
  Service Fabric Remoting calls, including extraction of trace context from
  untrusted request headers for server-side instrumentation.

### Instrumentation - RPC (client and server)

- `OpenTelemetry.Instrumentation.GrpcCore` (beta) - client and server
  interceptors for Grpc.Core, including extraction of trace context from
  untrusted request metadata for server-side instrumentation.
- `OpenTelemetry.Instrumentation.GrpcNetClient` (beta) - instruments outgoing
  gRPC calls made with Grpc.Net.Client, including injection of trace context.
- `OpenTelemetry.Instrumentation.Wcf` (beta) - instruments WCF clients and
  services, including extraction of trace context from untrusted message
  headers for server-side instrumentation.

### Instrumentation - outgoing requests (client-side)

- `OpenTelemetry.Instrumentation.Http` (stable) - instruments outgoing HTTP
  requests made with `HttpClient` and `HttpWebRequest`, including injection of
  trace context and redaction of sensitive values (such as query strings) from
  URLs. Depended on by `OpenTelemetry.Extensions.Enrichment.Http`.

### Instrumentation - cloud providers

- `OpenTelemetry.Instrumentation.AWS` (stable) - instruments calls made with
  the AWS SDK for .NET, including injection of trace context into SQS and SNS
  message attributes and recording of service-specific attributes from
  requests and responses. Depends on `OpenTelemetry.Extensions.AWS`.
- `OpenTelemetry.Instrumentation.AWSLambda` (stable) - a tracing wrapper for
  AWS Lambda function handlers that extracts trace context from untrusted
  invocation inputs, such as API Gateway requests, SQS messages and SNS
  notifications. Depends on `OpenTelemetry.Extensions.AWS`.

### Instrumentation - messaging and background jobs

- `OpenTelemetry.Instrumentation.ConfluentKafka` (development) - instruments
  Confluent.Kafka producers and consumers, including extraction of trace
  context from message headers that may originate from untrusted producers.
- `OpenTelemetry.Instrumentation.Hangfire` (beta) - instruments Hangfire
  background job execution, including propagation of trace context through job
  parameters.
- `OpenTelemetry.Instrumentation.Quartz` (beta) - instruments Quartz.NET job
  execution via its `DiagnosticSource` events.

### Instrumentation - databases and data stores

- `OpenTelemetry.Instrumentation.Cassandra` (beta) - collects metrics from the
  DataStax Cassandra driver.
- `OpenTelemetry.Instrumentation.EntityFrameworkCore` (beta) - instruments
  database calls made via Entity Framework Core. Relies on the shared SQL
  sanitizer (`src/Shared/SqlProcessor.cs`) to remove literal values from query
  text before it is recorded as an attribute, so a bypass of the sanitizer can
  leak sensitive data into telemetry.
- `OpenTelemetry.Instrumentation.Kusto` (alpha) - instruments queries and
  commands sent via the Kusto (Azure Data Explorer) client libraries,
  including sanitization of query text.
- `OpenTelemetry.Instrumentation.SqlClient` (stable) - instruments database
  calls made with `Microsoft.Data.SqlClient` and `System.Data.SqlClient`.
  Relies on the shared SQL sanitizer (`src/Shared/SqlProcessor.cs`) to remove
  literal values from query text before it is recorded as an attribute, so a
  bypass of the sanitizer can leak sensitive data into telemetry.
- `OpenTelemetry.Instrumentation.StackExchangeRedis` (beta) - instruments
  Redis commands made with StackExchange.Redis, which may contain keys and
  values supplied by untrusted users.

### Instrumentation - process and runtime metrics

- `OpenTelemetry.Instrumentation.EventCounters` (alpha) - collects metrics from
  .NET `EventCounter` sources within the current process.
- `OpenTelemetry.Instrumentation.Process` (release candidate) - collects
  metrics (such as memory and CPU usage) about the current process.
- `OpenTelemetry.Instrumentation.Runtime` (stable) - collects metrics about the
  .NET runtime, such as garbage collection, JIT and thread pool statistics.

### Resource detectors

Resource detectors read data from the local environment (such as environment
variables and files under `/proc`) or from cloud metadata endpoints to populate
resource attributes. None of them depend on other packages in this repository.

- `OpenTelemetry.Resources.AWS` (stable) - detects AWS Elastic Beanstalk, EC2,
  ECS and EKS resources, including making HTTP requests to the EC2 instance
  metadata service and the Kubernetes API (using custom TLS server certificate
  validation for the latter).
- `OpenTelemetry.Resources.Azure` (beta) - detects Azure App Service, Azure
  Container Apps and Azure VM resources, including making HTTP requests to the
  Azure Instance Metadata Service.
- `OpenTelemetry.Resources.Container` (beta) - detects the container ID by
  parsing cgroup v1 and v2 files under `/proc`.
- `OpenTelemetry.Resources.Gcp` (development) - detects Google Cloud Platform
  resources (GCE, GKE, Cloud Run and App Engine) using the GCP metadata
  endpoint.
- `OpenTelemetry.Resources.Host` (beta) - detects host name and host ID from the
  local machine (such as from `/etc/machine-id` or the Windows registry).
- `OpenTelemetry.Resources.OperatingSystem` (alpha) - detects operating system
  type, name, version and build from the local machine.
- `OpenTelemetry.Resources.Process` (release candidate) - detects attributes of
  the current process, such as its owner and process ID.
- `OpenTelemetry.Resources.ProcessRuntime` (beta) - detects attributes of the
  .NET runtime, such as its name, version and description.

### Samplers

- `OpenTelemetry.Sampler.AWS` (alpha) - a remote sampler that periodically
  retrieves sampling rules from an AWS X-Ray endpoint over HTTP and matches them
  (including wildcard patterns) against attributes of incoming requests.

### Persistent storage

- `OpenTelemetry.PersistentStorage.Abstractions` (stable) - abstractions for
  persistent storage of telemetry, used by exporters to buffer telemetry across
  process restarts. Depended on by
  `OpenTelemetry.PersistentStorage.FileSystem`.
- `OpenTelemetry.PersistentStorage.FileSystem` (stable) - a file system
  implementation of the persistent storage abstractions that creates, reads
  and deletes files on the local file system. Depends on
  `OpenTelemetry.PersistentStorage.Abstractions`.

### Control plane

- `OpenTelemetry.DynamicControl` (development) - an incubating, experimental
  implementation of dynamic control based on telemetry policies that does not
  yet expose any public APIs.
- `OpenTelemetry.OpAmp.Client` (development) - an OpAMP client that connects to
  a remote OpAMP server over HTTP or WebSockets and processes protobuf messages
  received from it.

### Other

- `OpenTelemetry.SemanticConventions` - generated constants for the
  OpenTelemetry semantic conventions. Contains no runtime logic.

### Deprecated packages

The following directories under `src` contain only a README and CHANGELOG for
packages that have been deprecated and whose source code has been removed from
this repository. They do not need to be scanned.

- `OpenTelemetry.Exporter.Instana`
- `OpenTelemetry.Exporter.Stackdriver`
- `OpenTelemetry.Instrumentation.ElasticsearchClient`
- `OpenTelemetry.Instrumentation.MassTransit`
- `OpenTelemetry.Instrumentation.MySqlData`

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
