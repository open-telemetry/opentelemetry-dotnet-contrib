// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;
using ActivitySourceFactory = OpenTelemetry.Trace.ActivitySourceFactory;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Implementation;

internal sealed class EntityFrameworkDiagnosticListener : ListenerHandler
{
    internal const string DiagnosticSourceName = "Microsoft.EntityFrameworkCore";

    internal const string EntityFrameworkCoreCommandCreated = "Microsoft.EntityFrameworkCore.Database.Command.CommandCreated";
    internal const string EntityFrameworkCoreCommandExecuting = "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuting";
    internal const string EntityFrameworkCoreCommandExecuted = "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted";
    internal const string EntityFrameworkCoreCommandCanceled = "Microsoft.EntityFrameworkCore.Database.Command.CommandCanceled";
    internal const string EntityFrameworkCoreCommandError = "Microsoft.EntityFrameworkCore.Database.Command.CommandError";

    internal static readonly Version SemanticConventionsVersion = new(1, 24, 0);
    internal static readonly ActivitySource ActivitySource = ActivitySourceFactory.Create<EntityFrameworkDiagnosticListener>(SemanticConventionsVersion);

    private static readonly Version SemanticConventionsVersionNew = new(1, 36, 0);
    private static readonly ActivitySource ActivitySourceNew = ActivitySourceFactory.Create<EntityFrameworkDiagnosticListener>(SemanticConventionsVersionNew);

    private static readonly ActivitySource ActivitySourceBoth = ActivitySourceFactory.Create<EntityFrameworkDiagnosticListener>(null);

    private static readonly string ActivityName = ActivitySource.Name + ".Execute";

    private static readonly object FilteredCommandMarker = new();

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

    // Commands whose activity was discarded by the filter, removed again when the command completes.
    // Weak keys, so a command that never raises a completion event isn't kept alive.
#if NET
    private readonly ConditionalWeakTable<object, object> filteredCommands = [];
#else
    private readonly ConditionalWeakTable<object, object> filteredCommands = new();
#endif

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

        var activitySource =
            this.options.EmitNewAttributes && this.options.EmitOldAttributes ?
            ActivitySourceBoth :
            this.options.EmitNewAttributes ?
            ActivitySourceNew :
            ActivitySource;

