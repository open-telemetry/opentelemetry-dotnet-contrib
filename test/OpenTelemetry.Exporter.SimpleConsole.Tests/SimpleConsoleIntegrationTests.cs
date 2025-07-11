// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using Xunit;

namespace OpenTelemetry.Exporter.SimpleConsole.Tests;

[Trait("CategoryName", "SimpleConsoleIntegrationTests")]
public class SimpleConsoleIntegrationTests
{
    [Fact]
    public void BasicLogIntegrationTest()
    {
        // Arrange
        var mockConsole = new MockConsole();
        using var loggerFactory = LoggerFactory.Create(logging => logging
            .AddOpenTelemetry(options =>
            {
                options.AddSimpleConsoleExporter(configure =>
                {
                    configure.Console = mockConsole;
                });
            }));

        // Act
        var logger = loggerFactory.CreateLogger<SimpleConsoleIntegrationTests>();

        logger.LogInformation("Test log message from SimpleConsole exporter");

        // Assert
        var output = mockConsole.GetOutput();

        var lines = Regex.Split(output, "\r?\n");

        // First line should NOT contain trace ID when no activity is present
        Assert.StartsWith("info: OpenTelemetry.Exporter.SimpleConsole.Tests.SimpleConsoleIntegrationTests[0]", lines[0].Trim());
        Assert.DoesNotMatch(@"info: .*\[0\] [0-9a-f]+$", lines[0].Trim());
        Assert.Equal($"      Test log message from SimpleConsole exporter", lines[1].TrimEnd());

        // Verify color changes: fg and bg for severity, then restore both
        Assert.Equal(4, mockConsole.ColorChanges.Count);
        Assert.Equal(("Foreground", ConsoleColor.DarkGreen), mockConsole.ColorChanges[0]); // Severity fg
        Assert.Equal(("Background", ConsoleColor.Black), mockConsole.ColorChanges[1]); // Severity bg
        Assert.Equal(("Foreground", ConsoleColor.White), mockConsole.ColorChanges[2]); // Restore fg
        Assert.Equal(("Background", ConsoleColor.Black), mockConsole.ColorChanges[3]); // Restore bg
    }

    [Theory]
    [InlineData(LogLevel.Trace, "TestApp", 1, "Trace message", "trce")]
    [InlineData(LogLevel.Debug, "MyApp.Services", 42, "Debug message", "dbug")]
    [InlineData(LogLevel.Information, "OpenTelemetry.Exporter.SimpleConsole.Tests.SimpleConsoleIntegrationTests", 0, "Info message", "info")]
    [InlineData(LogLevel.Warning, "MyApp.Controllers", 100, "Warning message", "warn")]
    [InlineData(LogLevel.Error, "MyApp.DataAccess", 500, "Error message", "fail")]
    [InlineData(LogLevel.Critical, "MyApp.Startup", 999, "Critical message", "crit")]
    public void LogLevelAndFormatTheoryTest(LogLevel logLevel, string category, int eventId, string message, string expectedSeverity)
    {
        // Arrange
        var mockConsole = new MockConsole();
        using var loggerFactory = LoggerFactory.Create(logging => logging
            .SetMinimumLevel(LogLevel.Trace)
            .AddOpenTelemetry(options =>
            {
                options.AddSimpleConsoleExporter(configure =>
                {
                    configure.Console = mockConsole;
                });
            }));

        // Act
        var logger = loggerFactory.CreateLogger(category);
#pragma warning disable CA2254 // Template should be a static string
        logger.Log(logLevel, new EventId(eventId), message);
#pragma warning restore CA2254 // Template should be a static string

        // Assert
        var output = mockConsole.GetOutput();

        var lines = Regex.Split(output, "\r?\n");
        Assert.StartsWith($"{expectedSeverity}: {category}[{eventId}]", lines[0].Trim());
        Assert.Equal($"      {message}", lines[1].TrimEnd());
    }

