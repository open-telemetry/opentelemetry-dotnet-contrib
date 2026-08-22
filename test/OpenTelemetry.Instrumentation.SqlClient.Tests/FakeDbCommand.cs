// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

internal sealed class FakeDbCommand : IDbCommand
{
    [AllowNull]
    public string CommandText { get; set; } = string.Empty;

    public int CommandTimeout { get; set; }

    public CommandType CommandType { get; set; }

    public IDbConnection? Connection { get; set; }

    public IDataParameterCollection Parameters => throw new NotImplementedException();

    public IDbTransaction? Transaction { get; set; }

    public UpdateRowSource UpdatedRowSource { get; set; }

    public void Cancel() => throw new NotImplementedException();

    public IDbDataParameter CreateParameter() => throw new NotImplementedException();

    public void Dispose()
    {
    }

    public int ExecuteNonQuery() => throw new NotImplementedException();

    public IDataReader ExecuteReader() => throw new NotImplementedException();

    public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotImplementedException();

    public object? ExecuteScalar() => throw new NotImplementedException();

    public void Prepare() => throw new NotImplementedException();
}
#endif
