// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Extensions.Trace.PartialActivityProcessor;
using Xunit;

namespace OpenTelemetry.Extensions.Tests.Trace.PartialActivityProcessor;

// TODO: add checks for log count
public class ProcessorTests
{
    private const int HeartbeatIntervalMilliseconds = 1000;
    private const int InitialHeartbeatDelayMilliseconds = 1000;
    private const int ProcessIntervalMilliseconds = 0;

    private readonly Processor processor;

    public ProcessorTests()
    {
        ILogger<Processor> logger = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        }).CreateLogger<Processor>();
        this.processor = new Processor(
            logger,
            HeartbeatIntervalMilliseconds,
            InitialHeartbeatDelayMilliseconds,
            ProcessIntervalMilliseconds);
    }

    [Fact]
    public void Constructor_ShouldThrowExceptionForInvalidParameters()
    {
        ILogger<Processor> logger = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        }).CreateLogger<Processor>();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Processor(
                logger,
                heartbeatIntervalMilliseconds: -1)); // Invalid heartbeat interval

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Processor(
                logger,
                initialHeartbeatDelayMilliseconds: -1)); // Invalid initial delay

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Processor(
                logger,
                processIntervalMilliseconds: -1)); // Invalid process interval

#if NET
        Assert.Throws<ArgumentNullException>(() =>
            new Processor(logger: null!)); // Null log exporter
#else
Assert.Throws<ArgumentOutOfRangeException>(() =>
    new PartialActivityProcessor(logger: null!)); // Null log exporter
#endif
    }

    [Fact]
    public void Constructor_ShouldInitializeFields() => Assert.NotNull(this.processor);

    [Fact]
    public void OnStart_ShouldAddActivityToActiveAndDelayed()
    {
        var activity = new Activity("TestActivity");

        this.processor.OnStart(activity);

        Assert.Contains(activity.SpanId, this.processor.ActiveActivities());
        Assert.Contains(activity.SpanId, this.processor.DelayedHeartbeatActivitiesLookup());
        Assert.Contains(
            this.processor.DelayedHeartbeatActivities(),
            valueTuple => valueTuple.SpanId == activity.SpanId);
    }

    [Fact]
    public void OnEnd_ShouldNotExportLogIfInitialHeartbeatNotSent()
    {
        var activity = new Activity("TestActivity");

        this.processor.OnStart(activity);

        this.processor.OnEnd(activity);
        Assert.DoesNotContain(activity.SpanId, this.processor.ActiveActivities());
        Assert.DoesNotContain(activity.SpanId, this.processor.DelayedHeartbeatActivitiesLookup());
        Assert.Contains(
            this.processor.DelayedHeartbeatActivities(),
            valueTuple => valueTuple.SpanId == activity.SpanId);
    }

    [Fact]
    public void OnEnd_ShouldExportLogIfInitialHeartbeatSent()
    {
        var activity = new Activity("TestActivity");
        var spanId = activity.SpanId;

        this.processor.OnStart(activity);

        var delayedHeartbeatActivityLookupRemoved = SpinWait.SpinUntil(
            () => this.processor.DelayedHeartbeatActivitiesLookup().All(
                activitySpanId => activitySpanId != spanId),
            TimeSpan.FromSeconds(10));
        Assert.True(
            delayedHeartbeatActivityLookupRemoved,
            "Lookup activity with delayed heartbeat not removed in time.");

        var delayedHeartbeatActivityRemoved = SpinWait.SpinUntil(
            () => this.processor.DelayedHeartbeatActivities().All(valueTuple =>
                valueTuple.SpanId != spanId),
            TimeSpan.FromSeconds(10));
        Assert.True(
            delayedHeartbeatActivityRemoved,
            "Activity with delayed heartbeat not removed in time.");

        var readyHeartbeatActivityAdded = SpinWait.SpinUntil(
            () => this.processor.ReadyHeartbeatActivities().Any(valueTuple =>
                valueTuple.SpanId == spanId),
            TimeSpan.FromSeconds(10));
        Assert.True(
            readyHeartbeatActivityAdded,
            "Activity with ready heartbeat not added in time.");

        // HACK: because of test flakiness in ci, this was added so that activity is no longer added to ready heartbeat activities because it is not active anymore
        this.processor.OnEnd(activity);

        var readyHeartbeatActivityRemoved = SpinWait.SpinUntil(
            () => this.processor.ReadyHeartbeatActivities().All(valueTuple =>
                valueTuple.SpanId != spanId),
            TimeSpan.FromSeconds(10));
        Assert.True(
            readyHeartbeatActivityRemoved,
            "Activity with ready heartbeat not removed in time.");

        Assert.DoesNotContain(activity.SpanId, this.processor.ActiveActivities());
    }

    [Fact]
    public void
        DelayedHeartbeatActivities_ShouldMoveActivitiesToReadyHeartbeatActivitiesAfterProcessing()
    {
        var activity = new Activity("TestActivity");
        var spanId = activity.SpanId;

        this.processor.OnStart(activity);

        var delayedHeartbeatActivityLookupRemoved = SpinWait.SpinUntil(
            () => this.processor.DelayedHeartbeatActivitiesLookup().All(
                activitySpanId => activitySpanId != spanId),
            TimeSpan.FromSeconds(10));
        Assert.True(
            delayedHeartbeatActivityLookupRemoved,
            "Lookup activity with delayed heartbeat not removed in time.");

        var delayedHeartbeatActivityRemoved = SpinWait.SpinUntil(
            () => this.processor.DelayedHeartbeatActivities().All(valueTuple =>
                valueTuple.SpanId != spanId),
            TimeSpan.FromSeconds(10));
        Assert.True(
            delayedHeartbeatActivityRemoved,
            "Activity with delayed heartbeat was not removed in time.");

        var readyHeartbeatActivityRemoved = SpinWait.SpinUntil(
            () => this.processor.ReadyHeartbeatActivities().Any(valueTuple =>
                valueTuple.SpanId == spanId),
            TimeSpan.FromSeconds(10));
        Assert.True(
            readyHeartbeatActivityRemoved,
            "Activity ready for heartbeat was not added in time.");
    }

    [Fact]
    public void ReadyHeartbeatActivities_ShouldProcessHeartbeatLogsAfterProcessing()
    {
        var activity = new Activity("TestActivity");
        var spanId = activity.SpanId;

        this.processor.OnStart(activity);

        var heartbeatReadyActivityAdded = SpinWait.SpinUntil(
            () => this.processor.ReadyHeartbeatActivities().Any(valueTuple =>
                valueTuple.SpanId == spanId),
            TimeSpan.FromSeconds(10));
        Assert.True(
            heartbeatReadyActivityAdded,
            "Activity ready for heartbeat was not added in time.");

        // HACK: because of test flakiness in ci, this was added so that activity is no longer added to ready heartbeat activities because it is not active anymore
        this.processor.OnEnd(activity);

        var heartbeatReadyActivityRemoved = SpinWait.SpinUntil(
            () => this.processor.ReadyHeartbeatActivities().All(valueTuple =>
                valueTuple.SpanId != spanId),
            TimeSpan.FromSeconds(15));
        Assert.True(
            heartbeatReadyActivityRemoved,
            "Activity ready for heartbeat was not removed in time.");
    }
}
