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

    /// <summary>
    /// The version of the OpenTelemetry semantic conventions the instrumentation targets. Shared by the
    /// activity source and the meter so the traces and metrics schemas cannot drift apart.
    /// </summary>
    public static readonly Version SemanticConventionsVersion = new(1, 40, 0);
}
