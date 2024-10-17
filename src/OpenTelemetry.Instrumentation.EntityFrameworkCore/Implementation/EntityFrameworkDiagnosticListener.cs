// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using System.Diagnostics;
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
    internal static readonly string ActivitySourceName = Assembly.GetName().Name;
    internal static readonly string ActivityName = ActivitySourceName + ".Execute";
    internal static readonly ActivitySource SqlClientActivitySource = new(ActivitySourceName, Assembly.GetPackageVersion());

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
        Activity? activity = Activity.Current;

        switch (name)
        {
            case EntityFrameworkCoreCommandCreated:
                {
                    activity = SqlClientActivitySource.StartActivity(ActivityName, ActivityKind.Client);
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
                        var dbContext = this.dbContextFetcher.Fetch(payload);
                        var dbContextDatabase = this.dbContextDatabaseFetcher.Fetch(dbContext);
                        var providerName = this.providerNameFetcher.Fetch(dbContextDatabase);

                        switch (providerName)
                        {
                            case "Microsoft.EntityFrameworkCore.SqlServer":
                                activity.AddTag(AttributeDbSystem, "mssql");
                                break;
                            case "Microsoft.EntityFrameworkCore.Cosmos":
                                activity.AddTag(AttributeDbSystem, "cosmosdb");
                                break;
                            case "Microsoft.EntityFrameworkCore.Sqlite":
                            case "Devart.Data.SQLite.EFCore":
                                activity.AddTag(AttributeDbSystem, "sqlite");
                                break;
                            case "MySql.Data.EntityFrameworkCore":
                            case "Pomelo.EntityFrameworkCore.MySql":
                            case "Devart.Data.MySql.EFCore":
                                activity.AddTag(AttributeDbSystem, "mysql");
                                break;
                            case "Npgsql.EntityFrameworkCore.PostgreSQL":
                            case "Devart.Data.PostgreSql.EFCore":
                                activity.AddTag(AttributeDbSystem, "postgresql");
                                break;
                            case "Oracle.EntityFrameworkCore":
                            case "Devart.Data.Oracle.EFCore":
                                activity.AddTag(AttributeDbSystem, "oracle");
                                break;
                            case "Microsoft.EntityFrameworkCore.InMemory":
                                activity.AddTag(AttributeDbSystem, "efcoreinmemory");
                                break;
                            case "FirebirdSql.EntityFrameworkCore.Firebird":
                                activity.AddTag(AttributeDbSystem, "firebird");
                                break;
                            case "FileContextCore":
                                activity.AddTag(AttributeDbSystem, "filecontextcore");
                                break;
                            case "EntityFrameworkCore.SqlServerCompact35":
                            case "EntityFrameworkCore.SqlServerCompact40":
                                activity.AddTag(AttributeDbSystem, "mssqlcompact");
                                break;
                            case "EntityFrameworkCore.OpenEdge":
                                activity.AddTag(AttributeDbSystem, "openedge");
                                break;
                            case "EntityFrameworkCore.Jet":
                                activity.AddTag(AttributeDbSystem, "jet");
                                break;
                            case "Google.Cloud.EntityFrameworkCore.Spanner":
                                activity.AddTag(AttributeDbSystem, "spanner");
                                break;
                            case "Teradata.EntityFrameworkCore":
                                activity.AddTag(AttributeDbSystem, "teradata");
                                break;
                            default:
                                activity.AddTag(AttributeDbSystem, "other_sql");
                                activity.AddTag("ef.provider", providerName);
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

                    if (activity.Source != SqlClientActivitySource)
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

                    if (activity.Source != SqlClientActivitySource)
                    {
                        return;
                    }

                    activity.Stop();
                }

                break;

            case EntityFrameworkCoreCommandError:
                {
                    if (activity == null)
                    {
                        EntityFrameworkInstrumentationEventSource.Log.NullActivity(name);
                        return;
                    }

                    if (activity.Source != SqlClientActivitySource)
                    {
                        return;
                    }

                    try
                    {
                        if (activity.IsAllDataRequested)
                        {
                            if (this.exceptionFetcher.Fetch(payload) is Exception exception)
                            {
                                activity.SetStatus(Status.Error.WithDescription(exception.Message));
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
        }
    }
}
