// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Exporter.Geneva.Transports;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_EtwEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<EtwDataTransport.EtwEventSource>();

    [Fact]
    public void EventSourceTests_ExporterEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<ExporterEventSource>();

    [Fact]
    public void EventSourceTests_MetricWindowsEventTracingDataTransport() =>
        EventSourceTestHelper.ValidateEventSourceIds<MetricWindowsEventTracingDataTransport>();

    [Theory]
    [InlineData(typeof(IndexOutOfRangeException))]
    [InlineData(typeof(ArgumentException))]
    public void FailedToSendMetricData_LogsBufferOverflowForFixedBufferExceptions(Type exceptionType)
    {
        var capturedEvents = new List<EventWrittenEventArgs>();

        using var listener = new BufferOverflowEventListener(capturedEvents);

        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        ExporterEventSource.Log.FailedToSendMetricData("account", "namespace", "test-metric", exception);

        var overflowEvent = Assert.Single(capturedEvents, e => e.EventId == 11);
        Assert.Equal(2, overflowEvent.Payload?.Count);
        Assert.Equal("test-metric", overflowEvent.Payload?[0]);
        Assert.Equal(GenevaMetricExporter.BufferSize, overflowEvent.Payload?[1]);
    }

    [Fact]
    public void FailedToSendMetricData_LogsBufferOverflowForMetricSerializerBoundsException()
    {
        var capturedEvents = new List<EventWrittenEventArgs>();

        using var listener = new BufferOverflowEventListener(capturedEvents);

        ExporterEventSource.Log.FailedToSendMetricData(
            "account",
            "namespace",
            "test-metric",
            new ArgumentOutOfRangeException("bufferIndex", 1, "The buffer is too small to write a value at the specified index."));

        Assert.Contains(capturedEvents, e => e.EventId == 11);
    }

    [Fact]
    public void FailedToSendMetricData_LogsGenericErrorForNonOverflowExceptions()
    {
        var capturedEvents = new List<EventWrittenEventArgs>();

        using var listener = new BufferOverflowEventListener(capturedEvents);

        ExporterEventSource.Log.FailedToSendMetricData("account", "namespace", "test-metric", new InvalidOperationException("boom"));

        Assert.Contains(capturedEvents, e => e.EventId == 3);
        Assert.DoesNotContain(capturedEvents, e => e.EventId == 11);
    }

    [Theory]
    [InlineData(typeof(IndexOutOfRangeException))]
    [InlineData(typeof(ArgumentException))]
    public void FailedToSendTraceData_LogsBufferOverflowForFixedBufferExceptions(Type exceptionType)
    {
        var capturedEvents = new List<EventWrittenEventArgs>();

        using var listener = new BufferOverflowEventListener(capturedEvents);

        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        ExporterEventSource.Log.FailedToSendTraceData(exception);

        Assert.Contains(capturedEvents, e => e.EventId == 12);
    }

    [Fact]
    public void FailedToSendTraceData_LogsBufferOverflowForEventBuilderException()
    {
        var capturedEvents = new List<EventWrittenEventArgs>();

        using var listener = new BufferOverflowEventListener(capturedEvents);

        ExporterEventSource.Log.FailedToSendTraceData(new InvalidOperationException("Event too large"));

        Assert.Contains(capturedEvents, e => e.EventId == 12);
    }

    [Fact]
    public void FailedToSendTraceData_LogsGenericErrorForNonOverflowExceptions()
    {
        var capturedEvents = new List<EventWrittenEventArgs>();

        using var listener = new BufferOverflowEventListener(capturedEvents);

        ExporterEventSource.Log.FailedToSendTraceData(new InvalidOperationException("boom"));

        Assert.Contains(capturedEvents, e => e.EventId == 1);
        Assert.DoesNotContain(capturedEvents, e => e.EventId == 12);
    }

    [Fact]
    public void FailedToSendTraceData_DoesNotTreatFieldCountLimitAsBufferOverflow()
    {
        var capturedEvents = new List<EventWrittenEventArgs>();

        using var listener = new BufferOverflowEventListener(capturedEvents);

        ExporterEventSource.Log.FailedToSendTraceData(new ArgumentOutOfRangeException("fieldCount"));

        Assert.Contains(capturedEvents, e => e.EventId == 1);
        Assert.DoesNotContain(capturedEvents, e => e.EventId == 12);
    }

    [Theory]
    [InlineData(typeof(IndexOutOfRangeException))]
    [InlineData(typeof(ArgumentException))]
    public void FailedToSendLogData_LogsBufferOverflowForFixedBufferExceptions(Type exceptionType)
    {
        var capturedEvents = new List<EventWrittenEventArgs>();

        using var listener = new BufferOverflowEventListener(capturedEvents);

        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        ExporterEventSource.Log.FailedToSendLogData(exception);

        Assert.Contains(capturedEvents, e => e.EventId == 13);
    }

    [Fact]
    public void FailedToSendLogData_LogsBufferOverflowForEventBuilderException()
    {
        var capturedEvents = new List<EventWrittenEventArgs>();

        using var listener = new BufferOverflowEventListener(capturedEvents);

        ExporterEventSource.Log.FailedToSendLogData(new InvalidOperationException("Event too large"));

        Assert.Contains(capturedEvents, e => e.EventId == 13);
    }

    [Fact]
    public void FailedToSendLogData_LogsGenericErrorForNonOverflowExceptions()
    {
        var capturedEvents = new List<EventWrittenEventArgs>();

        using var listener = new BufferOverflowEventListener(capturedEvents);

        ExporterEventSource.Log.FailedToSendLogData(new InvalidOperationException("boom"));

        Assert.Contains(capturedEvents, e => e.EventId == 2);
        Assert.DoesNotContain(capturedEvents, e => e.EventId == 13);
    }

    [Fact]
    public void FailedToSendLogData_DoesNotTreatFieldCountLimitAsBufferOverflow()
    {
        var capturedEvents = new List<EventWrittenEventArgs>();

        using var listener = new BufferOverflowEventListener(capturedEvents);

        ExporterEventSource.Log.FailedToSendLogData(new ArgumentOutOfRangeException("fieldCount"));

        Assert.Contains(capturedEvents, e => e.EventId == 2);
        Assert.DoesNotContain(capturedEvents, e => e.EventId == 13);
    }

