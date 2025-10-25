// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.States;
using Hangfire.Storage;

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

        this.Server = new BackgroundJobServer();
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

        while (!Completed() && !cts.IsCancellationRequested)
        {
            await Task.Delay(500);
        }

        bool Completed()
        {
            var jobDetails = this.MonitoringApi.JobDetails(jobId);

            if (jobDetails == null)
            {
                return false;
            }

            // Copy the history to an array to avoid exception if the collection is modified while iterating
            var history = jobDetails.History.ToArray();

            return history.Any(h => terminalStates.Contains(h.StateName));
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
            var jobDetails = this.MonitoringApi.JobDetails(jobId);

            if (jobDetails == null)
            {
                return false;
            }

            // Copy the history to an array to avoid exception if the collection is modified while iterating
            var history = jobDetails.History.ToArray();

            return history.Any(h => h.StateName == targetState);
        }
    }

    public void Dispose()
    {
        this.Server.Dispose();
    }
}