    [Fact]
    public void StructuredLoggingWithSemanticArgumentsTest()
    {
        // Arrange
        var mockConsole = new MockConsole();
        using var loggerFactory = LoggerFactory.Create(logging => logging
            .SetMinimumLevel(LogLevel.Trace)
            .AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.AddSimpleConsoleExporter(configure =>
                {
                    configure.Console = mockConsole;
                });
            }));

        // Act
        var logger = loggerFactory.CreateLogger<SimpleConsoleIntegrationTests>();
        var userName = "Alice";
        var userId = 12345;
        logger.LogInformation("User {UserName} with ID {UserId} logged in", userName, userId);

        // Assert
        var output = mockConsole.GetOutput();

        var lines = Regex.Split(output, "\r?\n");
        Assert.StartsWith("info: OpenTelemetry.Exporter.SimpleConsole.Tests.SimpleConsoleIntegrationTests[0]", lines[0].Trim());
        Assert.Equal("      User Alice with ID 12345 logged in", lines[1].TrimEnd());
    }

    [Fact]
    public void ExceptionLogIntegrationTest()
    {
        // Arrange
        var mockConsole = new MockConsole();
        using var loggerFactory = LoggerFactory.Create(logging => logging
            .AddOpenTelemetry(options =>
            {
                options.AddSimpleConsoleExporter(configure =>
                {
                    configure.Console = mockConsole;
                });
            }));

        // Act
        var logger = loggerFactory.CreateLogger<SimpleConsoleIntegrationTests>();
        Exception ex;
        try
        {
            throw new InvalidOperationException("Something went wrong!");
        }
        catch (Exception caught)
        {
            ex = caught;
        }

        logger.LogError(ex, "This is an error with exception");

        // Assert
        var output = mockConsole.GetOutput();
        var lines = Regex.Split(output, "\r?\n");
        Assert.StartsWith("fail: OpenTelemetry.Exporter.SimpleConsole.Tests.SimpleConsoleIntegrationTests[0]", lines[0].Trim());
        Assert.Equal($"      This is an error with exception", lines[1].TrimEnd());
        Assert.Contains("System.InvalidOperationException: Something went wrong!", output);

        // Should contain at least one stack trace line, indented
        Assert.Contains("      at ", output);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TimestampOutputTest(bool useUtc)
    {
        // Arrange
        var mockConsole = new MockConsole();
        var timestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
        using var loggerFactory = LoggerFactory.Create(logging => logging
            .AddOpenTelemetry(options =>
            {
                options.AddSimpleConsoleExporter(configure =>
                {
                    configure.Console = mockConsole;
                    configure.TimestampFormat = timestampFormat;
                    configure.UseUtcTimestamp = useUtc;
                });
            }));

        // Act
        var logger = loggerFactory.CreateLogger<SimpleConsoleIntegrationTests>();
        var before = useUtc ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
        logger.LogInformation("Timestamped log message");

        // Assert
        var output = mockConsole.GetOutput();
        var lines = Regex.Split(output, "\r?\n");

        var match = Regex.Match(lines[0], @"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}) info: ");
        Assert.True(match.Success, "Timestamp not found in the correct format.");

        var timestampStr = match.Groups[1].Value;
        DateTimeOffset parsedTimestamp;

        if (useUtc)
        {
            // For UTC, parse as UTC and ensure offset is zero
            parsedTimestamp = DateTimeOffset.ParseExact(timestampStr, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            Assert.Equal(TimeSpan.Zero, parsedTimestamp.Offset);
        }
        else
        {
            // For local, parse as local and ensure offset matches local system
            parsedTimestamp = DateTimeOffset.ParseExact(timestampStr, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
            Assert.Equal(before.Offset, parsedTimestamp.Offset);
        }

        // Check that the parsed timestamp is within the expected range (with a tolerance of 5 seconds)
        var timeDifference = (parsedTimestamp - before).Duration();
        Assert.True(timeDifference.TotalSeconds < 5, $"Timestamp is not within the expected range. Difference: {timeDifference.TotalSeconds} seconds.");
    }

    [Fact]
    public void ActivityContextOutputTest()
    {
        // Arrange
        var mockConsole = new MockConsole();
        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        using var loggerFactory = LoggerFactory.Create(logging => logging
            .AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.AddSimpleConsoleExporter(configure =>
                {
                    configure.Console = mockConsole;
                });
            }));

        // Act
        var logger = loggerFactory.CreateLogger<SimpleConsoleIntegrationTests>();
        using var activitySource = new ActivitySource("TestActivitySource");
        using var activity = activitySource.StartActivity("TestActivity");

        // Log the activity ID in the message, as in the example
        logger.LogInformation("Activity {ActivityId} started", activity?.Id);

        // Assert
        var output = mockConsole.GetOutput();
        var lines = output.Split('\n');

        // Check that we have at least 2 lines (first line + message)
        Assert.True(lines.Length >= 2, $"Expected at least 2 lines, got {lines.Length}");

        // Check first line contains trace ID with ".." (default length 8 < 30)
        var firstLine = lines[0].TrimEnd();
        Assert.Matches(@"info: .*\[\d+\] [0-9a-f]{8}\.\.", firstLine);

        // Check second line contains the activity message
        var messageLine = lines[1].TrimEnd();
        Assert.Matches(@"Activity 00-[0-9a-f]{32}-[0-9a-f]{16}-00 started", messageLine);
    }

    [Fact]
    public void SpanIdOutputTest()
    {
        // Arrange
        var mockConsole = new MockConsole();
        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        using var loggerFactory = LoggerFactory.Create(logging => logging
            .AddOpenTelemetry(options =>
            {
                options.AddSimpleConsoleExporter(configure =>
                {
                    configure.Console = mockConsole;
                    configure.IncludeSpanId = true;
                    configure.TraceIdLength = 32; // Full trace ID length
                });
            }));

        // Act
        var logger = loggerFactory.CreateLogger<SimpleConsoleIntegrationTests>();
        using var activitySource = new ActivitySource("TestActivitySource");
        using var activity = activitySource.StartActivity("TestActivity");

        // Use a static message (no activity ID in the message)
        logger.LogInformation("Static log message");

        // Assert
        var output = mockConsole.GetOutput();
        var lines = Regex.Split(output, "\r?\n");

        // First line should contain both trace ID and span ID
        var firstLine = lines.FirstOrDefault(l => l.Contains("info:"));
        Assert.NotNull(firstLine);
        Assert.Matches(@"info: .*\[0\] [0-9a-f]{32}-[0-9a-f]{16}$", firstLine);
    }

    [Fact]
    public void TraceIdLengthTest()
    {
        // Arrange
        var mockConsole = new MockConsole();
        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        using var loggerFactory = LoggerFactory.Create(logging => logging
            .AddOpenTelemetry(options =>
            {
                options.AddSimpleConsoleExporter(configure =>
                {
                    configure.Console = mockConsole;
                    configure.TraceIdLength = 16;
                });
            }));

        // Act
        var logger = loggerFactory.CreateLogger<SimpleConsoleIntegrationTests>();
        using var activitySource = new ActivitySource("TestActivitySource");
        using var activity = activitySource.StartActivity("TestActivity");

        logger.LogInformation("Test message");

        // Assert
        var output = mockConsole.GetOutput();
        var lines = output.Split('\n');

        // Check first line contains trace ID
        var firstLine = lines[0].TrimEnd();
        Assert.Matches(@"info: .*\[\d+\] [0-9a-f]{16}\.\.$", firstLine);
    }
}
