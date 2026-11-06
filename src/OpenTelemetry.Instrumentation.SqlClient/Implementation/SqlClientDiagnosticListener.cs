// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;

#if NET
using System.Text;
#endif
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.SqlClient.Implementation;

internal sealed class SqlClientDiagnosticListener : ListenerHandler
{
    public const string SqlDataBeforeExecuteCommand = "System.Data.SqlClient.WriteCommandBefore";
    public const string SqlMicrosoftBeforeExecuteCommand = "Microsoft.Data.SqlClient.WriteCommandBefore";

    public const string SqlDataAfterExecuteCommand = "System.Data.SqlClient.WriteCommandAfter";
    public const string SqlMicrosoftAfterExecuteCommand = "Microsoft.Data.SqlClient.WriteCommandAfter";

    public const string SqlDataWriteCommandError = "System.Data.SqlClient.WriteCommandError";
    public const string SqlMicrosoftWriteCommandError = "Microsoft.Data.SqlClient.WriteCommandError";

#if NET
    private const string ContextInfoParameterName = "@opentelemetry_traceparent";
    private const string SetContextSql = $"set context_info {ContextInfoParameterName}";

    private const string IL2026Justification = "Client application usage will ensure that core types from usage are preserved.";
#endif

    private const string ReturnedRowsBaselinePropertyName = "otel.sqlclient.returned_rows_baseline";
    private const string ConnectionSnapshotPropertyName = "otel.sqlclient.connection_snapshot";

    private static ConcurrentDictionary<Type, Func<IDbConnection, IDictionary?>?>? retrieveStatisticsCache;

    private readonly PropertyFetcher<IDbCommand> commandFetcher = new("Command");
    private readonly PropertyFetcher<Exception> exceptionFetcher = new("Exception");
    private readonly PropertyFetcher<int> exceptionNumberFetcher = new("Number");
    private readonly PropertyFetcher<IDictionary> statisticsFetcher = new("Statistics");
    private readonly PropertyFetcher<string> operationFetcher = new("Operation");

    // The start timestamp and connection details of each command which has no activity, to compute the
    // duration metric when it completes. Keyed weakly so that a command which never writes a completion
    // event (for example if it was cancelled before it started) does not leave an entry which is never removed.
#if NET
    private readonly ConditionalWeakTable<IDbCommand, PendingCommand> pendingCommands = [];
#else
    private readonly ConditionalWeakTable<IDbCommand, PendingCommand> pendingCommands = new();
#endif

    // The activity of each command which is executing. The completion events of an asynchronous command
    // are written on a copy of the execution context of the caller, so the activity cannot be found again
    // through Activity.Current. Keyed weakly so the activity of a command which never writes a completion
    // event (for example if it was cancelled before it started) does not keep the command alive.
#if NET
    private readonly ConditionalWeakTable<IDbCommand, Activity> activities = [];
#else
    private readonly ConditionalWeakTable<IDbCommand, Activity> activities = new();
#endif

    // Set once an activity has been tracked, so that nothing needs to be looked up before then.
    private volatile bool hasTrackedActivities;

    // Likewise set once a pending command has been tracked.
    private volatile bool hasTrackedPendingCommands;

    public SqlClientDiagnosticListener(string sourceName)
        : base(sourceName)
    {
    }

    public override bool SupportsNullActivity => true;

#if NET
    internal int PendingCommandCount => this.pendingCommands.Count();
#endif

