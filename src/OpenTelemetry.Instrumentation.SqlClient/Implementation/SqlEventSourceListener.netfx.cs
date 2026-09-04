// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Collections.Concurrent;
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

    private readonly ConcurrentDictionary<int, BeginState> beginStates = new();
    private EventSource? adoNetEventSource;
    private EventSource? mdsEventSource;

    internal int PendingBeginStateCount => this.beginStates.Count;

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
            this.EnableEvents(eventSource, EventLevel.Informational, EventKeywords.All);
        }
        else if (eventSource?.Name.StartsWith(MdsEventSourceName, StringComparison.Ordinal) == true)
        {
            this.mdsEventSource = eventSource;
            this.EnableEvents(eventSource, EventLevel.Informational, EventKeywords.All);
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

    private static bool TryGetObjectId(EventWrittenEventArgs eventData, out int objectId)
    {
        if (eventData.Payload.Count > 0 && eventData.Payload[0] is int value)
        {
            objectId = value;
            return true;
        }

        objectId = default;
        return false;
    }

    private static void StopActivity(Activity activity)
    {
        var currentActivity = Activity.Current;
        activity.Stop();
        if (!ReferenceEquals(currentActivity, activity))
        {
            Activity.Current = currentActivity;
        }
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
            return;
        }

        if (eventData.Payload.Count < 4)
        {
            SqlClientInstrumentationEventSource.Log.InvalidPayload(nameof(SqlEventSourceListener), nameof(this.OnBeginExecute));
            return;
        }

        // Metrics-only fast path: if the ActivitySource has no listeners then StartActivity
        // will always return null and no trace will be produced, so skip redundant work.
        if (!SqlTelemetryHelper.ActivitySource.HasListeners())
        {
            this.RecordBeginState(eventData);
            return;
        }

        var dataSource = (string)eventData.Payload[1];
        var databaseName = (string)eventData.Payload[2];
        var startTags = SqlTelemetryHelper.GetTagListFromConnectionInfo(dataSource, databaseName, out var activityName);
        var commandText = (string)eventData.Payload[3];
        if (!string.IsNullOrEmpty(commandText))
        {
            var sqlStatementInfo = SqlProcessor.GetSanitizedSql(commandText);
            startTags.Add(SemanticConventions.AttributeDbQueryText, sqlStatementInfo.SanitizedSql);
            if (!string.IsNullOrEmpty(sqlStatementInfo.DbQuerySummary))
            {
                startTags.Add(SemanticConventions.AttributeDbQuerySummary, sqlStatementInfo.DbQuerySummary);
                activityName = sqlStatementInfo.DbQuerySummary;
            }
        }

        var activity = SqlTelemetryHelper.ActivitySource.StartActivity(
            activityName,
            ActivityKind.Client,
            default(ActivityContext),
            startTags);

        if (activity == null)
        {
            // There is no listener or it decided not to sample the current request.
            this.RecordBeginState(eventData);
            return;
        }

        if (!TryGetObjectId(eventData, out _))
        {
            // Correlation is keyed by ObjectId. A malformed Begin cannot be safely matched
            // later, so do not leave the newly-created activity current and untracked.
            StopActivity(activity);
            return;
        }

        this.RecordBeginState(eventData, activity);
    }

    private void OnEndExecute(EventWrittenEventArgs eventData)
    {
        /*
           Expected payload:
            [0] -> ObjectId
            [1] -> CompositeState bitmask (0b001 -> successFlag, 0b010 -> isSqlExceptionFlag , 0b100 -> synchronousFlag)
            [2] -> SqlExceptionNumber
         */

        var handleManager = SqlClientInstrumentation.Instance.HandleManager;

        if (handleManager.TracingHandles == 0 && handleManager.MetricHandles == 0)
        {
            if (eventData.Payload.Count > 0)
            {
                _ = this.TakeBeginState(eventData);
            }

            return;
        }

        if (eventData.Payload.Count < 3)
        {
            if (eventData.Payload.Count > 0)
            {
                // A valid ObjectId lets us clean up the matching state. If identity itself is
                // malformed, retain any other commands because guessing would risk stopping a
                // concurrent command's activity.
                var beginState = this.TakeBeginState(eventData);
                if (beginState?.Activity is { } activity)
                {
                    StopActivity(activity);
                }
            }

            SqlClientInstrumentationEventSource.Log.InvalidPayload(nameof(SqlEventSourceListener), nameof(this.OnEndExecute));
            return;
        }

        var hasTrackedState = this.TryGetBeginState(eventData, out var trackedState);

        // Ensure any activity that may exist due to ActivitySource.AddActivityListener()
        // is stopped regardless of whether we're doing metrics and/or tracing.
        // See https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/3033.
        var sqlActivity = hasTrackedState
            ? trackedState.Activity
            : null;

        // If we're only collecting metrics, then we don't want to modify the activity
        var traceActivity =
            handleManager.TracingHandles == 0 && handleManager.MetricHandles != 0 ?
            null :
            sqlActivity;

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
                StopActivity(sqlActivity);
            }

            this.RecordDuration(sqlActivity, eventData);
        }
    }

    private void RecordDuration(Activity? activity, EventWrittenEventArgs eventData)
    {
        var beginState = this.TakeBeginState(eventData);

        if (SqlClientInstrumentation.Instance.HandleManager.MetricHandles == 0)
        {
            return;
        }

        var tags = default(TagList);

        if (activity != null && activity.IsAllDataRequested)
        {
            SqlTelemetryHelper.AddSharedTags(activity, ref tags);
        }
        else
        {
            tags.Add(SemanticConventions.AttributeDbSystemName, SqlTelemetryHelper.MicrosoftSqlServerDbSystemName);

            var (hasError, errorNumber, exceptionType) = ExtractErrorFromEvent(eventData);

            if (hasError)
            {
                if (errorNumber != null && exceptionType != null)
                {
                    tags.Add(SemanticConventions.AttributeDbResponseStatusCode, errorNumber);
                    tags.Add(SemanticConventions.AttributeErrorType, exceptionType);
                }
            }
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
            return;
        }

        SqlTelemetryHelper.DbClientOperationDuration.Record(duration, tags);
    }

    private void RecordBeginState(EventWrittenEventArgs eventData, Activity? activity = null)
    {
        if (TryGetObjectId(eventData, out var objectId))
        {
            this.beginStates[objectId] = new(Stopwatch.GetTimestamp(), activity);
        }
    }

    private BeginState? TakeBeginState(EventWrittenEventArgs eventData) =>
        TryGetObjectId(eventData, out var objectId)
            && this.beginStates.TryRemove(objectId, out var state)
                ? state
                : null;

    private bool TryGetBeginState(EventWrittenEventArgs eventData, out BeginState state)
    {
        if (TryGetObjectId(eventData, out var objectId))
        {
            return this.beginStates.TryGetValue(objectId, out state);
        }

        state = default;
        return false;
    }

    private readonly struct BeginState(long startTimestamp, Activity? activity)
    {
        public long StartTimestamp { get; } = startTimestamp;

        public Activity? Activity { get; } = activity;
    }
}
#endif
