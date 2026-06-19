// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

/// <summary>
/// Kusto-specific semantic convention attributes that are not part of the shared OpenTelemetry conventions.
/// </summary>
internal static class KustoSemanticConventions
{
    public const string DbSystemNameValue = "azure.kusto";
    public const string ClientRequestIdTagKey = $"{DbSystemNameValue}.client_request_id";
}
