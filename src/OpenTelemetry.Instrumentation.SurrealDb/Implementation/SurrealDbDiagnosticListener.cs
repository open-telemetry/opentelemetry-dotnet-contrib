// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Events.SurrealDb;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.SurrealDb.Implementation;

internal sealed class SurrealDbDiagnosticListener : ListenerHandler
{
    public SurrealDbDiagnosticListener(string sourceName)
        : base(sourceName)
    {
    }

    public override bool SupportsNullActivity => false;

    public override void OnEventWritten(string name, object? payload)
    {
        if (SurrealDbInstrumentation.Instance.HandleManager.TracingHandles == 0
            && SurrealDbInstrumentation.Instance.HandleManager.MetricHandles == 0)
        {
            return;
        }

        switch (name)
        {
            case SurrealDbBeforeExecuteMethod.Name:
                HandleBeforeExecute(payload);
                break;
            case SurrealDbBeforeExecuteQuery.Name:
                HandleBeforeExecuteQuery(payload);
                break;
            case SurrealDbExecuteMethod.Name:
                HandleExecute(payload);
                break;
            case SurrealDbAfterExecuteMethod.Name:
                HandleAfterExecute(payload);
                break;
            case SurrealDbExecuteError.Name:
                HandleExecuteError(payload);
                break;
        }
    }

    private static void HandleBeforeExecute(object? payload)
    {
        const string name = SurrealDbBeforeExecuteMethod.Name;
        if (payload is not SurrealDbBeforeExecuteMethod @event)
        {
            SurrealDbInstrumentationEventSource.Log.NullPayload(nameof(SurrealDbDiagnosticListener), name);
            return;
        }

        var activity = SurrealDbTelemetryHelper.ActivitySource.StartActivity(
            @event.Summary,
            ActivityKind.Client,
            default(ActivityContext));
        var options = SurrealDbInstrumentation.TracingOptions;

        if (activity is null)
            return;

        if (activity.IsAllDataRequested)
        {
            activity.AddTag(SemanticConventions.AttributeServerAddress, @event.Address.Host);
            activity.AddTag(SemanticConventions.AttributeServerPort, @event.Address.Port);
            activity.SetTag(SemanticConventions.AttributeDbSystemName, SurrealDbTelemetryHelper.SurrealDbSystemName);
            activity.SetTag(SemanticConventions.AttributeDbOperationName, @event.Method);

            if (!string.IsNullOrEmpty(@event.Table))
            {
                activity.SetTag(SemanticConventions.AttributeDbCollectionName, @event.Table);
                activity.SetTag(SemanticConventions.AttributeDbQuerySummary, @event.Summary);
            }

            var protocol = @event.Address.Scheme;
            var networkProtocol = protocol switch
            {
                "http" or "https" => "http",
                "ws" or "wss" => "ws",
                _ => null,
            };

            if (!string.IsNullOrEmpty(networkProtocol))
            {
                activity.AddTag(SemanticConventions.AttributeNetworkProtocolName, networkProtocol);
            }
        }

        if (activity.IsAllDataRequested)
        {
            try
            {
                if (options.Filter?.Invoke(@event) == false)
                {
                    SurrealDbInstrumentationEventSource.Log.MethodIsFilteredOut(activity.OperationName);
                    activity.IsAllDataRequested = false;
                    activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                }
            }
            catch (Exception ex)
            {
                SurrealDbInstrumentationEventSource.Log.MethodFilterException(ex);
                activity.IsAllDataRequested = false;
                activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            }
        }
    }

    private static void HandleBeforeExecuteQuery(object? payload)
    {
        const string name = SurrealDbBeforeExecuteQuery.Name;
        if (payload is not SurrealDbBeforeExecuteQuery @event)
        {
            SurrealDbInstrumentationEventSource.Log.NullPayload(nameof(SurrealDbDiagnosticListener), name);
            return;
        }

        var activity = Activity.Current;
        var options = SurrealDbInstrumentation.TracingOptions;

        if (activity is null)
        {
            SurrealDbInstrumentationEventSource.Log.NullActivity(name);
            return;
        }

        if (activity.Source != SurrealDbTelemetryHelper.ActivitySource)
        {
            return;
        }

        if (activity.IsAllDataRequested && options.SetDbQueryParameters)
        {
            foreach (var parameter in @event.Parameters)
            {
                activity.SetTag($"db.query.parameter.{parameter.Key}", parameter.Value);
            }
        }
    }

