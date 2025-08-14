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

                        this.AddSystemNameTag(activity, providerOrCommandName);

                        var dataSource = (string)this.dataSourceFetcher.Fetch(connection);
                        if (!string.IsNullOrEmpty(dataSource))
                        {
                            activity.AddTag(SemanticConventions.AttributeServerAddress, dataSource);
                        }

                        this.AddTag(activity, (SemanticConventions.AttributeDbName, SemanticConventions.AttributeDbNamespace), database);
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
                                        this.AddTag(activity, (SemanticConventions.AttributeDbStatement, SemanticConventions.AttributeDbQueryText), commandText);
                                    }

                                    break;

                                case CommandType.Text:
                                    if (this.options.SetDbStatementForText)
                                    {
                                        this.AddTag(activity, (SemanticConventions.AttributeDbStatement, SemanticConventions.AttributeDbQueryText), commandText);
                                    }

                                    break;

                                case CommandType.TableDirect:
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

    private void AddTag(Activity activity, (string Old, string New) attributes, string value)
        => this.AddTag(activity, attributes, (value, value));

    private void AddTag(Activity activity, (string Old, string New) attributes, (string Old, string New) values)
    {
        if (this.options.EmitOldAttributes)
        {
            activity.AddTag(attributes.Old, values.Old);
        }

        if (this.options.EmitNewAttributes)
        {
            activity.AddTag(attributes.New, values.New);
        }
    }

    private void AddSystemNameTag(Activity activity, string? providerOrCommandName)
    {
        string? value = providerOrCommandName switch
        {
            "Microsoft.EntityFrameworkCore.SqlServer" or "Microsoft.Data.SqlClient.SqlCommand" => "mssql",
            "Microsoft.EntityFrameworkCore.Cosmos" => "cosmosdb",
            "Microsoft.Data.Sqlite.SqliteCommand" => "sqlite",
            "Microsoft.EntityFrameworkCore.Sqlite" => "sqlite",
            "Devart.Data.SQLite.Entity.EFCore" => "sqlite",
            "MySql.Data.EntityFrameworkCore" => "mysql",
            "MySql.Data.MySqlClient.MySqlCommand" => "mysql",
            "Pomelo.EntityFrameworkCore.MySql" => "mysql",
            "Devart.Data.MySql.Entity.EFCore" => "mysql",
            "Devart.Data.MySql.MySqlCommand" => "mysql",
            "Npgsql.EntityFrameworkCore.PostgreSQL" => "postgresql",
            "Npgsql.NpgsqlCommand" => "postgresql",
            "Devart.Data.PostgreSql.Entity.EFCore" => "postgresql",
            "Devart.Data.PostgreSql.PgSqlCommand" => "postgresql",
            "Oracle.EntityFrameworkCore" => "oracle",
            "Oracle.ManagedDataAccess.Client.OracleCommand" => "oracle",
            "Devart.Data.Oracle.Entity.EFCore" => "oracle",
            "Devart.Data.Oracle.OracleCommand" => "oracle",
            "Microsoft.EntityFrameworkCore.InMemory" => "efcoreinmemory",
            "FirebirdSql.Data.FirebirdClient.FbCommand" => "firebird",
            "FirebirdSql.EntityFrameworkCore.Firebird" => "firebird",
            "FileContextCore" => "filecontextcore",
            "EntityFrameworkCore.SqlServerCompact35" => "mssqlcompact",
            "EntityFrameworkCore.SqlServerCompact40" => "mssqlcompact",
            "System.Data.SqlServerCe.SqlCeCommand" => "mssqlcompact",
            "EntityFrameworkCore.OpenEdge" => "openedge",
            "EntityFrameworkCore.Jet" => "jet",
            "EntityFrameworkCore.Jet.Data.JetCommand" => "jet",
            "Google.Cloud.EntityFrameworkCore.Spanner" => "spanner",
            "Google.Cloud.Spanner.Data.SpannerCommand" => "spanner",
            "Teradata.Client.Provider.TdCommand" => "teradata",
            "Teradata.EntityFrameworkCore" => "teradata",
            "EFCore.Snowflake" => "snowflake",
            "EFCore.Snowflake.Storage" => "snowflake",
            "EFCore.Snowflake.Storage.Internal" => "snowflake",
            _ => null,
        };

        if (value == null)
        {
            value = "other_sql";
            activity.AddTag("ef.provider", providerOrCommandName);
        }

        this.AddTag(activity, (SemanticConventions.AttributeDbSystem, SemanticConventions.AttributeDbSystemName), value);
    }
}
