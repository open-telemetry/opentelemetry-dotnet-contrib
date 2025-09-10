// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Internal.Listeners;
using OpenTelemetry.OpAmp.Client.Internal.Listeners.Messages;
using OpenTelemetry.OpAmp.Client.Internal.Settings;

namespace OpenTelemetry.OpAmp.Client.Internal.Services.Heartbeat;

internal sealed class HeartbeatService : IBackgroundService, IOpAmpListener<ConnectionSettingsMessage>, IDisposable
{
    public const string Name = "heartbeat-service";

    private readonly FrameDispatcher dispatcher;
    private readonly FrameProcessor processor;
    private readonly CancellationTokenSource cts;
    private readonly Lock timerUpdateLock = new();
    private Timer? timer;
    private TimeSpan tickInterval;
    private ulong startTime;

    public HeartbeatService(FrameDispatcher dispatcher, FrameProcessor processor)
    {
        this.dispatcher = dispatcher;
        this.processor = processor;
        this.cts = new CancellationTokenSource();

        this.processor.Subscribe(this);
    }

    public string ServiceName => Name;

    public void Configure(OpAmpClientSettings settings)
    {
        this.tickInterval = settings.Heartbeat.Interval;
    }

    public void Start()
    {
        this.startTime = GetCurrentTimeInNanoseconds();
        this.CreateOrUpdateTimer(this.tickInterval);
    }

    public void Stop()
    {
        this.cts.Cancel();
        this.CreateOrUpdateTimer(Timeout.InfiniteTimeSpan);
    }

    public void HandleMessage(ConnectionSettingsMessage message)
    {
        var newInterval = message.ConnectionSettings.Opamp?.HeartbeatIntervalSeconds ?? 0;
        if (newInterval > 0)
        {
            // TODO: change to proper logging
            Console.WriteLine($"[debug] New heartbeat interval received: {newInterval}s");

            this.CreateOrUpdateTimer(TimeSpan.FromSeconds(newInterval));
        }
    }

    public void Dispose()
    {
        this.processor.Unsubscribe(this);

        this.cts.Dispose();
        this.timer?.Dispose();
    }

    private static ulong GetCurrentTimeInNanoseconds()
    {
        return (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000; // Convert to nanoseconds
    }

    private void CreateOrUpdateTimer(TimeSpan interval)
    {
        lock (this.timerUpdateLock)
        {
            this.timer ??= new Timer(this.HeartbeatTick);
            this.timer.Change(interval, interval);
        }
    }

    private async void HeartbeatTick(object? state)
    {
        try
        {
            var report = this.CreateHealthReport();

            await this.dispatcher.DispatchHeartbeatAsync(report, this.cts.Token)
                .ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            // Ignore task cancellation
        }
        catch (Exception ex)
        {
            // TODO: change to proper logging
            Console.WriteLine($"[error] Heartbeat error: {ex.Message}");
        }
    }

    private HealthReport CreateHealthReport()
    {
        return new HealthReport
        {
            StartTime = this.startTime,
            StatusTime = GetCurrentTimeInNanoseconds(),
            IsHealthy = true,
            Status = "OK",
        };
    }
}
