using System;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Threading;

namespace OpenTelemetry.Exporter.Geneva
{
    [EventSource(Name = "OpenTelemetry-Exporter-Geneva")]
    internal class ExporterEventSource : EventSource
    {
        public static readonly ExporterEventSource Log = new ExporterEventSource();
        private const int EVENT_ID_TRACE = 1;
        private const int EVENT_ID_METRICS = 2;
        private const int EVENT_ID_ERROR = 3;

        [NonEvent]
        public void ExporterException(Exception ex)
        {
            if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.FailedToSendSpanData(ToInvariantString(ex));
            }
        }

        [Event(EVENT_ID_TRACE, Message = "Exporter failed to send SpanData. Data will not be sent. Exception: {0}", Level = EventLevel.Error)]
        public void FailedToSendSpanData(string ex)
        {
            // https://docs.microsoft.com/en-us/windows/win32/etw/about-event-tracing
            // ETW has a size limit: The total event size is greater than 64K. This includes the ETW header plus the data or payload.
            // TODO: Do not hit ETW size limit even for external library exception stack. But what is the ETW header size?
            // Source code: https://referencesource.microsoft.com/#mscorlib/system/diagnostics/eventing/eventsource.cs,867
            // Why is size calculated like below in WriteEvent source code?
            // descrs[0].Size = ((arg1.Length + 1) * 2);
            // I'm assuming it calculates the size of string, then it should be:
            // (count of chars) * sizeof(char) + sizeof(Length:int) = (str.Length * 2 + 4).
            this.WriteEvent(EVENT_ID_TRACE, ex);
        }

        [Event(EVENT_ID_METRICS, Message = "Exporter failed to send MetricData. Data will not be sent. MetricNamespace = {0}, MetricName = {1}, Message: {2}", Level = EventLevel.Error)]
        public void FailedToSendMetricData(string metricNamespace, string metricName, string message)
        {
            this.WriteEvent(EVENT_ID_METRICS, metricNamespace, metricName, message);
        }

        [Event(EVENT_ID_ERROR, Message = "Exporter failed.", Level = EventLevel.Error)]
        public void ExporterError(string message)
        {
            if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                // TODO: We should ensure that GenevaTraceExporter doesn't emit any message that could hit ETW size limit.
                this.WriteEvent(EVENT_ID_ERROR, message);
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
}
