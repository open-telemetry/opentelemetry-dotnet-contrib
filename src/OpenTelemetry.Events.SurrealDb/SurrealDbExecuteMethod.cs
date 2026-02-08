// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Events.SurrealDb.Data;

namespace OpenTelemetry.Events.SurrealDb;

/// <summary>
/// Event triggered when a SurrealDB method is executed from the client.
/// </summary>
public sealed class SurrealDbExecuteMethod
{
    public const string Name = "SurrealDb.Method.Execute";

    public string? Namespace { get; set; }
    public string? Database { get; set; }
    public TransientTraceData? Data { get; set; }
}
