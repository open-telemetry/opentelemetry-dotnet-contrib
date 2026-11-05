// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Diagnostics;
using System.Diagnostics.Tracing;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.SqlClient.Implementation;

/// <summary>
/// On .NET Framework, neither System.Data.SqlClient nor Microsoft.Data.SqlClient emit DiagnosticSource events.
/// Instead they use EventSource:
/// For System.Data.SqlClient see: <a href="https://github.com/microsoft/referencesource/blob/3b1eaf5203992df69de44c783a3eda37d3d4cd10/System.Data/System/Data/Common/SqlEventSource.cs#L29">reference source</a>.
/// For Microsoft.Data.SqlClient see: <a href="https://github.com/dotnet/SqlClient/blob/ac8bb3f9132e6c104dc3e307fe2d569daed0776f/src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlClientEventSource.cs#L15">SqlClientEventSource</a>.
///
/// We hook into these event sources and process their BeginExecute/EndExecute events.
/// </summary>
/// <remarks>
/// Note that before version 2.0.0, Microsoft.Data.SqlClient used
/// "Microsoft-AdoNet-SystemData" (same as System.Data.SqlClient), but since
/// 2.0.0 has switched to "Microsoft.Data.SqlClient.EventSource".
/// </remarks>
internal sealed class SqlEventSourceListener : EventListener
{
    internal const string AdoNetEventSourceName = "Microsoft-AdoNet-SystemData";
    internal const string MdsEventSourceName = "Microsoft.Data.SqlClient.EventSource";

    internal const int BeginExecuteEventId = 1;
    internal const int EndExecuteEventId = 2;

    /// <summary>
    /// The keyword the BeginExecute and EndExecute events are written with.
    /// </summary>
    /// <remarks>
    /// This is <c>Keywords.SqlClient</c> for "Microsoft-AdoNet-SystemData" (see the
    /// <a href="https://github.com/microsoft/referencesource/blob/3b1eaf5203992df69de44c783a3eda37d3d4cd10/System.Data/System/Data/Common/SqlEventSource.cs#L39-L42">reference source</a>
    /// and <a href="https://github.com/dotnet/SqlClient/blob/v1.1.0/src/Microsoft.Data.SqlClient/netfx/src/Microsoft/Data/SqlEventSource.cs#L35-L38">Microsoft.Data.SqlClient v1.1.0</a>)
    /// and <c>Keywords.ExecutionTrace</c> for "Microsoft.Data.SqlClient.EventSource" (see
    /// <a href="https://github.com/dotnet/SqlClient/blob/v2.0.0/src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlClientEventSource.cs#L147-L152">Microsoft.Data.SqlClient v2.0.0</a>),
    /// both of which have the same value in every version.
    /// <para/>
    /// Only the events with this keyword are enabled, rather than all of them, as
    /// Microsoft.Data.SqlClient writes many other trace events (for example for scopes,
    /// connection pooling and SNI) which are not used, but would still be delivered to
    /// (and allocate for) this listener for every command if they were enabled.
    /// </remarks>
    internal const EventKeywords ExecuteEventKeywords = (EventKeywords)1;

    /// <summary>
    /// The maximum number of commands tracked between their BeginExecute and EndExecute events.
    /// </summary>
    /// <remarks>
    /// SqlClient does not write EndExecute for an asynchronous execution which fails before the
    /// command is sent (for example because a parameter value cannot be converted, or the
    /// connection is not open), and the events only identify the command by an id which is never
    /// reused, so without a limit such commands would be tracked for the lifetime of the listener.
    /// </remarks>
    internal const int MaxPendingCommands = 10_000;

    private readonly Dictionary<(EventSource EventSource, int ObjectId), PendingCommand> pendingCommands = [];
    private readonly Lock pendingCommandsLock = new();
    private EventSource? adoNetEventSource;
    private EventSource? mdsEventSource;

    internal int PendingCommandCount
    {
        get
        {
            lock (this.pendingCommandsLock)
            {
                return this.pendingCommands.Count;
            }
        }
    }

