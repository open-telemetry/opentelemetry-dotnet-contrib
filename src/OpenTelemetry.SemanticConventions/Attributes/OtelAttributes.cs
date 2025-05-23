// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from 'src\OpenTelemetry.SemanticConventions\scripts\templates\registry\SemanticConventionsAttributes.cs.j2' </auto-generated>

#nullable enable

#pragma warning disable CS1570 // XML comment has badly formed XML

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class OtelAttributes
{
    /// <summary>
    /// A name uniquely identifying the instance of the OpenTelemetry component within its containing SDK instance.
    /// </summary>
    /// <remarks>
    /// Implementations SHOULD ensure a low cardinality for this attribute, even across application or SDK restarts.
    /// E.g. implementations MUST NOT use UUIDs as values for this attribute.
    /// <p>
    /// Implementations MAY achieve these goals by following a <c><otel.component.type>/<instance-counter></c> pattern, e.g. <c>batching_span_processor/0</c>.
    /// Hereby <c>otel.component.type</c> refers to the corresponding attribute value of the component.
    /// <p>
    /// The value of <c>instance-counter</c> MAY be automatically assigned by the component and uniqueness within the enclosing SDK instance MUST be guaranteed.
    /// For example, <c><instance-counter></c> MAY be implemented by using a monotonically increasing counter (starting with <c>0</c>), which is incremented every time an
    /// instance of the given component type is started.
    /// <p>
    /// With this implementation, for example the first Batching Span Processor would have <c>batching_span_processor/0</c>
    /// as <c>otel.component.name</c>, the second one <c>batching_span_processor/1</c> and so on.
    /// These values will therefore be reused in the case of an application restart.
    /// </remarks>
    public const string AttributeOtelComponentName = "otel.component.name";

    /// <summary>
    /// A name identifying the type of the OpenTelemetry component.
    /// </summary>
    /// <remarks>
    /// If none of the standardized values apply, implementations SHOULD use the language-defined name of the type.
    /// E.g. for Java the fully qualified classname SHOULD be used in this case.
    /// </remarks>
    public const string AttributeOtelComponentType = "otel.component.type";

    /// <summary>
    /// The name of the instrumentation scope - (<c>InstrumentationScope.Name</c> in OTLP).
    /// </summary>
    public const string AttributeOtelScopeName = "otel.scope.name";

    /// <summary>
    /// The version of the instrumentation scope - (<c>InstrumentationScope.Version</c> in OTLP).
    /// </summary>
    public const string AttributeOtelScopeVersion = "otel.scope.version";

    /// <summary>
    /// The result value of the sampler for this span.
    /// </summary>
    public const string AttributeOtelSpanSamplingResult = "otel.span.sampling_result";

    /// <summary>
    /// Name of the code, either "OK" or "ERROR". MUST NOT be set if the status code is UNSET.
    /// </summary>
    public const string AttributeOtelStatusCode = "otel.status_code";

    /// <summary>
    /// Description of the Status if it has a value, otherwise not set.
    /// </summary>
    public const string AttributeOtelStatusDescription = "otel.status_description";

    /// <summary>
    /// A name identifying the type of the OpenTelemetry component.
    /// </summary>
    public static class OtelComponentTypeValues
    {
        /// <summary>
        /// The builtin SDK batching span processor.
        /// </summary>
        public const string BatchingSpanProcessor = "batching_span_processor";

        /// <summary>
        /// The builtin SDK simple span processor.
        /// </summary>
        public const string SimpleSpanProcessor = "simple_span_processor";

        /// <summary>
        /// The builtin SDK batching log record processor.
        /// </summary>
        public const string BatchingLogProcessor = "batching_log_processor";

        /// <summary>
        /// The builtin SDK simple log record processor.
        /// </summary>
        public const string SimpleLogProcessor = "simple_log_processor";

        /// <summary>
        /// OTLP span exporter over gRPC with protobuf serialization.
        /// </summary>
        public const string OtlpGrpcSpanExporter = "otlp_grpc_span_exporter";

        /// <summary>
        /// OTLP span exporter over HTTP with protobuf serialization.
        /// </summary>
        public const string OtlpHttpSpanExporter = "otlp_http_span_exporter";

        /// <summary>
        /// OTLP span exporter over HTTP with JSON serialization.
        /// </summary>
        public const string OtlpHttpJsonSpanExporter = "otlp_http_json_span_exporter";

        /// <summary>
        /// OTLP log record exporter over gRPC with protobuf serialization.
        /// </summary>
        public const string OtlpGrpcLogExporter = "otlp_grpc_log_exporter";

        /// <summary>
        /// OTLP log record exporter over HTTP with protobuf serialization.
        /// </summary>
        public const string OtlpHttpLogExporter = "otlp_http_log_exporter";

        /// <summary>
        /// OTLP log record exporter over HTTP with JSON serialization.
        /// </summary>
        public const string OtlpHttpJsonLogExporter = "otlp_http_json_log_exporter";

        /// <summary>
        /// The builtin SDK periodically exporting metric reader.
        /// </summary>
        public const string PeriodicMetricReader = "periodic_metric_reader";

        /// <summary>
        /// OTLP metric exporter over gRPC with protobuf serialization.
        /// </summary>
        public const string OtlpGrpcMetricExporter = "otlp_grpc_metric_exporter";

        /// <summary>
        /// OTLP metric exporter over HTTP with protobuf serialization.
        /// </summary>
        public const string OtlpHttpMetricExporter = "otlp_http_metric_exporter";

        /// <summary>
        /// OTLP metric exporter over HTTP with JSON serialization.
        /// </summary>
        public const string OtlpHttpJsonMetricExporter = "otlp_http_json_metric_exporter";
    }

    /// <summary>
    /// The result value of the sampler for this span.
    /// </summary>
    public static class OtelSpanSamplingResultValues
    {
        /// <summary>
        /// The span is not sampled and not recording.
        /// </summary>
        public const string Drop = "DROP";

        /// <summary>
        /// The span is not sampled, but recording.
        /// </summary>
        public const string RecordOnly = "RECORD_ONLY";

        /// <summary>
        /// The span is sampled and recording.
        /// </summary>
        public const string RecordAndSample = "RECORD_AND_SAMPLE";
    }

    /// <summary>
    /// Name of the code, either "OK" or "ERROR". MUST NOT be set if the status code is UNSET.
    /// </summary>
    public static class OtelStatusCodeValues
    {
        /// <summary>
        /// The operation has been validated by an Application developer or Operator to have completed successfully.
        /// </summary>
        public const string Ok = "OK";

        /// <summary>
        /// The operation contains an error.
        /// </summary>
        public const string Error = "ERROR";
    }
}
