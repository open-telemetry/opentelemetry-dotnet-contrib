// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Events.SurrealDb;

/// <summary>
/// Event triggered after any SurrealDB method is executed from the client.
/// </summary>
public sealed class SurrealDbAfterExecuteMethod
{
    public const string Name = "SurrealDb.Method.AfterExecute";

    public int? BatchSize { get; set; }
}
