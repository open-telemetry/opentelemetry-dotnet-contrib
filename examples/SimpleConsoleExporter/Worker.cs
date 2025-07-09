// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Examples.SimpleConsoleExporter;

public class Worker(
    ILogger<Worker> logger,
    TimeProvider timeProvider,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("Examples.SimpleConsoleExporter.Worker");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await this.DoWork(stoppingToken);
    }

    private async Task DoWork(CancellationToken cancellationToken)
    {
        // Short delay to that startup messages are output
        await Task.Delay(100, cancellationToken);

        logger.LogTrace("This is a trace message");

        // Structured logging
        logger.LogDebug("Debug: User {UserId} performed {Action} at {Time}", 42, "Login", timeProvider.GetUtcNow());

        // ActivitySource tracing
        using (var activity = ActivitySource.StartActivity("SampleOperation", ActivityKind.Internal))
        {
            activity?.SetTag("sample.tag", "value");
            logger.LogInformation("Activity {ActivityId} started at {Time}", activity?.Id, timeProvider.GetUtcNow());

            // Simulate some work
            Task.Delay(100, cancellationToken).Wait(cancellationToken);
            logger.LogWarning("Activity finished warning");
        }

        // Exception logging
        try
        {
            this.ThrowSampleException();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An exception occurred");
        }

        // Logging with a scope
        using (logger.BeginScope("ScopeId: {ScopeId}", Guid.NewGuid()))
        {
            logger.LogCritical("This critical log is inside a scope");
        }

        // Logging with custom state (dictionary)
        var customState = new Dictionary<string, object>
        {
            ["CustomKey1"] = "CustomValue1",
            ["CustomKey2"] = 1234,
        };
        logger.Log(
            LogLevel.Debug,
            new EventId(1001, "CustomStateEvent"),
            customState,
            null,
            (state, ex) => $"This log has custom state: {state["CustomKey1"]}");

        // Short delay before stopping
        await Task.Delay(100, cancellationToken);

        // Stop the host after logging
        lifetime.StopApplication();

        return;
    }

    private void ThrowSampleException()
    {
        throw new InvalidOperationException("This is a sample exception for demonstration purposes.");
    }
}
