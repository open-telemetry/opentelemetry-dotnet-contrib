// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

internal class FakeDbConnection : IDbConnection
{
    [AllowNull]
    public string ConnectionString { get; set; } = string.Empty;

    public int ConnectionTimeout => 0;

    public string Database => "master";

    public ConnectionState State => ConnectionState.Closed;

    public IDbTransaction BeginTransaction() => throw new NotImplementedException();

    public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotImplementedException();

    public void ChangeDatabase(string databaseName) => throw new NotImplementedException();

    public void Close() => throw new NotImplementedException();

    public IDbCommand CreateCommand() => throw new NotImplementedException();

    public void Dispose()
    {
    }

    public void Open() => throw new NotImplementedException();
}
#endif
