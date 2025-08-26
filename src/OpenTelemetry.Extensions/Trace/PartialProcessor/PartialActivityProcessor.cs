// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Extensions.Trace.PartialProcessor;

/// <summary>
/// The PartialActivityProcessor is an OpenTelemetry span processor that emits logs at regular intervals (referred to as "heartbeats") during the lifetime of a span and when the span ends.
/// This processor is useful for monitoring long-running spans by providing periodic updates.
/// </summary>
public class PartialActivityProcessor : BaseProcessor<Activity>
{
    private const int DefaultHeartbeatIntervalMilliseconds = 5000;
    private const int DefaultInitialHeartbeatDelayMilliseconds = 5000;
    private const int DefaultProcessIntervalMilliseconds = 5000;

    private readonly ILogger logger;
    private readonly int heartbeatIntervalMilliseconds;
    private readonly int initialHeartbeatDelayMilliseconds;
    private readonly int processIntervalMilliseconds;

    private readonly object @lock = new();

    private readonly Dictionary<ActivitySpanId, Activity> activeActivities;

    private readonly Queue<(ActivitySpanId SpanId, DateTime InitialHeartbeatTime)>
        delayedHeartbeatActivities;

    private readonly HashSet<ActivitySpanId> delayedHeartbeatActivitiesLookup;

    private readonly Queue<(ActivitySpanId SpanId, DateTime NextHeartbeatTime)>
        readyHeartbeatActivities;

    private readonly Thread processorThread;
    private readonly ManualResetEvent shutdownTrigger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartialActivityProcessor"/> class.
    /// </summary>
    /// <param name="logger">Logger to be used for logging.</param>
    /// <param name="heartbeatIntervalMilliseconds">Heartbeat interval.</param>
    /// <param name="initialHeartbeatDelayMilliseconds">Initial heartbeat delay.</param>
    /// <param name="processIntervalMilliseconds">Interval when processor thread is called.</param>
    public PartialActivityProcessor(
        ILogger logger,
        int heartbeatIntervalMilliseconds = DefaultHeartbeatIntervalMilliseconds,
        int initialHeartbeatDelayMilliseconds = DefaultInitialHeartbeatDelayMilliseconds,
        int processIntervalMilliseconds = DefaultProcessIntervalMilliseconds)
    {
        ValidateParameters(
            logger,
            heartbeatIntervalMilliseconds,
            initialHeartbeatDelayMilliseconds,
            processIntervalMilliseconds);

        this.logger = logger;
        this.heartbeatIntervalMilliseconds = heartbeatIntervalMilliseconds;
        this.initialHeartbeatDelayMilliseconds = initialHeartbeatDelayMilliseconds;
        this.processIntervalMilliseconds = processIntervalMilliseconds;

        this.delayedHeartbeatActivities = new Queue<(ActivitySpanId, DateTime)>();
        this.delayedHeartbeatActivitiesLookup = new HashSet<ActivitySpanId>();
        this.readyHeartbeatActivities = new Queue<(ActivitySpanId, DateTime)>();
        this.activeActivities = new Dictionary<ActivitySpanId, Activity>();

        this.shutdownTrigger = new ManualResetEvent(false);

        this.processorThread = new Thread(this.ProcessQueues)
        {
            IsBackground = true, Name = $"OpenTelemetry-{nameof(PartialActivityProcessor)}",
        };
        this.processorThread.Start();
    }

