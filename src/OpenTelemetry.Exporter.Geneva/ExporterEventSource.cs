// <copyright file="ExporterEventSource.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Threading;

namespace OpenTelemetry.Exporter.Geneva;

[EventSource(Name = "OpenTelemetry-Exporter-Geneva")]
internal class ExporterEventSource : EventSource
{
    public static readonly ExporterEventSource Log = new ExporterEventSource();
    private const int EVENT_ID_TRACE = 1; // Failed to send Trace
    private const int EVENT_ID_LOG = 2; // Failed to send Log
    private const int EVENT_ID_METRIC = 3; // Failed to send Metric
    private const int EVENT_ID_ERROR = 4; // Other common exporter exceptions

    [Event(EVENT_ID_TRACE, Message = "Exporter failed to send trace data. Exception: {0}", Level = EventLevel.Error)]
    public void FailedToSendTraceData(Exception ex)
    {
        if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            // https://docs.microsoft.com/en-us/windows/win32/etw/about-event-tracing
            // ETW has a size limit: The total event size is greater than 64K. This includes the ETW header plus the data or payload.
            // TODO: Do not hit ETW size limit even for external library exception stack. But what is the ETW header size?
            // Source code: https://referencesource.microsoft.com/#mscorlib/system/diagnostics/eventing/eventsource.cs,867
            // Why is size calculated like below in WriteEvent source code?
            // descrs[0].Size = ((arg1.Length + 1) * 2);
            // I'm assuming it calculates the size of string, then it should be:
            // (count of chars) * sizeof(char) + sizeof(Length:int) = (str.Length * 2 + 4).
            this.WriteEvent(EVENT_ID_TRACE, ToInvariantString(ex));
        }
    }

    [Event(EVENT_ID_LOG, Message = "Exporter failed to send log data. Exception: {0}", Level = EventLevel.Error)]
    public void FailedToSendLogData(Exception ex)
    {
        if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            // TODO: Do not hit ETW size limit even for external library exception stack.
            this.WriteEvent(EVENT_ID_LOG, ToInvariantString(ex));
        }
    }

    [Event(EVENT_ID_METRIC, Message = "Exporter failed to send metric data. Data will not be sent. MonitoringAccount = {0} MetricNamespace = {1}, MetricName = {2}, Message: {3}", Level = EventLevel.Error)]
    public void FailedToSendMetricData(string monitoringAccount, string metricNamespace, string metricName, Exception ex)
    {
        if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            // TODO: Do not hit ETW size limit even for external library exception stack.
            this.WriteEvent(EVENT_ID_METRIC, monitoringAccount, metricNamespace, metricName, ToInvariantString(ex));
        }
    }

    [Event(EVENT_ID_ERROR, Message = "Exporter failed.", Level = EventLevel.Error)]
    public void ExporterException(string message, Exception ex)
    {
        if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            // TODO: Do not hit ETW size limit even for external library exception stack.
            this.WriteEvent(EVENT_ID_ERROR, message, ToInvariantString(ex));
        }
    }

    /// <summary>
    /// Returns a culture-independent string representation of the given <paramref name="exception"/> object,
    /// appropriate for diagnostics tracing.
    /// </summary>
    private static string ToInvariantString(Exception exception)
    {
        var originalUICulture = Thread.CurrentThread.CurrentUICulture;

        try
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            return exception.ToString();
        }
        finally
        {
            Thread.CurrentThread.CurrentUICulture = originalUICulture;
        }
    }
}
