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

    private static ConcurrentDictionary<Type, Func<IDbConnection, IDictionary?>?>? retrieveStatisticsCache;

    private readonly PropertyFetcher<IDbCommand> commandFetcher = new("Command");
    private readonly PropertyFetcher<Exception> exceptionFetcher = new("Exception");
    private readonly PropertyFetcher<int> exceptionNumberFetcher = new("Number");
    private readonly PropertyFetcher<IDictionary> statisticsFetcher = new("Statistics");
    private readonly PropertyFetcher<Guid> operationIdFetcher = new("OperationId");
    private readonly PropertyFetcher<string> operationFetcher = new("Operation");
    private readonly ConcurrentDictionary<Guid, BeginState> beginStates = new();

    public SqlClientDiagnosticListener(string sourceName)
        : base(sourceName)
    {
    }

    public override bool SupportsNullActivity => true;

    internal int PendingBeginStateCount => this.beginStates.Count;

    public override void OnEventWritten(string name, object? payload)
    {
        if (SqlClientInstrumentation.Instance.HandleManager.TracingHandles == 0
            && SqlClientInstrumentation.Instance.HandleManager.MetricHandles == 0)
        {
            // The instrumentation may have been disabled part way through a command's
            // execution, so make sure any timestamp entry is cleaned up.
            if (!this.beginStates.IsEmpty)
            {
                _ = this.TakeBeginState(payload);
            }

            return;
        }

        var options = SqlClientInstrumentation.Instance.GetTracingOptions();
        var activity = Activity.Current;
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

                    // Metrics-only fast path: if the ActivitySource has no listeners then StartActivity
                    // will always return null and no trace will be produced. Skip the (relatively
                    // expensive) connection tag derivation, query sanitization, filtering and enrichment
                    // entirely and only capture the start timestamp needed to compute the metric duration
                    // in the matching WriteCommandAfter/WriteCommandError event.
                    if (!SqlTelemetryHelper.ActivitySource.HasListeners())
                    {
                        this.RecordBeginState(payload);
                        return;
                    }

                    var connection = command.Connection;
                    var databaseName = connection?.Database;
                    var dataSource = (connection as DbConnection)?.DataSource;

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
                                ref activityName);
                            break;

                        case CommandType.TableDirect:
                        default:
                            break;
                    }

                    activity = SqlTelemetryHelper.ActivitySource.StartActivity(
                        activityName,
                        ActivityKind.Client,
                        default(ActivityContext),
                        startTags);

                    if (activity == null)
                    {
                        // There is no listener or it decided not to sample the current request.
                        this.RecordBeginState(payload);
                        return;
                    }

                    if (!TryFetchOperationId(this.operationIdFetcher, payload, out _))
                    {
                        // Correlation is keyed by OperationId. A malformed Begin cannot be
                        // safely matched later, so do not leave the newly-created activity
                        // current and untracked.
                        StopActivity(activity);
                        SqlClientInstrumentationEventSource.Log.NullActivity(name);
                        return;
                    }

                    this.RecordBeginState(payload, activity);

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

                    var hasOperationId = TryFetchOperationId(this.operationIdFetcher, payload, out _);
                    if (hasOperationId && this.TryGetBeginState(payload, out var beginState))
                    {
                        activity = beginState.Activity;
                    }
                    else
                    {
                        activity = null;
                    }

                    if (activity == null)
                    {
                        SqlClientInstrumentationEventSource.Log.NullActivity(name);
                        this.RecordDuration(null, payload);
                        return;
                    }

                    if (activity.Source != SqlTelemetryHelper.ActivitySource)
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

                    StopActivity(activity);
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

                    var hasOperationId = TryFetchOperationId(this.operationIdFetcher, payload, out _);
                    if (hasOperationId && this.TryGetBeginState(payload, out var beginState))
                    {
                        activity = beginState.Activity;
                    }
                    else
                    {
                        activity = null;
                    }

                    if (activity == null)
                    {
                        SqlClientInstrumentationEventSource.Log.NullActivity(name);
                        this.RecordDuration(null, payload);
                        return;
                    }

                    if (activity.Source != SqlTelemetryHelper.ActivitySource)
                    {
                        this.RecordDuration(null, payload);
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

                                activity.SetStatus(ActivityStatusCode.Error, exception.Message);

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
                        StopActivity(activity);
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
        // https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/3867
        // will change this code to use ActivityTraceFlags.RandomTraceId instead of 2.
        // If new enum values are added in the future the Fallback path will ensure
        // that the handling is functionally correct, but the switch should be updated
        // to include the new value(s) for better readability and performance where possible.
        return flags switch
        {
            ActivityTraceFlags.None => "00",
            ActivityTraceFlags.Recorded => "01",
            (ActivityTraceFlags)2 => "02",
            ActivityTraceFlags.Recorded | (ActivityTraceFlags)2 => "03",
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
    private static bool TryFetchOperationId(
        PropertyFetcher<Guid> fetcher,
        object? payload,
        out Guid operationId)
        => fetcher.TryFetch(payload, out operationId) && operationId != Guid.Empty;

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

    private static void StopActivity(Activity activity)
    {
        var currentActivity = Activity.Current;
        activity.Stop();
        if (!ReferenceEquals(currentActivity, activity))
        {
            Activity.Current = currentActivity;
        }
    }

    private void RecordBeginState(object? payload, Activity? activity = null)
    {
        if (TryFetchOperationId(this.operationIdFetcher, payload, out var operationId))
        {
            this.beginStates[operationId] = new(Stopwatch.GetTimestamp(), activity);
        }
    }

    private BeginState? TakeBeginState(object? payload) =>
        TryFetchOperationId(this.operationIdFetcher, payload, out var operationId)
            && this.beginStates.TryRemove(operationId, out var state)
                ? state
                : null;

    private bool TryGetBeginState(object? payload, out BeginState state)
    {
        if (TryFetchOperationId(this.operationIdFetcher, payload, out var operationId))
        {
            return this.beginStates.TryGetValue(operationId, out state);
        }

        state = default;
        return false;
    }

    private void RecordDuration(Activity? activity, object? payload, bool hasError = false)
    {
        // The pending start timestamp is always consumed, even when metrics are disabled, so that
        // entries cannot accumulate for the lifetime of the listener.
        var beginState = this.TakeBeginState(payload);

        if (SqlClientInstrumentation.Instance.HandleManager.MetricHandles == 0)
        {
            return;
        }

        double duration;
        if (activity != null)
        {
            duration = activity.Duration.TotalSeconds;
        }
        else if (beginState is { } state)
        {
            duration = SqlTelemetryHelper.CalculateDurationFromTimestamp(state.StartTimestamp);
        }
        else
        {
            // No start timestamp was captured for this command (for example the before event was
            // never seen because the instrumentation was enabled part way through the command), so
            // a duration cannot be computed. Recording an arbitrary value would skew the histogram.
            return;
        }

        var tags = default(TagList);

        if (activity != null && activity.IsAllDataRequested)
        {
            SqlTelemetryHelper.AddSharedTags(activity, ref tags);
        }
        else if (payload != null)
        {
            if (TryFetchCommand(this.commandFetcher, payload, out var command))
            {
                var connection = command.Connection;
                var databaseName = connection?.Database;
                var dataSource = (connection as DbConnection)?.DataSource;

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
                    tags.Add(SemanticConventions.AttributeDbStoredProcedureName, command.CommandText);
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

    private readonly struct BeginState(long startTimestamp, Activity? activity)
    {
        public long StartTimestamp { get; } = startTimestamp;

        public Activity? Activity { get; } = activity;
    }
}
#endif
