// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

internal readonly struct KustoStatementInfo
{
    public KustoStatementInfo(string? summarized, string? sanitized)
    {
        this.Summarized = summarized;
        this.Sanitized = sanitized;
    }

    public string? Summarized { get; }

    public string? Sanitized { get; }
}
