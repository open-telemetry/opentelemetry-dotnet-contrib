// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Implementation;

internal sealed class EntityFrameworkDiagnosticListener : ListenerHandler
{
    internal const string DiagnosticSourceName = "Microsoft.EntityFrameworkCore";

    internal const string EntityFrameworkCoreCommandCreated = "Microsoft.EntityFrameworkCore.Database.Command.CommandCreated";
    internal const string EntityFrameworkCoreCommandExecuting = "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuting";
    internal const string EntityFrameworkCoreCommandExecuted = "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted";
    internal const string EntityFrameworkCoreCommandError = "Microsoft.EntityFrameworkCore.Database.Command.CommandError";

    internal const string AttributePeerService = "peer.service";
    internal const string AttributeServerAddress = "server.address";
    internal const string AttributeDbSystem = "db.system";
    internal const string AttributeDbName = "db.name";
    internal const string AttributeDbNamespace = "db.namespace";
    internal const string AttributeDbStatement = "db.statement";
    internal const string AttributeDbQueryText = "db.query.text";

    internal static readonly Assembly Assembly = typeof(EntityFrameworkDiagnosticListener).Assembly;
    public static readonly AssemblyName AssemblyName = Assembly.GetName();

    internal static readonly string ActivitySourceName = AssemblyName.Name!;
    internal static readonly string ActivityName = ActivitySourceName + ".Execute";
    internal static readonly ActivitySource EntityFrameworkActivitySource = new(ActivitySourceName, Assembly.GetPackageVersion());

