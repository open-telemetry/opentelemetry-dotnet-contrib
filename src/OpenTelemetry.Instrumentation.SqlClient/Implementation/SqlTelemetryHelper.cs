// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Trace;
using ActivitySourceFactory = OpenTelemetry.Trace.ActivitySourceFactory;

namespace OpenTelemetry.Instrumentation.SqlClient.Implementation;

/// <summary>
/// Helper class to hold common properties used by both SqlClientDiagnosticListener on .NET Core
/// and SqlEventSourceListener on .NET Framework.
/// </summary>
internal sealed class SqlTelemetryHelper
{
    public const string MicrosoftSqlServerDbSystemName = "microsoft.sql_server";

    public static readonly Version SemanticConventionsVersion = new(1, 33, 0);
    public static readonly ActivitySource ActivitySource = ActivitySourceFactory.Create<SqlTelemetryHelper>(SemanticConventionsVersion);
    public static readonly Meter Meter = Metrics.MeterFactory.Create<SqlTelemetryHelper>(SemanticConventionsVersion);

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

    public static TagList GetTagListFromConnectionInfo(string? dataSource, string? databaseName, out string activityName)
    {
        activityName = MicrosoftSqlServerDbSystemName;

        TagList tags = default;

        tags.Add(SemanticConventions.AttributeDbSystemName, MicrosoftSqlServerDbSystemName);

        if (dataSource != null)
        {
            var connectionDetails = SqlConnectionDetails.ParseFromDataSource(dataSource);

            if (databaseName is { Length: > 0 })
            {
                var dbNamespace = connectionDetails.GetDbNamespace(databaseName);
                tags.Add(SemanticConventions.AttributeDbNamespace, dbNamespace);
                activityName = dbNamespace;
            }

            var serverAddress = connectionDetails.ServerHostName ?? connectionDetails.ServerIpAddress;
            if (!string.IsNullOrEmpty(serverAddress))
            {
                tags.Add(SemanticConventions.AttributeServerAddress, serverAddress);
                if (connectionDetails.BoxedPort is { } port)
                {
                    tags.Add(SemanticConventions.AttributeServerPort, port);
                }

                if (activityName == MicrosoftSqlServerDbSystemName)
                {
                    activityName = connectionDetails.ServerAddressAndPort!;
                }
            }
        }
        else if (databaseName is { Length: > 0 })
        {
            tags.Add(SemanticConventions.AttributeDbNamespace, databaseName);
            activityName = databaseName;
        }

        return tags;
    }

    internal static double CalculateDurationFromTimestamp(long begin)
        => Stopwatch.GetElapsedTime(begin).TotalSeconds;
}
