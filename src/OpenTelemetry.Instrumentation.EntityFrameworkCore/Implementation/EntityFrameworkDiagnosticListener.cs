// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using System.Diagnostics;
using System.Reflection;
using OpenTelemetry.Internal;

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
    internal static readonly string ActivitySourceName = Assembly.GetName().Name;
    internal static readonly string ActivityName = ActivitySourceName + ".Execute";
    internal static readonly ActivitySource EntityFrameworkActivitySource = new(ActivitySourceName, Assembly.GetPackageVersion());

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

    private readonly EntityFrameworkInstrumentationOptions options;

    public EntityFrameworkDiagnosticListener(string sourceName, EntityFrameworkInstrumentationOptions? options)
        : base(sourceName)
    {
        this.options = options ?? new EntityFrameworkInstrumentationOptions();
    }

    public override bool SupportsNullActivity => true;

    public override void OnEventWritten(string name, object? payload)
    {
        var activity = Activity.Current;

        switch (name)
        {
            case EntityFrameworkCoreCommandCreated:
                {
                    activity = EntityFrameworkActivitySource.StartActivity(ActivityName, ActivityKind.Client);
                    if (activity == null)
                    {
                        // There is no listener or it decided not to sample the current request.
                        return;
                    }

                    var command = this.commandFetcher.Fetch(payload);
                    if (command == null)
                    {
                        EntityFrameworkInstrumentationEventSource.Log.NullPayload(nameof(EntityFrameworkDiagnosticListener), name);
                        activity.Stop();
                        return;
                    }

                    var connection = this.connectionFetcher.Fetch(command);
                    var database = (string)this.databaseFetcher.Fetch(connection);
                    activity.DisplayName = database;

                    if (activity.IsAllDataRequested)
                    {
                        string? providerOrCommandName = null;

                        if (this.dbContextFetcher.Fetch(payload) is { } dbContext)
                        {
                            var dbContextDatabase = this.dbContextDatabaseFetcher.Fetch(dbContext);
                            providerOrCommandName = this.providerNameFetcher.Fetch(dbContextDatabase);
                        }
                        else
                        {
                            // Try to infer the database name from the command
                            // type if the DbContext is not available.
                            providerOrCommandName = command.GetType().FullName;
                        }

                        switch (providerOrCommandName)
                        {
                            case "Microsoft.EntityFrameworkCore.SqlServer":
                            case "Microsoft.Data.SqlClient.SqlCommand":
                                activity.AddTag(AttributeDbSystem, "mssql");
                                break;
                            case "Microsoft.EntityFrameworkCore.Cosmos":
                                activity.AddTag(AttributeDbSystem, "cosmosdb");
                                break;
                            case "Microsoft.Data.Sqlite.SqliteCommand":
                            case "Microsoft.EntityFrameworkCore.Sqlite":
                            case "Devart.Data.SQLite.Entity.EFCore":
                                activity.AddTag(AttributeDbSystem, "sqlite");
                                break;
                            case "MySql.Data.EntityFrameworkCore":
                            case "MySql.Data.MySqlClient.MySqlCommand":
                            case "Pomelo.EntityFrameworkCore.MySql":
                            case "Devart.Data.MySql.Entity.EFCore":
                            case "Devart.Data.MySql.MySqlCommand":
                                activity.AddTag(AttributeDbSystem, "mysql");
                                break;
                            case "Npgsql.EntityFrameworkCore.PostgreSQL":
                            case "Npgsql.NpgsqlCommand":
                            case "Devart.Data.PostgreSql.Entity.EFCore":
                            case "Devart.Data.PostgreSql.PgSqlCommand":
                                activity.AddTag(AttributeDbSystem, "postgresql");
                                break;
                            case "Oracle.EntityFrameworkCore":
                            case "Oracle.ManagedDataAccess.Client.OracleCommand":
                            case "Devart.Data.Oracle.Entity.EFCore":
                            case "Devart.Data.Oracle.OracleCommand":
                                activity.AddTag(AttributeDbSystem, "oracle");
                                break;
                            case "Microsoft.EntityFrameworkCore.InMemory":
                                activity.AddTag(AttributeDbSystem, "efcoreinmemory");
                                break;
                            case "FirebirdSql.Data.FirebirdClient.FbCommand":
                            case "FirebirdSql.EntityFrameworkCore.Firebird":
                                activity.AddTag(AttributeDbSystem, "firebird");
                                break;
                            case "FileContextCore":
                                activity.AddTag(AttributeDbSystem, "filecontextcore");
                                break;
                            case "EntityFrameworkCore.SqlServerCompact35":
                            case "EntityFrameworkCore.SqlServerCompact40":
                            case "System.Data.SqlServerCe.SqlCeCommand":
                                activity.AddTag(AttributeDbSystem, "mssqlcompact");
                                break;
                            case "EntityFrameworkCore.OpenEdge":
                                activity.AddTag(AttributeDbSystem, "openedge");
                                break;
                            case "EntityFrameworkCore.Jet":
                            case "EntityFrameworkCore.Jet.Data.JetCommand":
                                activity.AddTag(AttributeDbSystem, "jet");
                                break;
                            case "Google.Cloud.EntityFrameworkCore.Spanner":
                            case "Google.Cloud.Spanner.Data.SpannerCommand":
                                activity.AddTag(AttributeDbSystem, "spanner");
                                break;
                            case "Teradata.Client.Provider.TdCommand":
                            case "Teradata.EntityFrameworkCore":
                                activity.AddTag(AttributeDbSystem, "teradata");
                                break;
                            default:
                                activity.AddTag(AttributeDbSystem, "other_sql");
                                activity.AddTag("ef.provider", providerOrCommandName);
                                break;
                        }

                        var dataSource = (string)this.dataSourceFetcher.Fetch(connection);
                        if (!string.IsNullOrEmpty(dataSource))
                        {
                            activity.AddTag(AttributeServerAddress, dataSource);
                        }

                        if (this.options.EmitOldAttributes)
                        {
                            activity.AddTag(AttributeDbName, database);
                        }

                        if (this.options.EmitNewAttributes)
                        {
                            activity.AddTag(AttributeDbNamespace, database);
                        }
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
                            string? providerName = null;

                            if (this.dbContextFetcher.Fetch(payload) is { } dbContext)
                            {
                                var dbContextDatabase = this.dbContextDatabaseFetcher.Fetch(dbContext);
                                providerName = this.providerNameFetcher.Fetch(dbContextDatabase);
                            }

                            if (command is IDbCommand typedCommand && this.options.Filter?.Invoke(providerName, typedCommand) == false)
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
                                    if (this.options.SetDbStatementForStoredProcedure)
                                    {
                                        if (this.options.EmitOldAttributes)
                                        {
                                            activity.AddTag(AttributeDbStatement, commandText);
                                        }

                                        if (this.options.EmitNewAttributes)
                                        {
                                            activity.AddTag(AttributeDbQueryText, commandText);
                                        }
                                    }

                                    break;

                                case CommandType.Text:
                                    if (this.options.SetDbStatementForText)
                                    {
                                        if (this.options.EmitOldAttributes)
                                        {
                                            activity.AddTag(AttributeDbStatement, commandText);
                                        }

                                        if (this.options.EmitNewAttributes)
                                        {
                                            activity.AddTag(AttributeDbQueryText, commandText);
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
                                this.options.EnrichWithIDbCommand?.Invoke(activity, typedCommand);
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
                        return;
                    }

                    if (activity.Source == EntityFrameworkActivitySource)
                    {
                        activity.Stop();
                    }

                    // For some reason this EF event comes before the SQLClient SqlMicrosoftAfterExecuteCommand event.
                    // EF span should not be parent of any other span except SQLClient, because of that it can be closed safely.
                    // Can result in a slightly strange timeline where the EF span finishes before its child SQLClient but based on EventSource's it is true.
                    if (activity.Parent?.Source == EntityFrameworkActivitySource)
                    {
                        activity.Parent.Stop();
                    }
                }

                break;

            case EntityFrameworkCoreCommandError:
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

                    try
                    {
                        if (activity.IsAllDataRequested)
                        {
                            if (this.exceptionFetcher.Fetch(payload) is Exception exception)
                            {
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
                    }
                }

                break;
            default:
                break;
        }
    }
}
