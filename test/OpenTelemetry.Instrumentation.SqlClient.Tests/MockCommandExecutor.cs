// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Collections;
using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using OpenTelemetry.Instrumentation.SqlClient.Implementation;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

public class MockCommandExecutor
{
    public static void ExecuteCommand(string connectionString, CommandType commandType, string commandText, bool error, SqlClientLibrary library, long? selectRows = null, long? iduRows = null)
    {
        var statistics = error ? null : new Dictionary<string, object>
        {
            ["SelectRows"] = selectRows ?? 0L,
            ["IduRows"] = iduRows ?? 0L,
        };
        ExecuteCommand(connectionString, commandType, commandText, error, library, (IDictionary?)statistics);
    }

    public static void ExecuteCommand(IDbCommand command, SqlClientLibrary library, IDictionary? statistics)
    {
        using var fakeSqlClientDiagnosticSource = new FakeSqlClientDiagnosticSource();

        var beforeCommand = library == SqlClientLibrary.SystemDataSqlClient
            ? SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand
            : SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand;

        var afterCommand = library == SqlClientLibrary.SystemDataSqlClient
            ? SqlClientDiagnosticListener.SqlDataAfterExecuteCommand
            : SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand;

        var operationId = Guid.NewGuid();

        fakeSqlClientDiagnosticSource.Write(
            beforeCommand,
            new
            {
                OperationId = operationId,
                Command = command,
                Timestamp = (long?)1000000L,
            });

        fakeSqlClientDiagnosticSource.Write(
            afterCommand,
            new
            {
                OperationId = operationId,
                Command = command,
                Statistics = statistics,
                Timestamp = 2000000L,
            });
    }

    public static void ExecuteCommand(string connectionString, CommandType commandType, string commandText, bool error, SqlClientLibrary library, IDictionary? statistics)
    {
        using var fakeSqlClientDiagnosticSource = new FakeSqlClientDiagnosticSource();

        var beforeCommand = library == SqlClientLibrary.SystemDataSqlClient
            ? SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand
            : SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand;

        var afterCommand = beforeCommand == SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand
            ? SqlClientDiagnosticListener.SqlDataAfterExecuteCommand
            : SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand;

        var errorCommand = beforeCommand == SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand
            ? SqlClientDiagnosticListener.SqlDataWriteCommandError
            : SqlClientDiagnosticListener.SqlMicrosoftWriteCommandError;

        using var sqlConnection = new SqlConnection(connectionString);
        using var sqlCommand = sqlConnection.CreateCommand();

        var operationId = Guid.NewGuid();
        sqlCommand.CommandType = commandType;
#pragma warning disable CA2100
        sqlCommand.CommandText = commandText;
#pragma warning restore CA2100

        var beforeExecuteEventData = new
        {
            OperationId = operationId,
            Command = sqlCommand,
            Timestamp = (long?)1000000L,
        };

        fakeSqlClientDiagnosticSource.Write(
            beforeCommand,
            beforeExecuteEventData);

        if (error)
        {
            var commandErrorEventData = new
            {
                OperationId = operationId,
                Command = sqlCommand,
                Exception = new Exception("Boom!"),
                Timestamp = 2000000L,
            };

            fakeSqlClientDiagnosticSource.Write(
                errorCommand,
                commandErrorEventData);
        }
        else
        {
            // Mirrors the connection statistics dictionary that Microsoft.Data.SqlClient /
            // System.Data.SqlClient include on the WriteCommandAfter payload. SelectRows is the
            // number of rows returned by queries; IduRows is the number affected by
            // INSERT/UPDATE/DELETE commands. The values are cumulative for the connection lifetime;
            // callers that want to test the per-command delta behaviour should pass cumulative values
            // that reflect all prior work on the connection plus the current command.
            var afterExecuteEventData = new
            {
                OperationId = operationId,
                Command = sqlCommand,
                Statistics = statistics,
                Timestamp = 2000000L,
            };

            fakeSqlClientDiagnosticSource.Write(
                afterCommand,
                afterExecuteEventData);
        }
    }

    private class FakeSqlClientDiagnosticSource : IDisposable
    {
        private readonly DiagnosticListener listener;

        public FakeSqlClientDiagnosticSource()
        {
            this.listener = new DiagnosticListener(SqlClientInstrumentation.SqlClientDiagnosticListenerName);
        }

        public void Write(string name, object value)
        {
            if (this.listener.IsEnabled(name))
            {
                this.listener.Write(name, value);
            }
        }

        public void Dispose()
        {
            this.listener.Dispose();
        }
    }
}
#endif