    public override void OnEventWritten(string name, object? payload)
    {
        if (SqlClientInstrumentation.Instance.HandleManager.TracingHandles == 0
            && SqlClientInstrumentation.Instance.HandleManager.MetricHandles == 0)
        {
            // The instrumentation may have been disabled part way through a command's
            // execution, so make sure any timestamp entry is cleaned up.
            _ = this.TakePendingCommand(payload);

            // Likewise release the activity of the command. One which is executing is stopped, and one left by
            // an execution which never completed is cleared so that a command which is executed again does not
            // pick it up if the instrumentation is enabled again before it completes.
            if (this.hasTrackedActivities)
            {
                if (IsCompletionEvent(name))
                {
                    _ = TryFetchCommand(this.commandFetcher, payload, out var completedCommand);

                    if (this.TakeActivity(completedCommand) is { } completedActivity)
                    {
                        SqlTelemetryHelper.StopActivity(completedActivity);
                    }
                }
                else if (name is SqlDataBeforeExecuteCommand or SqlMicrosoftBeforeExecuteCommand)
                {
                    _ = TryFetchCommand(this.commandFetcher, payload, out var startedCommand);
                    _ = this.TakeActivity(startedCommand);
                }
            }

            return;
        }

        var options = SqlClientInstrumentation.Instance.GetTracingOptions();
        switch (name)
        {
            case SqlDataBeforeExecuteCommand:
            case SqlMicrosoftBeforeExecuteCommand:
                {
                    if (!TryFetchCommand(this.commandFetcher, payload, out var command))
                    {
                        SqlClientInstrumentationEventSource.Log.NullPayload(nameof(SqlClientDiagnosticListener), name);
                        return;
                    }

#if NET
                    // skip if this is an injected query
                    if (options.EnableTraceContextPropagation &&
                        command.CommandType is CommandType.Text && command.CommandText == SetContextSql)
                    {
                        return;
                    }
#endif

                    // An execution which never completed (for example one with a token which was already
                    // cancelled) leaves its activity and start timestamp associated with the command, which a
                    // command that is executed again must not pick up when it completes, so they are cleared first.
                    if (this.hasTrackedActivities)
                    {
                        _ = this.activities.Remove(command);
                    }

                    if (this.hasTrackedPendingCommands)
                    {
                        _ = this.pendingCommands.Remove(command);
                    }

                    // Snapshot the connection details before the command executes. A connection which
                    // breaks while the command executes is closed before the error event is written,
                    // after which its Database reverts to the initial catalog of its connection string
                    // rather than the database the command was executed against.
                    var connection = command.Connection;
                    var databaseName = connection?.Database;
                    var dataSource = (connection as DbConnection)?.DataSource;

                    // Metrics-only fast path: if the ActivitySource has no listeners then StartActivity
                    // will always return null and no trace will be produced. Skip the (relatively
                    // expensive) connection tag derivation, query sanitization, filtering and enrichment
                    // entirely and only capture the start timestamp needed to compute the metric duration,
                    // and the connection details, for the matching WriteCommandAfter/WriteCommandError event.
                    if (!SqlTelemetryHelper.ActivitySource.HasListeners())
                    {
                        this.RecordPendingCommand(command, dataSource, databaseName);
                        return;
                    }

                    var startTags = SqlTelemetryHelper.GetTagListFromConnectionInfo(dataSource, databaseName, out var activityName);

                    var commandType = command.CommandType;
                    var commandText = command.CommandText;

                    switch (commandType)
                    {
                        case CommandType.StoredProcedure:
                            DatabaseSemanticConventionHelper.AddTagsForSamplingAndUpdateActivityNameForStoredProcedure(
                                ref startTags,
                                commandText,
                                ref activityName);
                            break;

                        case CommandType.Text:
                            DatabaseSemanticConventionHelper.AddTagsForSamplingAndUpdateActivityNameForQueryText(
                                ref startTags,
                                commandText,
                                ref activityName,
                                dialect: SqlProcessor.SqlDialect.SqlServer);
                            break;

                        case CommandType.TableDirect:
                        default:
                            break;
                    }

                    var callerActivity = Activity.Current;

                    var activity = SqlTelemetryHelper.ActivitySource.StartActivity(
                        activityName,
                        ActivityKind.Client,
                        default(ActivityContext),
                        startTags);

                    if (activity == null)
                    {
                        // There is no listener or it decided not to sample the current request.
                        this.RecordPendingCommand(command, dataSource, databaseName);
                        return;
                    }

                    // The event which completes an asynchronous command is written on a copy of the
                    // execution context of the caller, so stopping the activity there cannot restore the
                    // caller's current activity. Restore it here instead, otherwise the caller is left with
                    // the activity of the command as its current activity and every command it goes on to
                    // execute becomes a child of the previous one. The activity is found again by command.
                    Activity.Current = callerActivity;

                    this.activities.Add(command, activity);
                    this.hasTrackedActivities = true;

                    activity.SetCustomProperty(ConnectionSnapshotPropertyName, new ConnectionSnapshot(dataSource, databaseName));

                    // Snapshot the connection's cumulative returned row count before the command
                    // executes so that the after-handler can compute the per-command delta. The
                    // baseline is only captured for the executions whose row count is complete by
                    // the time the command finishes, which is also what makes the after-handler
                    // skip the others.
                    if (options.RecordReturnedRows &&
                        activity.IsAllDataRequested &&
                        TryFetchOperation(this.operationFetcher, payload, out var operation) &&
                        IsRowCountAvailableWhenCommandCompletes(operation))
                    {
                        activity.SetCustomProperty(ReturnedRowsBaselinePropertyName, GetConnectionReturnedRows(command));
                    }

#if NET
                    if (options.EnableTraceContextPropagation &&
                        command.CommandType is CommandType.Text && connection is { State: ConnectionState.Open })
                    {
                        using var setContextCommand = connection.CreateCommand();
                        setContextCommand.Transaction = command.Transaction;
                        setContextCommand.CommandText = SetContextSql;
                        setContextCommand.CommandType = CommandType.Text;
                        var parameter = setContextCommand.CreateParameter();
                        parameter.ParameterName = ContextInfoParameterName;

                        var tracedflags = FormatActivityTraceFlags(activity.ActivityTraceFlags);
                        var traceparent = $"00-{activity.TraceId.ToHexString()}-{activity.SpanId.ToHexString()}-{tracedflags}";

                        parameter.DbType = DbType.Binary;
                        parameter.Value = Encoding.UTF8.GetBytes(traceparent);
                        setContextCommand.Parameters.Add(parameter);

                        setContextCommand.ExecuteNonQuery();
                    }
#endif

                    if (activity.IsAllDataRequested)
                    {
                        try
                        {
                            if (options.Filter?.Invoke(command) == false)
                            {
                                SqlClientInstrumentationEventSource.Log.CommandIsFilteredOut(activity.OperationName);
                                activity.IsAllDataRequested = false;
                                activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            SqlClientInstrumentationEventSource.Log.CommandFilterException(ex);
                            activity.IsAllDataRequested = false;
                            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                            return;
                        }

                        if (options.SetDbQueryParameters)
                        {
                            SqlParameterProcessor.AddQueryParameters(activity, command);
                        }

                        try
                        {
                            options.EnrichWithSqlCommand?.Invoke(activity, command);
                        }
                        catch (Exception ex)
                        {
                            SqlClientInstrumentationEventSource.Log.EnrichmentException(ex);
                        }
                    }
                }

                break;
            case SqlDataAfterExecuteCommand:
            case SqlMicrosoftAfterExecuteCommand:
                {
                    _ = TryFetchCommand(this.commandFetcher, payload, out var command);

#if NET
                    // skip if this is an injected query
                    if (options.EnableTraceContextPropagation && command != null &&
                        command.CommandType is CommandType.Text && command.CommandText == SetContextSql)
                    {
                        return;
                    }
#endif

                    var activity = this.TakeActivity(command);

                    if (activity == null)
                    {
                        this.RecordDuration(null, payload);
                        return;
                    }

                    // The baseline is only present if the before-handler determined that the row
                    // count reported when the command completes is meaningful for this execution.
                    if (options.RecordReturnedRows &&
                        activity.IsAllDataRequested &&
                        activity.GetCustomProperty(ReturnedRowsBaselinePropertyName) is long baseline &&
                        TryFetchStatistics(this.statisticsFetcher, payload, out var statistics))
                    {
                        if (GetReturnedRowsDelta(baseline, statistics) is { } returnedRows)
                        {
                            activity.SetTag(SemanticConventions.AttributeDbResponseReturnedRows, returnedRows);
                        }
                    }

                    SqlTelemetryHelper.StopActivity(activity);
                    this.RecordDuration(activity, payload);
                }

                break;
            case SqlDataWriteCommandError:
            case SqlMicrosoftWriteCommandError:
                {
                    _ = TryFetchCommand(this.commandFetcher, payload, out var command);

#if NET
                    // skip if this is an injected query
                    if (options.EnableTraceContextPropagation && command != null &&
                        command.CommandType is CommandType.Text && command.CommandText == SetContextSql)
                    {
                        return;
                    }
#endif

                    var activity = this.TakeActivity(command);

                    if (activity == null)
                    {
                        this.RecordDuration(null, payload, hasError: true);
                        return;
                    }

                    try
                    {
                        if (activity.IsAllDataRequested)
                        {
                            if (TryFetchException(this.exceptionFetcher, payload, out var exception))
                            {
                                activity.AddTag(SemanticConventions.AttributeErrorType, exception.GetType().FullName);

                                if (TryFetchExceptionNumber(this.exceptionNumberFetcher, exception, out var exceptionNumber))
                                {
                                    activity.AddTag(SemanticConventions.AttributeDbResponseStatusCode, exceptionNumber.ToString(CultureInfo.InvariantCulture));
                                }

                                activity.SetStatus(ActivityStatusCode.Error);

                                if (options.RecordException)
                                {
                                    activity.AddException(exception);
                                }
                            }
                            else
                            {
                                SqlClientInstrumentationEventSource.Log.NullPayload(nameof(SqlClientDiagnosticListener), name);
                            }
                        }
                    }
                    finally
                    {
                        SqlTelemetryHelper.StopActivity(activity);
                        this.RecordDuration(activity, payload, hasError: true);
                    }
                }

                break;
            default:
                break;
        }
    }

    private static string FormatActivityTraceFlags(ActivityTraceFlags flags)
    {
        // If new enum values are added in the future the Fallback path will ensure
        // that the handling is functionally correct, but the switch should be updated
        // to include the new value(s) for better readability and performance where possible.
        return flags switch
        {
            ActivityTraceFlags.None => "00",
            ActivityTraceFlags.Recorded => "01",
            (ActivityTraceFlags)2 => "02",
#if NET11_0_OR_GREATER
            ActivityTraceFlags.Recorded | ActivityTraceFlags.RandomTraceId => "03",
#else
            ActivityTraceFlags.Recorded | (ActivityTraceFlags)2 => "03",
#endif
            _ => Fallback((byte)flags),
        };

        static string Fallback(byte flags)
        {
            // IDE0302 suppressed as benchmarking showed that the explicitly stackalloc'd variant was more performant
#pragma warning disable IDE0302 // Simplify collection initialization
            Span<char> buffer = stackalloc char[2];
#pragma warning restore IDE0302 // Simplify collection initialization

            buffer[0] = GetHexChar(flags >> 4);
            buffer[1] = GetHexChar(flags & 0xF);

            return buffer.ToString();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static char GetHexChar(int value)
            {
                return (char)(value + (value < 10 ? '0' : 'a' - 10));
            }
        }
    }

#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = IL2026Justification)]
#endif
    private static bool TryFetchCommand(
        PropertyFetcher<IDbCommand> fetcher,
        object? payload,
        [NotNullWhen(true)] out IDbCommand? command)
        => fetcher.TryFetch(payload, out command);

#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = IL2026Justification)]
#endif
    private static bool TryFetchException(
        PropertyFetcher<Exception> fetcher,
        object? payload,
        [NotNullWhen(true)] out Exception? exception)
        => fetcher.TryFetch(payload, out exception);

#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = IL2026Justification)]
#endif
    private static bool TryFetchExceptionNumber(
        PropertyFetcher<int> fetcher,
        Exception exception,
        out int number)
        => fetcher.TryFetch(exception, out number);

#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = IL2026Justification)]
#endif
    private static bool TryFetchStatistics(
        PropertyFetcher<IDictionary> fetcher,
        object? payload,
        [NotNullWhen(true)] out IDictionary? statistics)
        => fetcher.TryFetch(payload, out statistics) && statistics != null;

#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = IL2026Justification)]
#endif
    private static bool TryFetchOperation(
        PropertyFetcher<string> fetcher,
        object? payload,
        [NotNullWhen(true)] out string? operation)
        => fetcher.TryFetch(payload, out operation) && operation != null;

    // The connection statistics are only updated as the response from the server is consumed, which
    // for the reader-based executions (ExecuteReader and ExecuteXmlReader) happens once the command
    // has completed and the activity has already been stopped. The row counts observed when those
    // commands complete therefore do not describe the rows the operation returns, so the row counts
    // are only used for the executions which consume the response before the command completes:
    // ExecuteNonQuery (rows affected) and ExecuteScalar (the single row that is read).
    //
    // SqlClient derives the operation name from the name of the member which wrote the event (using
    // CallerMemberName), so the members which implement the asynchronous overloads are matched by
    // looking for the relevant substring rather than by an exact name. For example, executing a
    // command with ExecuteNonQueryAsync() reports InternalExecuteNonQueryAsync for the before event
    // and CleanupAfterExecuteNonQueryAsync for the after event.
    private static bool IsRowCountAvailableWhenCommandCompletes(string operation) =>
        operation.Contains("NonQuery", StringComparison.Ordinal) ||
        operation.Contains("Scalar", StringComparison.Ordinal);

    // Both System.Data.SqlClient.SqlConnection and Microsoft.Data.SqlClient.SqlConnection
    // expose a public RetrieveStatistics() method that returns an IDictionary snapshot of
    // the connection's cumulative statistics. We look the method up once per concrete
    // connection type and cache a delegate so that subsequent calls avoid repeated
    // reflection lookups.
    private static long GetConnectionReturnedRows(IDbCommand command)
    {
        var connection = command.Connection;
        if (connection == null)
        {
            return 0L;
        }

        try
        {
            retrieveStatisticsCache ??= [];
            var retrieve = retrieveStatisticsCache.GetOrAdd(connection.GetType(), CreateRetrieveStatisticsDelegate);
            if (retrieve?.Invoke(connection) is IDictionary stats && stats["SelectRows"] is long selectRows)
            {
                return selectRows;
            }
        }
        catch
        {
            // Statistics not available from this connection type; baseline defaults to zero.
        }

        return 0L;
    }

#if NET
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = IL2026Justification)]
#endif
    private static Func<IDbConnection, IDictionary?>? CreateRetrieveStatisticsDelegate(Type connectionType)
    {
        var method = connectionType.GetMethod("RetrieveStatistics", Type.EmptyTypes);
        return method == null
            ? null
            : connection => method.Invoke(connection, null) as IDictionary;
    }

    // db.response.returned_rows describes the rows the operation returned, so only the
    // connection's SelectRows counter is used. The statistics also report the number of rows
    // affected by data manipulation commands (IduRows), but rows affected by an
    // INSERT/UPDATE/DELETE are not rows returned by it, so that counter is deliberately
    // ignored: a command which returns no rows reports 0, including when it affected rows.
    //
    // The counter is cumulative for the lifetime of the connection, so the pre-command
    // baseline is subtracted to obtain the number of rows returned by this command.
    private static long? GetReturnedRowsDelta(long baseline, IDictionary statistics)
        => statistics["SelectRows"] is long selectRows ? selectRows - baseline : null;

    private static bool IsCompletionEvent(string name) =>
        name is SqlDataAfterExecuteCommand or SqlMicrosoftAfterExecuteCommand
            or SqlDataWriteCommandError or SqlMicrosoftWriteCommandError;

    private Activity? TakeActivity(IDbCommand? command) =>
        command != null && this.activities.TryGetValue(command, out var activity) && this.activities.Remove(command)
            ? activity
            : null;

    private void RecordPendingCommand(IDbCommand command, string? dataSource, string? databaseName)
    {
        // A command only executes one operation at a time, so any existing entry is stale
        // (it was cleared before the command started, but never throw on a duplicate key).
        var pendingCommand = new PendingCommand(Stopwatch.GetTimestamp(), dataSource, databaseName);
#if NET
        this.pendingCommands.AddOrUpdate(command, pendingCommand);
#else
        this.pendingCommands.Add(command, pendingCommand);
#endif
        this.hasTrackedPendingCommands = true;
    }

    private PendingCommand? TakePendingCommand(object? payload) =>
        this.hasTrackedPendingCommands
        && TryFetchCommand(this.commandFetcher, payload, out var command)
        && this.pendingCommands.TryGetValue(command, out var pendingCommand)
        && this.pendingCommands.Remove(command)
            ? pendingCommand
            : null;

    private void RecordDuration(Activity? activity, object? payload, bool hasError = false)
    {
        // The pending command is always consumed, even when metrics are disabled, so that
        // entries cannot accumulate for the lifetime of the listener.
        var pendingCommand = this.TakePendingCommand(payload);

        if (SqlClientInstrumentation.Instance.HandleManager.MetricHandles == 0)
        {
            return;
        }

        double duration;
        if (activity != null)
        {
            duration = activity.Duration.TotalSeconds;
        }
        else if (pendingCommand is { } pending)
        {
            duration = SqlTelemetryHelper.CalculateDurationFromTimestamp(pending.BeginTimestamp);
        }
        else
        {
            // No start timestamp was captured for this command (for example the before event was
            // never seen because the instrumentation was enabled part way through the command), so
            // a duration cannot be computed. Recording an arbitrary value would skew the histogram.
            return;
        }

        var tags = default(TagList);

        // The tags are always derived from the command, and never read back from the activity. The tags
        // of an activity can also be set by processors (for example from baggage) and by enrichment, which
        // must not be able to change the dimensions of the metric. The tags derived here are the same as
        // the ones the activity is created with, so they do not depend on the sampling decision.
        if (payload != null)
        {
            if (TryFetchCommand(this.commandFetcher, payload, out var command))
            {
                string? databaseName;
                string? dataSource;

                if (activity?.GetCustomProperty(ConnectionSnapshotPropertyName) is ConnectionSnapshot snapshot)
                {
                    dataSource = snapshot.DataSource;
                    databaseName = snapshot.DatabaseName;
                }
                else if (pendingCommand is { } pending)
                {
                    dataSource = pending.DataSource;
                    databaseName = pending.DatabaseName;
                }
                else
                {
                    var connection = command.Connection;
                    databaseName = connection?.Database;
                    dataSource = (connection as DbConnection)?.DataSource;
                }

                var connectionTags = SqlTelemetryHelper.GetTagListFromConnectionInfo(
                    dataSource,
                    databaseName,
                    out _);

                foreach (var tag in connectionTags)
                {
                    tags.Add(tag.Key, tag.Value);
                }

                if (command.CommandType is CommandType.StoredProcedure)
                {
                    // Add the same tags as an activity for the command is created with.
                    var ignored = string.Empty;
                    DatabaseSemanticConventionHelper.AddTagsForSamplingAndUpdateActivityNameForStoredProcedure(
                        ref tags,
                        command.CommandText,
                        ref ignored);
                }
                else if (command.CommandType is CommandType.Text)
                {
                    var querySummary = SqlProcessor.GetSanitizedSql(command.CommandText, SqlProcessor.SqlDialect.SqlServer).DbQuerySummary;
                    if (!string.IsNullOrEmpty(querySummary))
                    {
                        tags.Add(SemanticConventions.AttributeDbQuerySummary, querySummary);
                    }
                }
            }

            if (hasError)
            {
                if (TryFetchException(this.exceptionFetcher, payload, out var exception))
                {
                    tags.Add(SemanticConventions.AttributeErrorType, exception.GetType().FullName);

                    if (TryFetchExceptionNumber(this.exceptionNumberFetcher, exception, out var exceptionNumber))
                    {
                        tags.Add(SemanticConventions.AttributeDbResponseStatusCode, exceptionNumber.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }
        }

        SqlTelemetryHelper.DbClientOperationDuration.Record(duration, tags);
    }

    private sealed class PendingCommand(long beginTimestamp, string? dataSource, string? databaseName)
    {
        public long BeginTimestamp { get; } = beginTimestamp;

        public string? DataSource { get; } = dataSource;

        public string? DatabaseName { get; } = databaseName;
    }

    private sealed class ConnectionSnapshot(string? dataSource, string? databaseName)
    {
        public string? DataSource { get; } = dataSource;

        public string? DatabaseName { get; } = databaseName;
    }
}
#endif