    private static void HandleExecute(object? payload)
    {
        const string name = SurrealDbExecuteMethod.Name;
        if (payload is not SurrealDbExecuteMethod @event)
        {
            SurrealDbInstrumentationEventSource.Log.NullPayload(nameof(SurrealDbDiagnosticListener), name);
            return;
        }

        var activity = Activity.Current;
        var options = SurrealDbInstrumentation.TracingOptions;

        if (activity is null)
        {
            SurrealDbInstrumentationEventSource.Log.NullActivity(name);
            return;
        }

        if (activity.Source != SurrealDbTelemetryHelper.ActivitySource)
        {
            return;
        }

        if (activity.IsAllDataRequested)
        {
            string? dbNamespace = null;
            if (!string.IsNullOrEmpty(@event.Namespace))
            {
                dbNamespace = @event.Namespace;
                if (!string.IsNullOrEmpty(@event.Database))
                {
                    dbNamespace += '|' + @event.Database;
                }
            }

            if (!string.IsNullOrEmpty(dbNamespace))
            {
                activity.AddTag(SemanticConventions.AttributeDbNamespace, dbNamespace);
            }
        }

        if (options.EnableTraceContextPropagation && @event.Data is not null)
        {
            var tracedflags = (activity.ActivityTraceFlags & ActivityTraceFlags.Recorded) != 0 ? "01" : "00";
            @event.Data.Traceparent = $"00-{activity.TraceId.ToHexString()}-{activity.SpanId.ToHexString()}-{tracedflags}";
        }
    }

    private static void HandleAfterExecute(object? payload)
    {
        const string name = SurrealDbAfterExecuteMethod.Name;
        if (payload is not SurrealDbAfterExecuteMethod @event)
        {
            SurrealDbInstrumentationEventSource.Log.NullPayload(nameof(SurrealDbDiagnosticListener), name);
            return;
        }

        var activity = Activity.Current;
        if (activity is null)
        {
            SurrealDbInstrumentationEventSource.Log.NullActivity(name);
            return;
        }

        if (activity.Source != SurrealDbTelemetryHelper.ActivitySource)
        {
            return;
        }

        if (activity.IsAllDataRequested && @event.BatchSize is > 1)
        {
            activity.AddTag(SemanticConventions.AttributeDbOperationBatchSize, @event.BatchSize.Value);
        }

        activity.Stop();
        RecordDuration(activity);
    }

    private static void HandleExecuteError(object? payload)
    {
        const string name = SurrealDbExecuteError.Name;
        if (payload is not SurrealDbExecuteError @event)
        {
            SurrealDbInstrumentationEventSource.Log.NullPayload(nameof(SurrealDbDiagnosticListener), name);
            return;
        }

        var activity = Activity.Current;
        var options = SurrealDbInstrumentation.TracingOptions;

        if (activity is null)
        {
            SurrealDbInstrumentationEventSource.Log.NullActivity(name);
            return;
        }

        if (activity.Source != SurrealDbTelemetryHelper.ActivitySource)
        {
            return;
        }

        if (activity.IsAllDataRequested && @event.Exception is not null)
        {
            activity.AddTag(SemanticConventions.AttributeErrorType, @event.Exception.GetType().FullName);
            activity.SetStatus(ActivityStatusCode.Error, @event.Exception.Message);

            if (options.RecordException)
            {
                activity.AddException(@event.Exception);
            }
        }

        activity.Stop();
        RecordDuration(activity);
    }

    private static void RecordDuration(Activity activity)
    {
        if (SurrealDbInstrumentation.Instance.HandleManager.MetricHandles == 0)
        {
            return;
        }

        var tags = default(TagList);

        var duration = activity.Duration.TotalSeconds;
        SurrealDbTelemetryHelper.DbClientOperationDuration.Record(duration, tags);
    }
}