#if NET
    [Fact]
    public void BufferOverflowHelper_LogsLogOverflowForEventHeaderErrorCode()
    {
        var capturedEvents = new List<EventWrittenEventArgs>();

        using var listener = new BufferOverflowEventListener(capturedEvents);

        Assert.True(GenevaBufferOverflowExceptionHelper.TryReportLogBufferOverflow(Microsoft.LinuxTracepoints.Provider.TracepointHandle.EventTooBigError));
        Assert.Contains(capturedEvents, e => e.EventId == 13);
    }
#endif

    [Fact]
    public void BufferOverflowHelper_DoesNotHandleNonOverflowExceptions()
    {
        Assert.False(GenevaBufferOverflowExceptionHelper.IsMsgPackBufferOverflow(new ArgumentOutOfRangeException("fieldCount")));
        Assert.True(GenevaBufferOverflowExceptionHelper.IsMetricSerializerBufferOverflow(new ArgumentOutOfRangeException("bufferIndex", 1, "The buffer is too small to write a value at the specified index.")));
        Assert.False(GenevaBufferOverflowExceptionHelper.TryReportMetricBufferOverflow("test-metric", new InvalidOperationException("boom")));
    }

    private sealed class BufferOverflowEventListener : EventListener
    {
        private readonly List<EventWrittenEventArgs> capturedEvents;

        public BufferOverflowEventListener(List<EventWrittenEventArgs> capturedEvents)
        {
            this.capturedEvents = capturedEvents;
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "OpenTelemetry-Exporter-Geneva")
            {
                this.EnableEvents(eventSource, EventLevel.Error, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData) =>
            this.capturedEvents.Add(eventData);
    }
}
