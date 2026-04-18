// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Instana;

internal class InstanaExporterConstants
{
    internal const string OpenTelemetrySpanType = "otel";
    internal const string KindField = "kind";
    internal const string ServerKind = "server";
    internal const string ClientKind = "client";
    internal const string ProducerKind = "producer";
    internal const string ConsumerKind = "consumer";
    internal const string InternalKind = "internal";
    internal const string ServiceField = "service";
    internal const string OperationField = "operation";
    internal const string TraceStateField = "trace_state";
    internal const string ErrorField = "error";
    internal const string ErrorDetailField = "error_detail";
    internal const string ExceptionField = "exception";
    internal const string TagsField = "tags";
    internal const string EventsField = "events";
    internal const string EventNameField = "name";
    internal const string EventTimestampField = "ts";

    internal const string InstanaEndpointUrl = "INSTANA_ENDPOINT_URL";
    internal const string InstanaAgentKey = "INSTANA_AGENT_KEY";
    internal const string InstanaTimeout = "INSTANA_TIMEOUT";
    internal const string InstanaEndpointProxy = "INSTANA_ENDPOINT_PROXY";
}
