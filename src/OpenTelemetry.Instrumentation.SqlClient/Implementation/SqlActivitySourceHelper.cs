// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
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
    public const string MicrosoftSqlServerDbSystemName = "microsoft.sql_server";
    public const string MicrosoftSqlServerDbSystem = "mssql";

    public static readonly Assembly Assembly = typeof(SqlActivitySourceHelper).Assembly;
    public static readonly AssemblyName AssemblyName = Assembly.GetName();
    public static readonly string ActivitySourceName = AssemblyName.Name!;
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, Assembly.GetPackageVersion());

    public static readonly string MeterName = AssemblyName.Name!;
    public static readonly Meter Meter = new(MeterName, Assembly.GetPackageVersion());

    public static readonly Histogram<double> DbClientOperationDuration = Meter.CreateHistogram(
        "db.client.operation.duration",
        unit: "s",
        description: "Duration of database client operations.",
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1, 5, 10] });

    internal static readonly string[] SharedTagNames =
    [
        SemanticConventions.AttributeDbSystem,
        SemanticConventions.AttributeDbSystemName,
        SemanticConventions.AttributeDbNamespace,
        SemanticConventions.AttributeDbOperationName,
        SemanticConventions.AttributeDbStoredProcedureName,
        SemanticConventions.AttributeDbQuerySummary,
        SemanticConventions.AttributeDbResponseStatusCode,
        SemanticConventions.AttributeErrorType,
        SemanticConventions.AttributeServerPort,
        SemanticConventions.AttributeServerAddress,
    ];

    public static TagList GetTagListFromConnectionInfo(string? dataSource, string? databaseName, SqlClientTraceInstrumentationOptions options, out string activityName)
    {
        activityName = options.EmitNewAttributes
            ? MicrosoftSqlServerDbSystemName
            : MicrosoftSqlServerDbSystem;

        TagList tags = default;

        if (options.EmitOldAttributes)
        {
            tags.Add(SemanticConventions.AttributeDbSystem, MicrosoftSqlServerDbSystem);
        }

        if (options.EmitNewAttributes)
        {
            tags.Add(SemanticConventions.AttributeDbSystemName, MicrosoftSqlServerDbSystemName);
        }

        if (dataSource != null)
        {
            var connectionDetails = SqlConnectionDetails.ParseFromDataSource(dataSource);

            if (!string.IsNullOrEmpty(databaseName))
            {
                if (options.EmitOldAttributes)
                {
                    tags.Add(SemanticConventions.AttributeDbName, databaseName);
                    activityName = databaseName!;
                }

                if (options.EmitNewAttributes)
                {
                    var dbNamespace = !string.IsNullOrEmpty(connectionDetails.InstanceName)
                        ? $"{connectionDetails.InstanceName}.{databaseName}" // TODO: Refactor SqlConnectionDetails to include database to avoid string allocation here.
                        : databaseName!;
                    tags.Add(SemanticConventions.AttributeDbNamespace, dbNamespace);
                    activityName = dbNamespace;
                }
            }

            var serverAddress = connectionDetails.ServerHostName ?? connectionDetails.ServerIpAddress;
            if (!string.IsNullOrEmpty(serverAddress))
            {
                tags.Add(SemanticConventions.AttributeServerAddress, serverAddress);
                if (connectionDetails.Port is { } port)
                {
                    tags.Add(SemanticConventions.AttributeServerPort, port);
                }

                if (activityName == MicrosoftSqlServerDbSystem || activityName == MicrosoftSqlServerDbSystemName)
                {
                    if (connectionDetails.Port is { } portNumber)
                    {
                        activityName = $"{serverAddress}:{portNumber}"; // TODO: Another opportunity to refactor SqlConnectionDetails
                    }
                    else
                    {
                        activityName = serverAddress!;
                    }
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

    internal static double CalculateDurationFromTimestamp(long begin)
    {
#if NET
        var duration = Stopwatch.GetElapsedTime(begin);
#else
        var end = Stopwatch.GetTimestamp();
        var timestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;
        var delta = end - begin;
        var ticks = (long)(timestampToTicks * delta);
        var duration = new TimeSpan(ticks);
#endif

        return duration.TotalSeconds;
    }
}