    internal static readonly string MeterName = AssemblyName.Name!;
    internal static readonly Meter Meter = new(MeterName, Assembly.GetPackageVersion());
    internal static readonly Histogram<double> DbClientOperationDuration = Meter.CreateHistogram(
        "db.client.operation.duration",
        unit: "s",
        description: "Duration of database client operations.",
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1, 5, 10] });

    private static readonly string[] SharedTagNames =
    [
        SemanticConventions.AttributeDbSystem,
        SemanticConventions.AttributeDbCollectionName,
        SemanticConventions.AttributeDbName,
        SemanticConventions.AttributeDbNamespace,
        SemanticConventions.AttributeDbResponseStatusCode,
        SemanticConventions.AttributeServerAddress,
    ];

    private readonly PropertyFetcher<object> commandFetcher = new("Command");
    private readonly PropertyFetcher<object> connectionFetcher = new("Connection");
    private readonly PropertyFetcher<object> dbContextFetcher = new("Context");
    private readonly PropertyFetcher<object> dbContextDatabaseFetcher = new("Database");
    private readonly PropertyFetcher<string> providerNameFetcher = new("ProviderName");
    private readonly PropertyFetcher<object> dataSourceFetcher = new("DataSource");
    private readonly PropertyFetcher<object> databaseFetcher = new("Database");
    private readonly PropertyFetcher<CommandType> commandTypeFetcher = new("CommandType");
    private readonly PropertyFetcher<string> commandTextFetcher = new("CommandText");
    private readonly PropertyFetcher<Exception> exceptionFetcher = new("Exception");
    private readonly AsyncLocal<long> beginTimestamp = new();

    public EntityFrameworkDiagnosticListener(string sourceName)
        : base(sourceName)
    {
    }

    public override bool SupportsNullActivity => true;

    private static double CalculateDurationFromTimestamp(long begin, long? end = null)
    {
        end ??= Stopwatch.GetTimestamp();
        var timestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;
        var delta = end - begin;
        var ticks = (long)(timestampToTicks * delta);
        var duration = new TimeSpan(ticks);
        return duration.TotalSeconds;
    }

    private static string GetDbSystemFromProviderName(string? providerName)
    {
        return providerName switch
        {
            "Microsoft.EntityFrameworkCore.SqlServer" => "mssql",
            "Microsoft.EntityFrameworkCore.Cosmos" => "cosmosdb",
            "Microsoft.EntityFrameworkCore.Sqlite" or "Devart.Data.SQLite.Entity.EFCore" => "sqlite",
            "MySql.Data.EntityFrameworkCore" or "Pomelo.EntityFrameworkCore.MySql" or "Devart.Data.MySql.Entity.EFCore" => "mysql",
            "Npgsql.EntityFrameworkCore.PostgreSQL" or "Devart.Data.PostgreSql.Entity.EFCore" => "postgresql",
            "Oracle.EntityFrameworkCore" or "Devart.Data.Oracle.Entity.EFCore" => "oracle",
            "Microsoft.EntityFrameworkCore.InMemory" => "efcoreinmemory",
            "FirebirdSql.EntityFrameworkCore.Firebird" => "firebird",
            "FileContextCore" => "filecontextcore",
            "EntityFrameworkCore.SqlServerCompact35" or "EntityFrameworkCore.SqlServerCompact40" => "mssqlcompact",
            "EntityFrameworkCore.OpenEdge" => "openedge",
            "EntityFrameworkCore.Jet" => "jet",
            "Google.Cloud.EntityFrameworkCore.Spanner" => "spanner",
            "Teradata.EntityFrameworkCore" => "teradata",
            _ => "other_sql",
        };
    }

    private TagList CreateTagsFromConnectionInfo(object? payload, object? command, EntityFrameworkInstrumentationOptions options, out string activityName)
    {
        activityName = ActivityName;
        var tags = default(TagList);

        if (payload == null || command == null)
        {
            return tags;
        }

        var connection = this.connectionFetcher.Fetch(command);
        var dataSource = (string)this.dataSourceFetcher.Fetch(connection);
        var database = (string)this.databaseFetcher.Fetch(connection);
        activityName = database;

        var dbContext = this.dbContextFetcher.Fetch(payload);
        var dbContextDatabase = this.dbContextDatabaseFetcher.Fetch(dbContext);
        var providerName = this.providerNameFetcher.Fetch(dbContextDatabase);

        var dbSystem = GetDbSystemFromProviderName(providerName);
        tags.Add(SemanticConventions.AttributeDbSystem, dbSystem);
        if (dbSystem == "other_sql" && providerName != null)
        {
            tags.Add("ef.provider", providerName);
        }

        if (!string.IsNullOrEmpty(dataSource))
        {
            tags.Add(SemanticConventions.AttributeServerAddress, dataSource);
        }

        if (options.EmitOldAttributes)
        {
            tags.Add(SemanticConventions.AttributeDbName, database);
        }

        if (options.EmitNewAttributes)
        {
            tags.Add(SemanticConventions.AttributeDbNamespace, database);
        }

        return tags;
    }

    public override void OnEventWritten(string name, object? payload)
    {
        if (EntityFrameworkInstrumentation.TracingHandles == 0 && EntityFrameworkInstrumentation.MetricHandles == 0)
        {
            return;
        }

        var options = EntityFrameworkInstrumentation.TracingOptions;
        var activity = Activity.Current;

        switch (name)
        {
            case EntityFrameworkCoreCommandCreated:
                {
                    var command = this.commandFetcher.Fetch(payload);
                    if (command == null)
                    {
                        EntityFrameworkInstrumentationEventSource.Log.NullPayload(nameof(EntityFrameworkDiagnosticListener), name);
                        return;
                    }

                    var startTags = this.CreateTagsFromConnectionInfo(payload, command, options, out var activityName);
                    activity = EntityFrameworkActivitySource.StartActivity(
                        activityName,
                        ActivityKind.Client,
                        default(ActivityContext),
                        startTags);

                    if (activity == null)
                    {
                        this.beginTimestamp.Value = Stopwatch.GetTimestamp();
                        return;
                    }
                }

                break;

            case EntityFrameworkCoreCommandExecuting:
                {
                    if (activity == null)
                    {
                        EntityFrameworkInstrumentationEventSource.Log.NullActivity(name);
                        return;
                    }

                    if (activity.Source != EntityFrameworkActivitySource)
                    {
                        return;
                    }

                    if (activity.IsAllDataRequested)
                    {
                        var command = this.commandFetcher.Fetch(payload);

                        try
                        {
                            var dbContext = this.dbContextFetcher.Fetch(payload);
                            var dbContextDatabase = this.dbContextDatabaseFetcher.Fetch(dbContext);
                            var providerName = this.providerNameFetcher.Fetch(dbContextDatabase);

                            if (command is IDbCommand typedCommand && options.Filter?.Invoke(providerName, typedCommand) == false)
                            {
                                EntityFrameworkInstrumentationEventSource.Log.CommandIsFilteredOut(activity.OperationName);
                                activity.IsAllDataRequested = false;
                                activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            EntityFrameworkInstrumentationEventSource.Log.CommandFilterException(ex);
                            activity.IsAllDataRequested = false;
                            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                            return;
                        }

                        if (this.commandTypeFetcher.Fetch(command) is CommandType commandType)
                        {
                            var commandText = this.commandTextFetcher.Fetch(command);
                            switch (commandType)
                            {
                                case CommandType.StoredProcedure:
                                    if (options.SetDbStatementForStoredProcedure)
                                    {
                                        if (options.EmitOldAttributes)
                                        {
                                            activity.AddTag(SemanticConventions.AttributeDbStatement, commandText);
                                        }

                                        if (options.EmitNewAttributes)
                                        {
                                            activity.AddTag(SemanticConventions.AttributeDbQueryText, commandText);
                                        }
                                    }

                                    break;

                                case CommandType.Text:
                                    if (options.SetDbStatementForText)
                                    {
                                        if (options.EmitOldAttributes)
                                        {
                                            activity.AddTag(SemanticConventions.AttributeDbStatement, commandText);
                                        }

                                        if (options.EmitNewAttributes)
                                        {
                                            activity.AddTag(SemanticConventions.AttributeDbQueryText, commandText);
                                        }
                                    }

                                    break;

                                case CommandType.TableDirect:
                                    break;
                                default:
                                    break;
                            }
                        }

                        try
                        {
                            if (command is IDbCommand typedCommand)
                            {
                                options.EnrichWithIDbCommand?.Invoke(activity, typedCommand);
                            }
                        }
                        catch (Exception ex)
                        {
                            EntityFrameworkInstrumentationEventSource.Log.EnrichmentException(nameof(EntityFrameworkCoreCommandExecuting), ex);
                        }
                    }
                }

                break;

            case EntityFrameworkCoreCommandExecuted:
                {
                    if (activity == null)
                    {
                        EntityFrameworkInstrumentationEventSource.Log.NullActivity(name);
                        this.RecordDuration(null, payload);
                        return;
                    }

                    if (activity.Source != EntityFrameworkActivitySource)
                    {
                        this.RecordDuration(null, payload);
                        return;
                    }

                    activity.Stop();
                    this.RecordDuration(activity, payload);
                }

                break;

            case EntityFrameworkCoreCommandError:
                {
                    if (activity == null)
                    {
                        EntityFrameworkInstrumentationEventSource.Log.NullActivity(name);
                        this.RecordDuration(null, payload, hasError: true);
                        return;
                    }

                    if (activity.Source != EntityFrameworkActivitySource)
                    {
                        this.RecordDuration(null, payload, hasError: true);
                        return;
                    }

                    try
                    {
                        if (activity.IsAllDataRequested)
                        {
                            if (this.exceptionFetcher.Fetch(payload) is Exception exception)
                            {
                                activity.AddTag(SemanticConventions.AttributeErrorType, exception.GetType().FullName);
                                activity.SetStatus(ActivityStatusCode.Error, exception.Message);
                            }
                            else
                            {
                                EntityFrameworkInstrumentationEventSource.Log.NullPayload(nameof(EntityFrameworkDiagnosticListener), name);
                            }
                        }
                    }
                    finally
                    {
                        activity.Stop();
                        this.RecordDuration(activity, payload, hasError: true);
                    }
                }

                break;
            default:
                break;
        }
    }

    private void RecordDuration(Activity? activity, object? payload, bool hasError = false)
    {
        if (EntityFrameworkInstrumentation.MetricHandles == 0)
        {
            return;
        }

        var tags = default(TagList);

        if (activity != null && activity.IsAllDataRequested)
        {
            foreach (var tag in SharedTagNames)
            {
                var value = activity.GetTagItem(tag);
                if (value != null)
                {
                    tags.Add(tag, value);
                }
            }
        }
        else if (payload != null)
        {
            var command = this.commandFetcher.Fetch(payload);
            var startTags = this.CreateTagsFromConnectionInfo(
                payload,
                command,
                EntityFrameworkInstrumentation.TracingOptions,
                out _);

            foreach (var tag in startTags)
            {
                tags.Add(tag.Key, tag.Value);
            }

            if (hasError)
            {
                if (this.exceptionFetcher.Fetch(payload) is Exception exception)
                {
                    tags.Add(SemanticConventions.AttributeErrorType, exception.GetType().FullName);
                }
            }
        }

        var duration = activity?.Duration.TotalSeconds
            ?? CalculateDurationFromTimestamp(this.beginTimestamp.Value);
        DbClientOperationDuration.Record(duration, tags);
    }
}
