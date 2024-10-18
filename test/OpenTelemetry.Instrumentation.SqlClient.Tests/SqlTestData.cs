// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using OpenTelemetry.Instrumentation.SqlClient.Implementation;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

internal class SqlTestData
{
#if !NETFRAMEWORK
    public static IEnumerable<object[]> SqlClientCallsAreCollectedSuccessfullyCases()
    {
        var bools = new[] { true, false };
        return from beforeCommand in new[] { SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand }
               from commandType in new[] { CommandType.StoredProcedure, CommandType.Text }
               from captureTextCommandContent in bools
               from shouldEnrich in bools
               from tracingEnabled in bools
               from metricsEnabled in bools
               let endCommand = beforeCommand == SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand
                   ? SqlClientDiagnosticListener.SqlDataAfterExecuteCommand
                   : SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand
               let commandText = commandType == CommandType.Text
                   ? "select * from sys.databases"
                   : "SP_GetOrders"
               let captureStoredProcedureCommandName = beforeCommand == SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand
               select new object[]
               {
                    beforeCommand,
                    endCommand,
                    commandType,
                    commandText,
                    captureStoredProcedureCommandName,
                    captureTextCommandContent,
                    shouldEnrich,
                    tracingEnabled,
                    metricsEnabled,
               };
    }

    public static IEnumerable<object[]> SqlClientErrorsAreCollectedSuccessfullyCases()
    {
        var bools = new[] { true, false };
        return from beforeCommand in new[] { SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand }
               from shouldEnrich in bools
               from recordException in bools
               from tracingEnabled in bools
               from metricsEnabled in bools
               let errorCommand = beforeCommand == SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand
                   ? SqlClientDiagnosticListener.SqlDataWriteCommandError
                   : SqlClientDiagnosticListener.SqlMicrosoftWriteCommandError
               let captureStoredProcedureCommandName = beforeCommand == SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand
               select new object[]
               {
                    beforeCommand,
                    errorCommand,
                    shouldEnrich,
                    recordException,
                    tracingEnabled,
                    metricsEnabled,
               };
    }
#endif
}
