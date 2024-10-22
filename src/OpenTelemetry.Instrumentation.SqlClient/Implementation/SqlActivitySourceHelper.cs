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
    public static readonly string ActivityName = ActivitySourceName + ".Execute";

    public static readonly IEnumerable<KeyValuePair<string, object?>> CreationTags = new[]
    {
        new KeyValuePair<string, object?>(SemanticConventions.AttributeDbSystem, MicrosoftSqlServerDatabaseSystemName),
    };

    public static void AddConnectionLevelDetailsToActivity(string dataSource, Activity activity, SqlClientTraceInstrumentationOptions options)
    {
        // TODO: The attributes added here are required. We need to consider
        // collecting these attributes by default.
        if (options.EnableConnectionLevelAttributes)
        {
            var connectionDetails = SqlConnectionDetails.ParseFromDataSource((string)dataSource);

            // TODO: In the new conventions, instance name should now be captured
            // as a part of db.namespace, when available.
            if (options.EmitOldAttributes && !string.IsNullOrEmpty(connectionDetails.InstanceName))
            {
                activity.SetTag(SemanticConventions.AttributeDbMsSqlInstanceName, connectionDetails.InstanceName);
            }

            if (!string.IsNullOrEmpty(connectionDetails.ServerHostName))
            {
                activity.SetTag(SemanticConventions.AttributeServerAddress, connectionDetails.ServerHostName);
            }
            else
            {
                activity.SetTag(SemanticConventions.AttributeServerAddress, connectionDetails.ServerIpAddress);
            }

            if (connectionDetails.Port.HasValue)
            {
                activity.SetTag(SemanticConventions.AttributeServerPort, connectionDetails.Port);
            }
        }
    }
}
