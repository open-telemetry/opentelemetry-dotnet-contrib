// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Geneva;

[EventSource(Name = "OpenTelemetry-Exporter-Geneva")]
internal sealed class ExporterEventSource : EventSource
{
    public static readonly ExporterEventSource Log = new ExporterEventSource();
    private const int EVENT_ID_TRACE = 1; // Failed to send Trace
    private const int EVENT_ID_LOG = 2; // Failed to send Log
    private const int EVENT_ID_METRIC = 3; // Failed to send Metric
    private const int EVENT_ID_ERROR = 4; // Other common exporter exceptions
    private const int EVENT_ID_OTLP_PROTOBUF_METRIC = 5; // Failed to serialize metric
    private const int EVENT_ID_COMPLETED_EXPORT = 6; // Completed export

    [NonEvent]
    public void FailedToSendTraceData(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            // https://docs.microsoft.com/en-us/windows/win32/etw/about-event-tracing
            // ETW has a size limit: The total event size is greater than 64K. This includes the ETW header plus the data or payload.
            // TODO: Do not hit ETW size limit even for external library exception stack. But what is the ETW header size?
            // Source code: https://referencesource.microsoft.com/#mscorlib/system/diagnostics/eventing/eventsource.cs,867
            // Why is size calculated like below in WriteEvent source code?
            // descrs[0].Size = ((arg1.Length + 1) * 2);
            // I'm assuming it calculates the size of string, then it should be:
            // (count of chars) * sizeof(char) + sizeof(Length:int) = (str.Length * 2 + 4).
            this.FailedToSendTraceData(ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void FailedToSendLogData(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            // TODO: Do not hit ETW size limit even for external library exception stack.
            this.FailedToSendLogData(ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void FailedToSendMetricData(string monitoringAccount, string metricNamespace, string metricName, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            // TODO: Do not hit ETW size limit even for external library exception stack.
            this.FailedToSendMetricData(monitoringAccount, metricNamespace, metricName, ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void ExporterException(string message, Exception ex)
    {
        if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            // TODO: Do not hit ETW size limit even for external library exception stack.
            this.ExporterException(message, ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void FailedToSerializeMetric(string metricName, Exception ex)
    {
        if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            // TODO: Do not hit ETW size limit even for external library exception stack.
            this.FailedToSerializeMetric(metricName, ex.ToInvariantString());
        }
    }

    [Event(EVENT_ID_TRACE, Message = "Exporter failed to send trace data. Exception: {0}", Level = EventLevel.Error)]
    public void FailedToSendTraceData(string error)
    {
        this.WriteEvent(EVENT_ID_TRACE, error);
    }

    [Event(EVENT_ID_LOG, Message = "Exporter failed to send log data. Exception: {0}", Level = EventLevel.Error)]
    public void FailedToSendLogData(string error)
    {
        this.WriteEvent(EVENT_ID_LOG, error);
    }

    [Event(EVENT_ID_METRIC, Message = "Exporter failed to send metric data. Data will not be sent. MonitoringAccount = {0} MetricNamespace = {1}, MetricName = {2}, Message: {3}", Level = EventLevel.Error)]
    public void FailedToSendMetricData(string monitoringAccount, string metricNamespace, string metricName, string error)
    {
        this.WriteEvent(EVENT_ID_METRIC, monitoringAccount, metricNamespace, metricName, error);
    }

    [Event(EVENT_ID_ERROR, Message = "Exporter failed. Message: {0}, Exception: {1}", Level = EventLevel.Error)]
    public void ExporterException(string message, string error)
    {
        this.WriteEvent(EVENT_ID_ERROR, message, error);
    }

    [Event(EVENT_ID_OTLP_PROTOBUF_METRIC, Message = "Failed to serialize '{0}' metric, Exception: {1}", Level = EventLevel.Error)]
    public void FailedToSerializeMetric(string metricName, string error)
    {
        this.WriteEvent(EVENT_ID_OTLP_PROTOBUF_METRIC, metricName, error);
    }

    [Event(EVENT_ID_COMPLETED_EXPORT, Message = "'{0}' completed data export.", Level = EventLevel.Informational)]
    public void ExportCompleted(string exporterName)
    {
        this.WriteEvent(EVENT_ID_COMPLETED_EXPORT, exporterName);
    }
}
