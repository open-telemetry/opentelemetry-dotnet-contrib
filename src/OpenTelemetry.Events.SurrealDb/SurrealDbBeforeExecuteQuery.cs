// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Events.SurrealDb;

/// <summary>
/// Additional information from an event triggered before a "query" method is executed.
/// </summary>
public sealed class SurrealDbBeforeExecuteQuery
{
    public const string Name = "SurrealDb.Query.BeforeExecute";

    public IReadOnlyDictionary<string, object?> Parameters { get; set; }
}
