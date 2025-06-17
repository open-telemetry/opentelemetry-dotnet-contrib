// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAMPClient.Data;
using OpenTelemetry.OpAMPClient.Services.Internal;
using OpenTelemetry.OpAMPClient.Settings;

namespace OpenTelemetry.OpAMPClient.Services;

internal class HeartbeatService : IBackgroundService, IDisposable
{
    public const string Name = "heartbeat-service";

    private readonly FrameDispatcher dispatcher;

    private PeriodicTimer? timer;
    private CancellationTokenSource cts;
    private HealthStatus? lastStatus;
    private ulong startTime; // Start time in unix nanoseconds
    private ulong statusTime;
    private bool isConfigured;

    public HeartbeatService(FrameDispatcher dispatcher)
    {
        this.cts = new CancellationTokenSource();
        this.dispatcher = dispatcher;
    }

    public string ServiceName => Name;

    public void Configure(OpAMPSettings settings)
    {
        if (this.timer == null)
        {
            this.timer = new PeriodicTimer(settings.HeartbeatSettings.Interval);
        }
        else
        {
            this.timer.Period = settings.HeartbeatSettings.Interval;
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
        if (!this.isConfigured)
        {
            throw new InvalidOperationException("Heartbeat service is not configured. Call Configure() before starting.");
        }

        this.startTime = GetCurrentTimeInNanoseconds();

        _ = Task.Run(() => this.HeartbeatLoop(this.cts.Token));
    }

    public void Stop()
    {
        this.timer?.Dispose();
        this.cts.Cancel(); // Cancel the heartbeat loop
    }

    public void Dispose()
    {
        this.Stop();
        this.cts.Dispose();
    }

    private static ulong GetCurrentTimeInNanoseconds()
    {
        return (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000; // Convert to nanoseconds
    }

    private async Task HeartbeatLoop(CancellationToken token)
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

                await this.dispatcher.DispatchHeartbeatAysnc(report, token)
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
