// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Data;
using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Listeners.Messages;
using OpenTelemetry.OpAmp.Client.Services.Internal;
using OpenTelemetry.OpAmp.Client.Settings;

namespace OpenTelemetry.OpAmp.Client.Services;

internal class HeartbeatService : IBackgroundService, IOpAmpListener<ConnectionSettingsMessage>, IDisposable
{
    public const string Name = "heartbeat-service";

    private readonly FrameDispatcher dispatcher;
    private readonly FrameProcessor processor;
    private PeriodicTimer? timer;
    private CancellationTokenSource cts;
    private HealthStatus? lastStatus;
    private Task? heartbeatTask;
    private TimeSpan? requestedInterval; // Server requested interval
    private ulong startTime; // Start time in unix nanoseconds
    private ulong statusTime;
    private bool isConfigured;
    private bool isDisposed;

    public HeartbeatService(FrameDispatcher dispatcher, FrameProcessor processor)
    {
        this.cts = new CancellationTokenSource();
        this.dispatcher = dispatcher;
        this.processor = processor;

        this.processor.Subscribe(this);
    }

    public string ServiceName => Name;

    public void Configure(OpAmpSettings settings)
    {
        var interval = this.requestedInterval ?? settings.HeartbeatSettings.Interval;
        this.requestedInterval = null; // Reset after use

        if (this.timer == null)
        {
            this.timer = new PeriodicTimer(interval);
        }
        else
        {
            this.timer.Period = interval;
        }

        if (!this.isConfigured && !settings.HeartbeatSettings.ShouldWaitForFirstStatus)
        {
            this.UpdateStatus(new HealthStatus
            {
                IsHealthy = true,
                Status = settings.HeartbeatSettings.InitialStatus,
            });
        }

        this.isConfigured = true;
    }

    public void UpdateStatus(HealthStatus status)
    {
        this.lastStatus = status;
        this.statusTime = GetCurrentTimeInNanoseconds();
    }

    public void Start()
    {
        if (this.isDisposed)
        {
            ObjectDisposedException.ThrowIf(this.isDisposed, this);
        }

        if (!this.isConfigured)
        {
            throw new InvalidOperationException("Heartbeat service is not configured. Call Configure() before starting.");
        }

        if (this.heartbeatTask != null)
        {
            throw new InvalidOperationException("Heartbeat service already started.");
        }

        this.startTime = GetCurrentTimeInNanoseconds();
        this.heartbeatTask = Task.Run(() => this.HeartbeatLoopAsync(this.cts.Token));
    }

    public void Stop()
    {
        this.processor.Unsubscribe(this);

        this.timer?.Dispose();
        this.cts.Cancel(); // Cancel the heartbeat loop
    }

    public void HandleMessage(ConnectionSettingsMessage message)
    {
        var newInterval = message.ConnectionSettings.Opamp?.HeartbeatIntervalSeconds ?? 0;
        if (newInterval > 0)
        {
            // TODO: Debug log the new heartbeat interval
            Console.WriteLine($"[Debug] New heartbeat interval received: {newInterval}s");

            // TODO: may need to sync further to eliminate the race condition completely
            if (this.timer == null)
            {
                this.requestedInterval = TimeSpan.FromSeconds(newInterval);
            }
            else
            {
                this.timer.Period = TimeSpan.FromSeconds(newInterval);
            }
        }
    }

    public void Dispose()
    {
        if (this.isDisposed)
        {
            return; // Already disposed
        }

        this.Stop();
        this.cts.Dispose();
        this.isDisposed = true;
    }

    private static ulong GetCurrentTimeInNanoseconds()
    {
        return (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000; // Convert to nanoseconds
    }

    private async Task HeartbeatLoopAsync(CancellationToken token)
    {
        while (await this.timer!.WaitForNextTickAsync(token)
            .ConfigureAwait(false))
        {
            // Status is not yet received
            if (this.lastStatus == null)
            {
                continue;
            }

            try
            {
                var report = this.CreateHealthReport();

                await this.dispatcher.DispatchHeartbeatAsync(report, token)
                    .ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // Handle cancellation gracefully
                break;
            }
            catch (Exception ex)
            {
                // TODO: Log the exception (logging not implemented here)
                Console.WriteLine($"Heartbeat error: {ex.Message}");
            }
        }
    }

    private HealthReport CreateHealthReport()
    {
        if (this.lastStatus == null)
        {
            throw new InvalidOperationException("Status is not set.");
        }

        return new HealthReport
        {
            StartTime = this.startTime,
            StatusTime = this.statusTime,
            DetailedStatus = this.lastStatus,
        };
    }
}
