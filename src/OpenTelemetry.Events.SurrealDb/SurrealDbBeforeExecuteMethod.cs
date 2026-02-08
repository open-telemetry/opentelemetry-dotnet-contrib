// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Events.SurrealDb;

/// <summary>
/// Event triggered before any SurrealDB method is executed from the client.
/// </summary>
public sealed class SurrealDbBeforeExecuteMethod
{
    public const string Name = "SurrealDb.Method.BeforeExecute";

    public string Summary { get; set; }
    public string Method { get; set; }
    public Uri Address { get; set; }
    public string? ProtocolName { get; set; }
    public string? Table { get; set; }
}
