// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

namespace OpenTelemetry.Instrumentation.Hangfire.Tests;

public class HangfireFixture : IDisposable
{
    public HangfireFixture()
    {
        GlobalConfiguration.Configuration
            .UseMemoryStorage();

        // Remove AutomaticRetryAttribute from global filters for tests
        // This ensures jobs fail immediately without retries, allowing state transitions to be tested
        var retryFilter = GlobalJobFilters.Filters.FirstOrDefault(f => f.Instance is AutomaticRetryAttribute);
        if (retryFilter != null)
        {
            GlobalJobFilters.Filters.Remove(retryFilter.Instance);
        }

        // Configure server with faster schedule polling for tests
        // Default is 15 seconds, which is too slow for test scenarios
        var options = new BackgroundJobServerOptions
        {
            SchedulePollingInterval = TimeSpan.FromMilliseconds(100),
        };

        this.Server = new BackgroundJobServer(options);
        this.MonitoringApi = JobStorage.Current.GetMonitoringApi();
    }

    public BackgroundJobServer Server { get; }

    public IMonitoringApi MonitoringApi { get; }

    /// <summary>
    /// Waits for a Hangfire job to be processed (reach a terminal state).
    /// </summary>
    /// <param name="jobId">The ID of the job to wait for.</param>
    /// <param name="timeToWaitInSeconds">Maximum time to wait in seconds.</param>
    /// <returns>A task that completes when the job reaches a terminal state or the timeout expires.</returns>
    public async Task WaitJobProcessedAsync(string jobId, int timeToWaitInSeconds)
    {
        var timeout = TimeSpan.FromSeconds(timeToWaitInSeconds);
        using var cts = new CancellationTokenSource(timeout);

        string[] terminalStates = [SucceededState.StateName, FailedState.StateName, DeletedState.StateName];

        while (!cts.IsCancellationRequested)
        {
            if (Completed())
            {
                return;
            }

            await Task.Delay(50, cts.Token);
        }

        Assert.Fail("Timeout");

        bool Completed()
        {
            var history = this.GetHistory(jobId);

            return history != null && history.Any(h => terminalStates.Contains(h.StateName));
        }
    }

    /// <summary>
    /// Waits for a Hangfire job to reach a specific state.
    /// </summary>
    /// <param name="jobId">The ID of the job to wait for.</param>
    /// <param name="targetState">The target state to wait for (e.g., "Scheduled", "Enqueued").</param>
    /// <param name="timeToWaitInSeconds">Maximum time to wait in seconds.</param>
    /// <returns>A task that completes when the job reaches the target state or the timeout expires.</returns>
    public async Task WaitJobInStateAsync(string jobId, string targetState, int timeToWaitInSeconds)
    {
        var timeout = TimeSpan.FromSeconds(timeToWaitInSeconds);
        using var cts = new CancellationTokenSource(timeout);

        while (!InState() && !cts.IsCancellationRequested)
        {
            await Task.Delay(100);
        }

        bool InState()
        {
            var history = this.GetHistory(jobId);

            return history != null && history.Any(h => h.StateName == targetState);
        }
    }

    public void Dispose()
        => this.Server.Dispose();

    /// <summary>
    /// Gets the state history of a job, or <see langword="null"/> if it is not (yet) available.
    /// </summary>
    /// <param name="jobId">The ID of the job.</param>
    /// <returns>The state history, or <see langword="null"/> if the job was not found or could not be read.</returns>
    private StateHistoryDto[]? GetHistory(string jobId)
    {
        try
        {
            // Hangfire.MemoryStorage's StateHistoryList is not thread-safe, and
            // JobDetails() copies it without locking, so the copy can fail with
            // an ArgumentException (or InvalidOperationException) if the server
            // appends a state concurrently. Treat that as "not available yet" so
            // the caller polls again; the next attempt sees a consistent history.
            return this.MonitoringApi.JobDetails(jobId)?.History.ToArray();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
