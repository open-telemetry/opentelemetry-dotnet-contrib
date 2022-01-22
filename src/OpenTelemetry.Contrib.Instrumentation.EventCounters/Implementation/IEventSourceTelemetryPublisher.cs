namespace OpenTelemetry.Contrib.Instrumentation.EventCounters.Implementation
{
    internal interface IEventSourceTelemetryPublisher
    {
        void Publish(MetricTelemetry metricTelemetry);
    }
}
