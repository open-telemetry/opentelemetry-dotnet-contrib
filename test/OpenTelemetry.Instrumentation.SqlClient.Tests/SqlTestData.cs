// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Data;
using OpenTelemetry.Instrumentation.SqlClient.Implementation;
#endif

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

internal class SqlTestData
{
#if !NETFRAMEWORK
    public static IEnumerable<object[]> SqlClientCallsAreCollectedSuccessfullyCases()
    {
        var bools = new[] { true, false };
        return from beforeCommand in new[] { SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand }
               from commandType in new[] { CommandType.StoredProcedure, CommandType.Text }
               from emitOldAttributes in bools
               from emitNewAttributes in bools
               where emitOldAttributes && emitNewAttributes
               select new object[]
               {
                    beforeCommand,
                    commandType,
                    emitOldAttributes,
                    emitNewAttributes,
               };
    }

    public static IEnumerable<object[]> SqlClientErrorsAreCollectedSuccessfullyCases()
    {
        var bools = new[] { true, false };
        return from beforeCommand in new[] { SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand }
               from shouldEnrich in bools
               from recordException in bools
               select new object[]
               {
                    beforeCommand,
                    recordException,
               };
    }
#endif
}
