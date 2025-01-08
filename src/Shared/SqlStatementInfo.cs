// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation;

internal struct SqlStatementInfo
{
    public SqlStatementInfo(string sanitizedSql, string dbQuerySummaryText, string? dbOperationName, string? dbCollectionName)
    {
        this.SanitizedSql = sanitizedSql;
        this.DbQuerySummary = dbQuerySummaryText;
        this.DbOperationName = dbOperationName;
        this.DbCollectionName = dbCollectionName;
    }

    public string SanitizedSql { get; }

    public string DbQuerySummary { get; }

    public string? DbOperationName { get; }

    public string? DbCollectionName { get; }
}
