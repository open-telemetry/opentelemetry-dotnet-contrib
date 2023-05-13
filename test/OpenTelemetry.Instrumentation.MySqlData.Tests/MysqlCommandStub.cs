// <copyright file="MysqlCommandStub.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace OpenTelemetry.Instrumentation.MySqlData.Tests;

public class MysqlCommandStub : DbCommand
{
    public MysqlCommandStub(string commandText, DbConnection connection)
    {
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
        this.CommandText = commandText;
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
        this.DbConnection = connection;
    }

#pragma warning disable CS8765
    public override string CommandText { get; set; }
#pragma warning restore CS8765

    public override int CommandTimeout { get; set; }

    public override CommandType CommandType { get; set; }

    public override bool DesignTimeVisible { get; set; }

    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection { get; set; }

    protected override DbParameterCollection DbParameterCollection { get; } = null!;

    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel()
    {
    }

    public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
    {
        if (this.CommandText.Contains("throw"))
        {
            throw new Exception("test exception");
        }

        await Task.Delay(0, cancellationToken);

        return 1;
    }

    public override int ExecuteNonQuery()
    {
        return this.ExecuteNonQueryAsync().GetAwaiter().GetResult();
    }

    public override async Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(0, cancellationToken);
        if (this.CommandText.Contains("throw"))
        {
            throw new Exception("test exception");
        }

        return new object();
    }

    public override object? ExecuteScalar()
    {
        if (this.CommandText.Contains("throw"))
        {
            throw new Exception("test exception");
        }

        return new object();
    }

    public override void Prepare()
    {
    }

    protected override DbParameter CreateDbParameter()
    {
        return new MySqlParameter();
    }

    protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
    {
        await Task.Delay(0, cancellationToken);
        if (this.CommandText.Contains("throw"))
        {
            throw new Exception("test exception");
        }

        return new DataTableReader(new DataTable());
    }

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        return this.ExecuteDbDataReaderAsync(behavior, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
    }
}