        switch (name)
        {
            case EntityFrameworkCoreCommandCreated:
                {
                    activity = activitySource.StartActivity(ActivityName, ActivityKind.Client);
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

                        this.AddDbSystemNameTag(activity, providerOrCommandName);

                        var dataSource = (string)this.dataSourceFetcher.Fetch(connection);
                        if (!string.IsNullOrEmpty(dataSource))
                        {
                            var connectionDetails = SqlConnectionDetails.ParseFromDataSource(dataSource);

                            var serverAddress = connectionDetails.ServerHostName ?? connectionDetails.ServerIpAddress;
                            if (!string.IsNullOrEmpty(serverAddress))
                            {
                                this.AddTag(activity, ("peer.service", SemanticConventions.AttributeServerAddress), serverAddress);

                                if (this.options.EmitNewAttributes && connectionDetails.BoxedPort is { } port)
                                {
                                    activity.AddTag(SemanticConventions.AttributeServerPort, port);
                                }
                            }
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

                    if (activity.Source != activitySource)
                    {
                        return;
                    }

                    if (activity.IsAllDataRequested)
                    {
                        var command = this.commandFetcher.Fetch(payload);
                        string? providerName = null;

                        try
                        {
                            if (this.dbContextFetcher.Fetch(payload) is { } dbContext)
                            {
                                var dbContextDatabase = this.dbContextDatabaseFetcher.Fetch(dbContext);
                                providerName = this.providerNameFetcher.Fetch(dbContextDatabase);
                            }

                            if (command is IDbCommand typedCommand && this.options.Filter?.Invoke(providerName, typedCommand) == false)
                            {
                                EntityFrameworkInstrumentationEventSource.Log.CommandIsFilteredOut(activity.OperationName);
                                this.DiscardFilteredActivity(activity, command);
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            EntityFrameworkInstrumentationEventSource.Log.CommandFilterException(ex);
                            this.DiscardFilteredActivity(activity, command);
                            return;
                        }

                        if (this.options.EmitNewAttributes && this.options.SetDbQueryParameters)
                        {
                            SqlParameterProcessor.AddQueryParameters(activity, command);
                        }

                        if (this.commandTypeFetcher.Fetch(command) is CommandType commandType)
                        {
                            var commandText = this.commandTextFetcher.Fetch(command);
                            switch (commandType)
                            {
                                case CommandType.StoredProcedure:
                                    DatabaseSemanticConventionHelper.ApplyConventionsForStoredProcedure(
                                        activity,
                                        commandText,
                                        this.options.EmitOldAttributes,
                                        this.options.EmitNewAttributes);
                                    break;

                                case CommandType.Text:
                                    EntityFrameworkQueryTextProcessor.Apply(activity, command, commandText, providerName, this.options);
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
            case EntityFrameworkCoreCommandCanceled:
                {
                    // The activity of a filtered-out command was already stopped. Activity.Current is now
                    // its parent (possibly null, or the activity of an outer EF command), so leave it alone.
                    if (this.WasFilteredOut(payload))
                    {
                        return;
                    }

                    if (activity == null)
                    {
                        EntityFrameworkInstrumentationEventSource.Log.NullActivity(name);
                        return;
                    }

                    if (activity.Source == activitySource)
                    {
                        activity.Stop();
                    }

                    // For some reason this EF event comes before the SQLClient SqlMicrosoftAfterExecuteCommand event.
                    // EF span should not be parent of any other span except SQLClient, because of that it can be closed safely.
                    // Can result in a slightly strange timeline where the EF span finishes before its child SQLClient but based on EventSource's it is true.
                    if (activity.Parent?.Source == activitySource)
                    {
                        activity.Parent.Stop();
                    }
                }

                break;

            case EntityFrameworkCoreCommandError:
                {
                    if (this.WasFilteredOut(payload))
                    {
                        return;
                    }

                    if (activity == null)
                    {
                        EntityFrameworkInstrumentationEventSource.Log.NullActivity(name);
                        return;
                    }

                    if (activity.Source != activitySource)
                    {
                        return;
                    }

                    try
                    {
                        if (activity.IsAllDataRequested)
                        {
                            if (this.exceptionFetcher.Fetch(payload) is { } exception)
                            {
                                activity.SetStatus(ActivityStatusCode.Error);
                                activity.SetTag(SemanticConventions.AttributeErrorType, exception.GetType().FullName);
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

    internal static bool IsHandledEvent(string eventName) => eventName switch
    {
        EntityFrameworkCoreCommandCreated or
        EntityFrameworkCoreCommandExecuting or
        EntityFrameworkCoreCommandExecuted or
        EntityFrameworkCoreCommandCanceled or
        EntityFrameworkCoreCommandError => true,
        _ => false,
    };

    private void DiscardFilteredActivity(Activity activity, object? command)
    {
        activity.IsAllDataRequested = false;
        activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;

        // Stop the activity now so that Activity.Current reverts to its parent. If it stayed
        // current, spans started while the command executes (for example by an instrumented
        // ADO.NET provider such as Npgsql or SqlClient) would get a parent that is not recorded
        // and would be dropped by parent-based samplers.
        activity.Stop();

        // When the command completes, Activity.Current is this activity's parent: null, an unrelated
        // activity, or the activity of an outer EF command. Remember the command so the completion
        // callbacks neither report a missing activity nor stop or fail that parent.
        if (command != null && this.options.Filter != null)
        {
            _ = this.filteredCommands.GetValue(command, static _ => FilteredCommandMarker);
        }
    }

    // Commands are only marked when a filter is configured, so skip the lookup otherwise.
    // TryGetValue doesn't lock, so only commands that were actually filtered out pay for Remove.
    private bool WasFilteredOut(object? payload)
        => this.options.Filter != null
        && this.commandFetcher.Fetch(payload) is { } command
        && this.filteredCommands.TryGetValue(command, out _)
        && this.filteredCommands.Remove(command);

    private void AddTag(Activity activity, (string Old, string New) attributes, string? value)
        => this.AddTag(activity, attributes, (value, value));

    private void AddTag(Activity activity, (string Old, string New) attributes, (string? Old, string? New) values)
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

    private void AddDbSystemNameTag(Activity activity, string? providerOrCommandName)
    {
        var values = EntityFrameworkProviderInfo.GetDbSystemNames(providerOrCommandName);

        // Custom tag for backwards compatibility only
        if (this.options.EmitOldAttributes &&
            values == (DbSystems.OtherSql, DbSystemNames.OtherSql))
        {
            activity.AddTag("ef.provider", providerOrCommandName);
        }

        this.AddTag(activity, (SemanticConventions.AttributeDbSystem, SemanticConventions.AttributeDbSystemName), values);
    }
}
