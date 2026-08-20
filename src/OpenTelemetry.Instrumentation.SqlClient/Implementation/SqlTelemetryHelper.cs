// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Collections.Frozen;
#endif
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

    public static readonly Version SemanticConventionsVersion = new(1, 44, 0);
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

#if NET
    private static readonly FrozenDictionary<string, int> SharedTagNameIndexes = CreateSharedTagNameIndexes().ToFrozenDictionary(StringComparer.Ordinal);
#else
    private static readonly Dictionary<string, int> SharedTagNameIndexes = CreateSharedTagNameIndexes();
#endif

    private static readonly int AllSharedTagsFoundMask = (1 << SharedTagNames.Length) - 1;

    public static void AddSharedTags(Activity activity, ref TagList tags)
    {
        var enumerator = activity.EnumerateTagObjects();
        var found = 0;

        while (enumerator.MoveNext())
        {
            ref readonly var tag = ref enumerator.Current;

            if (tag.Key is null ||
                tag.Value is null ||
                !SharedTagNameIndexes.TryGetValue(tag.Key, out var index))
            {
                continue;
            }

            var mask = 1 << index;

            if ((found & mask) != 0)
            {
                // A value for this tag name has already been added.
                continue;
            }

            tags.Add(tag.Key, tag.Value);
            found |= mask;

            if (found == AllSharedTagsFoundMask)
            {
                break;
            }
        }
    }

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

    private static Dictionary<string, int> CreateSharedTagNameIndexes()
    {
        Debug.Assert(SharedTagNames.Length < 32, "There are too many shared tag names to track with a 32-bit mask.");

        var indexes = new Dictionary<string, int>(SharedTagNames.Length, StringComparer.Ordinal);

        for (var i = 0; i < SharedTagNames.Length; i++)
        {
            indexes[SharedTagNames[i]] = i;
        }

        return indexes;
    }
}