    /// <inheritdoc />
    public override void OnStart(Activity data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data), "Activity data cannot be null.");
        }

        lock (this.@lock)
        {
            this.activeActivities[data.SpanId] = data;
            this.delayedHeartbeatActivitiesLookup.Add(data.SpanId);
            this.delayedHeartbeatActivities.Enqueue((data.SpanId,
                DateTime.UtcNow.AddMilliseconds(this.initialHeartbeatDelayMilliseconds)));
        }
    }

    /// <inheritdoc />
    public override void OnEnd(Activity data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data), "Activity data cannot be null.");
        }

        bool isDelayedHeartbeatPending;
        lock (this.@lock)
        {
            this.activeActivities.Remove(data.SpanId);

            isDelayedHeartbeatPending = this.delayedHeartbeatActivitiesLookup.Remove(data.SpanId);
        }

        if (isDelayedHeartbeatPending)
        {
            return;
        }

        using (this.logger.BeginScope(GetEndedLogRecordAttributes()))
        {
#pragma warning disable CA1848, CA2254 // Use LoggerMessage delegates for improved performance and template should not vary between calls
            this.logger.LogInformation(
                SpecHelper.Json(new TracesData(data, TracesData.Signal.Ended)));
#pragma warning restore CA1848, CA2254
        }
    }

    // added for tests convenience

    /// <summary>
    /// Gets the active activities.
    /// </summary>
    /// <returns>Snapshot of <see cref="activeActivities"/>.</returns>
    internal IReadOnlyDictionary<ActivitySpanId, Activity> ActiveActivities()
    {
        lock (this.@lock)
        {
            return new Dictionary<ActivitySpanId, Activity>(this.activeActivities);
        }
    }

    /// <summary>
    /// Gets the activities that are delayed for heartbeat logging.
    /// </summary>
    /// <returns>Snapshot of <see cref="delayedHeartbeatActivities"/>.</returns>
    internal IReadOnlyCollection<(ActivitySpanId SpanId, DateTime InitialHeartbeatTime)>
        DelayedHeartbeatActivities()
    {
        lock (this.@lock)
        {
            return new List<(ActivitySpanId SpanId, DateTime InitialHeartbeatTime)>(
                this.delayedHeartbeatActivities);
        }
    }

    /// <summary>
    /// Gets the lookup for delayed heartbeat activities.
    /// </summary>
    /// <returns>Snapshot of <see cref="delayedHeartbeatActivitiesLookup"/>.</returns>
    internal IReadOnlyCollection<ActivitySpanId> DelayedHeartbeatActivitiesLookup()
    {
        lock (this.@lock)
        {
            return new List<ActivitySpanId>(this.delayedHeartbeatActivitiesLookup);
        }
    }

    /// <summary>
    /// Gets the activities that are ready for heartbeat logging.
    /// </summary>
    /// <returns>Snapshot of ready <see cref="readyHeartbeatActivities"/>.</returns>
    internal IReadOnlyCollection<(ActivitySpanId SpanId, DateTime NextHeartbeatTime)>
        ReadyHeartbeatActivities()
    {
        lock (this.@lock)
        {
            return new List<(ActivitySpanId SpanId, DateTime NextHeartbeatTime)>(
                this.readyHeartbeatActivities);
        }
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        try
        {
            this.shutdownTrigger.Set();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        return this.processorThread.Join(timeoutMilliseconds);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        this.shutdownTrigger.Dispose();
    }

    private static Dictionary<string, object> GetHeartbeatLogRecordAttributes() => new()
    {
        ["span.state"] = "heartbeat", ["log.body.type"] = "json/v1",
    };

    private static Dictionary<string, object> GetEndedLogRecordAttributes() => new()
    {
        ["span.state"] = "ended", ["log.body.type"] = "json/v1",
    };

    private static void ValidateParameters(
        ILogger logger,
        int heartbeatIntervalMilliseconds,
        int initialHeartbeatDelayMilliseconds,
        int processIntervalMilliseconds)
    {
#if NET
        ArgumentNullException.ThrowIfNull(logger);
#else
        if (logger == null)
        {
            throw new ArgumentOutOfRangeException(nameof(logger));
        }
#endif

        if (heartbeatIntervalMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heartbeatIntervalMilliseconds),
                "Heartbeat interval must be greater than zero.");
        }

        if (initialHeartbeatDelayMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialHeartbeatDelayMilliseconds),
                "Initial heartbeat delay must be zero or greater.");
        }

        if (processIntervalMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(processIntervalMilliseconds),
                "Process interval must be zero or greater.");
        }
    }

    private void ProcessQueues()
    {
        var triggers = new WaitHandle[] { this.shutdownTrigger };

        while (true)
        {
            try
            {
                WaitHandle.WaitAny(triggers, this.processIntervalMilliseconds);

                this.ProcessDelayedHeartbeatActivities();
                this.ProcessReadyHeartbeatActivities();
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    private void ProcessDelayedHeartbeatActivities()
    {
        List<Activity> activitiesToBeLogged = [];
        lock (this.@lock)
        {
            while (true)
            {
                if (this.delayedHeartbeatActivities.Count == 0)
                {
                    break;
                }

                var peekedItem = this.delayedHeartbeatActivities.Peek();
                if (peekedItem.InitialHeartbeatTime > DateTime.UtcNow)
                {
                    break;
                }

                this.delayedHeartbeatActivitiesLookup.Remove(peekedItem.SpanId);
                this.delayedHeartbeatActivities.Dequeue();

                if (this.activeActivities.TryGetValue(peekedItem.SpanId, out var activity))
                {
                    activitiesToBeLogged.Add(activity);

                    this.readyHeartbeatActivities.Enqueue((peekedItem.SpanId,
                        DateTime.UtcNow.AddMilliseconds(this.heartbeatIntervalMilliseconds)));
                }
            }
        }

        this.LogActivities(activitiesToBeLogged);
    }

    private void ProcessReadyHeartbeatActivities()
    {
        List<Activity> activitiesToBeLogged = [];
        lock (this.@lock)
        {
            while (true)
            {
                if (this.readyHeartbeatActivities.Count == 0)
                {
                    break;
                }

                var peekedItem = this.readyHeartbeatActivities.Peek();
                if (peekedItem.NextHeartbeatTime > DateTime.UtcNow)
                {
                    break;
                }

                this.readyHeartbeatActivities.Dequeue();

                if (this.activeActivities.TryGetValue(peekedItem.SpanId, out var activity))
                {
                    activitiesToBeLogged.Add(activity);

                    this.readyHeartbeatActivities.Enqueue((peekedItem.SpanId,
                        DateTime.UtcNow.AddMilliseconds(this.heartbeatIntervalMilliseconds)));
                }
            }
        }

        this.LogActivities(activitiesToBeLogged);
    }

    private void LogActivities(List<Activity> activitiesToBeLogged)
    {
        foreach (var activity in activitiesToBeLogged)
        {
            // begin scope needs to happen inside foreach so resource is properly set

            using (this.logger.BeginScope(GetHeartbeatLogRecordAttributes()))
            {
#pragma warning disable CA1848, CA2254 // Use LoggerMessage delegates for improved performance and template should not vary between calls
                this.logger.LogInformation(
                    SpecHelper.Json(new TracesData(activity, TracesData.Signal.Heartbeat)));
#pragma warning restore CA1848, CA2254
            }
        }
    }
}
