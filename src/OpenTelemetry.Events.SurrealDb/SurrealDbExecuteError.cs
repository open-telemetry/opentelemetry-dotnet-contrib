// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Events.SurrealDb;

/// <summary>
/// Event triggered when a SurrealDB method failed.
/// </summary>
public sealed class SurrealDbExecuteError
{
    public const string Name = "SurrealDb.Error.Execute";

    public Exception? Exception { get; set; }
}
