// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Events.SurrealDb.Data;

/// <summary>
/// Data holder for the current trace.
/// </summary>
public sealed class TransientTraceData
{
    public string? Traceparent { get; set; }
}
