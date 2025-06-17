// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAMPClient.Data;
using OpenTelemetry.OpAMPClient.Listeners;
using OpenTelemetry.OpAMPClient.Listeners.Messages;
using OpenTelemetry.OpAMPClient.Services.Internal;
using OpenTelemetry.OpAMPClient.Settings;

namespace OpenTelemetry.OpAMPClient.Services;

internal class HeartbeatService : IBackgroundService, IOpAMPListener<ConnectionSettingsMessage>, IDisposable
{
    public const string Name = "heartbeat-service";

    private readonly FrameDispatcher dispatcher;
    private readonly FrameProcessor processor;
    private Thread hearthbeatThread;
    private CancellationTokenSource cts;
    private TimeSpan heartbeatInterval;
    private HealthStatus? lastStatus;
    private bool isRunning;
    private ulong startTime; // Start time in unix nanoseconds
    private ulong statusTime;

    public HeartbeatService(FrameDispatcher dispatcher, FrameProcessor processor)
    {
        this.cts = new CancellationTokenSource();
        this.dispatcher = dispatcher;
        this.processor = processor;
        this.hearthbeatThread = new Thread(this.HeartbeatLoop)
        {
            IsBackground = true,
            Name = "OpAMP Heartbeat Thread",
        };

        this.processor.Subscribe(this);
    }

    public string ServiceName => Name;

    public void Configure(OpAMPSettings settings)
    {
        this.heartbeatInterval = settings.HeartbeatSettings.Interval;

        if (!settings.HeartbeatSettings.ShouldWaitForFirstStatus)
        {
            this.UpdateStatus(new HealthStatus
            {
                IsHealthy = true,
                Status = settings.HeartbeatSettings.InitialStatus,
            });
        }
    }

    public void UpdateStatus(HealthStatus status)
    {
        this.lastStatus = status;
        this.statusTime = GetCurrentTimeInNanoseconds();
    }

    public void Start()
    {
        this.isRunning = true;
        this.hearthbeatThread.Start();
        this.startTime = GetCurrentTimeInNanoseconds();
    }

    public void Stop()
    {
        this.processor.Unsubscribe(this);

        this.isRunning = false;
        this.cts.Cancel(); // Cancel the heartbeat loop
    }

    public void HandleMessage(ConnectionSettingsMessage message)
    {
        var newInterval = message.ConnectionSettings.Opamp.HeartbeatIntervalSeconds;

        if (newInterval > 0)
        {
            // TODO: Debug log the new heartbeat interval
            Console.WriteLine($"[Debug] New heartbeat interval received: {newInterval}s");
            this.heartbeatInterval = TimeSpan.FromSeconds(newInterval);
        }
    }

    public void Dispose()
    {
        this.Stop();
        this.hearthbeatThread.Join();
        this.cts.Dispose();
    }

    private static ulong GetCurrentTimeInNanoseconds()
    {
        return (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000; // Convert to nanoseconds
    }

    private async void HeartbeatLoop()
    {
        while (this.isRunning)
        {
            // Status is not yet received
            if (this.lastStatus == null)
            {
                try
                {
                    await Task.Delay(this.heartbeatInterval, this.cts.Token)
                        .ConfigureAwait(false);

                    continue;
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

            try
            {
                var report = this.CreateHealthReport();

                await this.dispatcher.DispatchHearthbeatAysnc(report, this.cts.Token)
                    .ConfigureAwait(false);

                await Task.Delay(this.heartbeatInterval, this.cts.Token)
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