    public override void Dispose()
    {
        if (this.adoNetEventSource != null)
        {
            this.DisableEvents(this.adoNetEventSource);
        }

        if (this.mdsEventSource != null)
        {
            this.DisableEvents(this.mdsEventSource);
        }

        base.Dispose();
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource?.Name.StartsWith(AdoNetEventSourceName, StringComparison.Ordinal) == true)
        {
            this.adoNetEventSource = eventSource;
            this.EnableEvents(eventSource, EventLevel.Informational, ExecuteEventKeywords);
        }
        else if (eventSource?.Name.StartsWith(MdsEventSourceName, StringComparison.Ordinal) == true)
        {
            this.mdsEventSource = eventSource;
            this.EnableEvents(eventSource, EventLevel.Informational, ExecuteEventKeywords);
        }

        base.OnEventSourceCreated(eventSource);
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        try
        {
            if (eventData.EventId == BeginExecuteEventId)
            {
                this.OnBeginExecute(eventData);
            }
            else if (eventData.EventId == EndExecuteEventId)
            {
                this.OnEndExecute(eventData);
            }
        }
        catch (Exception exc)
        {
            SqlClientInstrumentationEventSource.Log.UnknownErrorProcessingEvent(nameof(SqlEventSourceListener), nameof(this.OnEventWritten), exc);
        }
    }

    private static (bool HasError, string? ErrorNumber, string? ExceptionType) ExtractErrorFromEvent(EventWrittenEventArgs eventData)
    {
        var compositeState = (int)eventData.Payload[1];

        if ((compositeState & 0b001) != 0b001)
        {
            if ((compositeState & 0b010) == 0b010)
            {
                var errorNumber = $"{eventData.Payload[2]}";
                var exceptionType = eventData.EventSource.Name == MdsEventSourceName
                    ? "Microsoft.Data.SqlClient.SqlException"
                    : "System.Data.SqlClient.SqlException";
                return (true, errorNumber, exceptionType);
            }
            else
            {
                return (true, null, null);
            }
        }

        return (false, null, null);
    }

    private static void AddErrorTags(EventWrittenEventArgs eventData, ref TagList tags)
    {
        var (hasError, errorNumber, exceptionType) = ExtractErrorFromEvent(eventData);

        if (hasError && errorNumber != null && exceptionType != null)
        {
            tags.Add(SemanticConventions.AttributeDbResponseStatusCode, errorNumber);
            tags.Add(SemanticConventions.AttributeErrorType, exceptionType);
        }
    }

    private static void RecordDuration(Activity? activity, EventWrittenEventArgs eventData, in PendingCommand pendingCommand)
    {
        if (SqlClientInstrumentation.Instance.HandleManager.MetricHandles == 0)
        {
            return;
        }

        double duration;
        if (activity != null)
        {
            duration = activity.Duration.TotalSeconds;
        }
        else if (pendingCommand.BeginTimestamp is { } begin)
        {
            duration = SqlTelemetryHelper.CalculateDurationFromTimestamp(begin);
        }
        else
        {
            // No start timestamp was captured for this command (for example the before event was
            // never seen because the instrumentation was enabled part way through the command),
            // so a duration cannot be computed. Recording an arbitrary value would skew the histogram.
            return;
        }

        // The tags are always derived from the command, and never read back from the activity. The tags
        // of an activity can also be set by processors (for example from baggage) and by enrichment, which
        // must not be able to change the dimensions of the metric. The tags derived here are the same as
        // the ones the activity is created with, so they do not depend on the sampling decision.
        var tags = SqlTelemetryHelper.GetTagListFromConnectionInfo(pendingCommand.DataSource, pendingCommand.DatabaseName, out _);

        AddErrorTags(eventData, ref tags);

        if (!string.IsNullOrEmpty(pendingCommand.QuerySummary))
        {
            tags.Add(SemanticConventions.AttributeDbQuerySummary, pendingCommand.QuerySummary);
        }

        SqlTelemetryHelper.DbClientOperationDuration.Record(duration, tags);
    }

    private void OnBeginExecute(EventWrittenEventArgs eventData)
    {
        /*
           Expected payload:
            [0] -> ObjectId
            [1] -> DataSource
            [2] -> Database
            [3] -> CommandText

            Note:
            - For "Microsoft-AdoNet-SystemData" v1.0: [3] CommandText = CommandType == CommandType.StoredProcedure ? CommandText : string.Empty; (so it is set for only StoredProcedure command types)
                (https://github.com/dotnet/SqlClient/blob/v1.0.19239.1/src/Microsoft.Data.SqlClient/netfx/src/Microsoft/Data/SqlClient/SqlCommand.cs#L6369)
            - For "Microsoft-AdoNet-SystemData" v1.1: [3] CommandText = sqlCommand.CommandText (so it is set for all command types)
                (https://github.com/dotnet/SqlClient/blob/v1.1.0/src/Microsoft.Data.SqlClient/netfx/src/Microsoft/Data/SqlClient/SqlCommand.cs#L7459)
            - For "Microsoft.Data.SqlClient.EventSource" v2.0+: [3] CommandText = sqlCommand.CommandText (so it is set for all command types).
                (https://github.com/dotnet/SqlClient/blob/f4568ce68da21db3fe88c0e72e1287368aaa1dc8/src/Microsoft.Data.SqlClient/netcore/src/Microsoft/Data/SqlClient/SqlCommand.cs#L6641)
         */

        if (SqlClientInstrumentation.Instance.HandleManager.TracingHandles == 0
            && SqlClientInstrumentation.Instance.HandleManager.MetricHandles == 0)
        {
            // Clear the command of an execution which never ended, so that it is not used for this one if the
            // instrumentation is enabled again before this one ends.
            if (eventData.Payload.Count > 0 && eventData.Payload[0] is int disabledObjectId)
            {
                _ = this.TryRemovePendingCommand((eventData.EventSource, disabledObjectId), out _);
            }

            return;
        }

        if (eventData.Payload.Count < 4)
        {
            SqlClientInstrumentationEventSource.Log.InvalidPayload(nameof(SqlEventSourceListener), nameof(this.OnBeginExecute));
            return;
        }

        var correlationKey = (eventData.EventSource, ObjectId: (int)eventData.Payload[0]);
        _ = this.TryRemovePendingCommand(correlationKey, out _);
        var dataSource = (string)eventData.Payload[1];
        var databaseName = (string)eventData.Payload[2];
        var commandText = (string)eventData.Payload[3];
        SqlStatementInfo sqlStatementInfo = default;
        if (!string.IsNullOrEmpty(commandText))
        {
            sqlStatementInfo = SqlProcessor.GetSanitizedSql(commandText, SqlProcessor.SqlDialect.SqlServer);
        }

        // Metrics-only fast path: if the ActivitySource has no listeners then StartActivity
        // will always return null and no trace will be produced, so skip the connection tag
        // derivation until the duration is recorded.
        if (!SqlTelemetryHelper.ActivitySource.HasListeners())
        {
            this.AddPendingCommand(correlationKey, new(Stopwatch.GetTimestamp(), sqlStatementInfo.DbQuerySummary, dataSource, databaseName, null));
            return;
        }

        var startTags = SqlTelemetryHelper.GetTagListFromConnectionInfo(dataSource, databaseName, out var activityName);
        if (!string.IsNullOrEmpty(commandText))
        {
            startTags.Add(SemanticConventions.AttributeDbQueryText, sqlStatementInfo.SanitizedSql);
            if (!string.IsNullOrEmpty(sqlStatementInfo.DbQuerySummary))
            {
                startTags.Add(SemanticConventions.AttributeDbQuerySummary, sqlStatementInfo.DbQuerySummary);
                activityName = sqlStatementInfo.DbQuerySummary;
            }
        }

        var callerActivity = Activity.Current;

        var activity = SqlTelemetryHelper.ActivitySource.StartActivity(
            activityName,
            ActivityKind.Client,
            default(ActivityContext),
            startTags);

        // The event which ends an asynchronous command may be written on a copy of the execution context
        // of the caller, so stopping the activity there cannot restore the caller's current activity.
        // Restore it here instead, otherwise the caller is left with the activity of the command as its
        // current activity and every command it goes on to execute becomes a child of the previous one.
        // The activity is found again through the pending command.
        if (activity != null)
        {
            Activity.Current = callerActivity;
        }

        // If there is no activity because there is no listener or it decided not to sample the current
        // request, or the activity was only started for another listener while only metrics are being
        // collected, the start time needs to be tracked to calculate the duration. Otherwise, the duration
        // is taken from the activity, but the details of the command are still tracked in case the
        // activity is not recorded.
        this.AddPendingCommand(
            correlationKey,
            new(
                activity == null || SqlClientInstrumentation.Instance.HandleManager.TracingHandles == 0 ? Stopwatch.GetTimestamp() : null,
                sqlStatementInfo.DbQuerySummary,
                dataSource,
                databaseName,
                activity));
    }

    private void OnEndExecute(EventWrittenEventArgs eventData)
    {
        /*
           Expected payload:
            [0] -> ObjectId
            [1] -> CompositeState bitmask (0b001 -> successFlag, 0b010 -> isSqlExceptionFlag , 0b100 -> synchronousFlag)
            [2] -> SqlExceptionNumber
         */

        if (eventData.Payload.Count < 3)
        {
            SqlClientInstrumentationEventSource.Log.InvalidPayload(nameof(SqlEventSourceListener), nameof(this.OnEndExecute));
            return;
        }

        var correlationKey = (eventData.EventSource, ObjectId: (int)eventData.Payload[0]);
        _ = this.TryRemovePendingCommand(correlationKey, out var pendingCommand);

        var handleManager = SqlClientInstrumentation.Instance.HandleManager;

        if (handleManager.TracingHandles == 0 && handleManager.MetricHandles == 0)
        {
            // The instrumentation was disabled while the command was executing.
            if (pendingCommand.Activity is { } abandonedActivity)
            {
                SqlTelemetryHelper.StopActivity(abandonedActivity);
            }

            return;
        }

        // Ensure any activity that may exist due to ActivitySource.AddActivityListener()
        // is stopped regardless of whether we're doing metrics and/or tracing.
        // See https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/3033.
        var sqlActivity = pendingCommand.Activity;

        // If we're only collecting metrics, then we don't want to modify the activity
        var traceActivity =
            handleManager.TracingHandles == 0 && handleManager.MetricHandles != 0
                ? null
                : sqlActivity;

        try
        {
            if (traceActivity?.IsAllDataRequested is true)
            {
                var (hasError, errorNumber, exceptionType) = ExtractErrorFromEvent(eventData);

                if (hasError)
                {
                    if (errorNumber != null && exceptionType != null)
                    {
                        traceActivity.SetStatus(ActivityStatusCode.Error, errorNumber);
                        traceActivity.SetTag(SemanticConventions.AttributeDbResponseStatusCode, errorNumber);
                        traceActivity.SetTag(SemanticConventions.AttributeErrorType, exceptionType);
                    }
                    else
                    {
                        traceActivity.SetStatus(ActivityStatusCode.Error, "Unknown Sql failure.");
                    }
                }
            }
        }
        finally
        {
            // If there's a SQL activity, stop it before recording the duration.
            if (sqlActivity != null)
            {
                SqlTelemetryHelper.StopActivity(sqlActivity);
            }

            RecordDuration(traceActivity, eventData, in pendingCommand);
        }
    }

    private void AddPendingCommand((EventSource EventSource, int ObjectId) correlationKey, in PendingCommand pendingCommand)
    {
        lock (this.pendingCommandsLock)
        {
            // The commands which are really executing are limited by the number of connections, so when
            // the limit is reached nearly all of the entries belong to commands which will never end. They
            // cannot be told apart, so all of them are dropped: a command which is still executing
            // loses its activity and duration measurement for this execution.
            if (this.pendingCommands.Count >= MaxPendingCommands)
            {
                this.pendingCommands.Clear();
            }

            this.pendingCommands[correlationKey] = pendingCommand;
        }
    }

    private bool TryRemovePendingCommand((EventSource EventSource, int ObjectId) correlationKey, out PendingCommand pendingCommand)
    {
        lock (this.pendingCommandsLock)
        {
            if (this.pendingCommands.TryGetValue(correlationKey, out pendingCommand))
            {
                _ = this.pendingCommands.Remove(correlationKey);
                return true;
            }

            return false;
        }
    }

    private readonly struct PendingCommand(long? beginTimestamp, string? querySummary, string? dataSource, string? databaseName, Activity? activity)
    {
        public long? BeginTimestamp { get; } = beginTimestamp;

        public string? QuerySummary { get; } = querySummary;

        public string? DataSource { get; } = dataSource;

        public string? DatabaseName { get; } = databaseName;

        public Activity? Activity { get; } = activity;
    }
}
#endif
