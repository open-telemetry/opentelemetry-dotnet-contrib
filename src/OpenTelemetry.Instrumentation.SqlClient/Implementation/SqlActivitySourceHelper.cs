// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.SqlClient.Implementation;

/// <summary>
/// Helper class to hold common properties used by both SqlClientDiagnosticListener on .NET Core
/// and SqlEventSourceListener on .NET Framework.
/// </summary>
internal sealed class SqlActivitySourceHelper
{
    public const string MicrosoftSqlServerDatabaseSystemName = "mssql";

    public static readonly Assembly Assembly = typeof(SqlActivitySourceHelper).Assembly;
    public static readonly AssemblyName AssemblyName = Assembly.GetName();
    public static readonly string ActivitySourceName = AssemblyName.Name!;
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, Assembly.GetPackageVersion());

    public static TagList GetTagListFromConnectionInfo(string? dataSource, string? databaseName, SqlClientTraceInstrumentationOptions options, out string activityName)
    {
        activityName = MicrosoftSqlServerDatabaseSystemName;

        var tags = new TagList
        {
            { SemanticConventions.AttributeDbSystem, MicrosoftSqlServerDatabaseSystemName },
        };

        if (options.EnableConnectionLevelAttributes && dataSource != null)
        {
            var connectionDetails = SqlConnectionDetails.ParseFromDataSource(dataSource);

            if (options.EmitOldAttributes && !string.IsNullOrEmpty(databaseName))
            {
                tags.Add(SemanticConventions.AttributeDbName, databaseName);
                activityName = databaseName!;
            }

            if (options.EmitNewAttributes && !string.IsNullOrEmpty(databaseName))
            {
                var dbNamespace = !string.IsNullOrEmpty(connectionDetails.InstanceName)
                    ? $"{connectionDetails.InstanceName}.{databaseName}" // TODO: Refactor SqlConnectionDetails to include database to avoid string allocation here.
                    : databaseName!;
                tags.Add(SemanticConventions.AttributeDbNamespace, dbNamespace);
                activityName = dbNamespace;
            }

            var serverAddress = connectionDetails.ServerHostName ?? connectionDetails.ServerIpAddress;
            if (!string.IsNullOrEmpty(serverAddress))
            {
                tags.Add(SemanticConventions.AttributeServerAddress, serverAddress);
                if (connectionDetails.Port.HasValue)
                {
                    tags.Add(SemanticConventions.AttributeServerPort, connectionDetails.Port);
                }

                if (activityName == MicrosoftSqlServerDatabaseSystemName)
                {
                    activityName = connectionDetails.Port.HasValue
                        ? $"{serverAddress}:{connectionDetails.Port}" // TODO: Another opportunity to refactor SqlConnectionDetails
                        : serverAddress!;
                }
            }

            if (options.EmitOldAttributes && !string.IsNullOrEmpty(connectionDetails.InstanceName))
            {
                tags.Add(SemanticConventions.AttributeDbMsSqlInstanceName, connectionDetails.InstanceName);
            }
        }
        else if (!string.IsNullOrEmpty(databaseName))
        {
            if (options.EmitNewAttributes)
            {
                tags.Add(SemanticConventions.AttributeDbNamespace, databaseName);
            }

            if (options.EmitOldAttributes)
            {
                tags.Add(SemanticConventions.AttributeDbName, databaseName);
            }

            activityName = databaseName!;
        }

        return tags;
    }
}
