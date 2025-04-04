// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Serilog;
using Xunit;
using SerilogILogger = Serilog.ILogger;

namespace OpenTelemetry.Appenders.Serilog.Tests;

public class OpenTelemetrySerilogSinkTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SerilogDisposesProviderTests(bool dispose)
    {
        var testLoggerProvider = new TestLoggerProvider();

        Log.Logger = new LoggerConfiguration()
            .WriteTo.OpenTelemetry(testLoggerProvider, disposeProvider: dispose)
            .CreateLogger();

        Log.CloseAndFlush();

        Assert.Equal(dispose, testLoggerProvider.IsDisposed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SerilogBasicLogTests(bool includeRenderedMessage)
    {
        var testLoggerProvider = new TestLoggerProvider();

        Log.Logger = new LoggerConfiguration()
            .WriteTo.OpenTelemetry(
                testLoggerProvider,
                options: new() { IncludeRenderedMessage = includeRenderedMessage },
                disposeProvider: true)
            .CreateLogger();

        Log.Logger.Information("Hello {greeting}", "World");

        Log.CloseAndFlush();

        Assert.Single(testLoggerProvider.Logger.LogEntries);

        var logEntry = testLoggerProvider.Logger.LogEntries[0];

        Assert.Equal(LogLevel.Information, logEntry.LogLevel);
        Assert.Equal("Hello {greeting}", logEntry.Message);

        Assert.NotNull(logEntry.State);
        Assert.Contains(logEntry.State, kvp => kvp.Key == "greeting" && (string)kvp.Value == "World");
        Assert.Contains(logEntry.State, kvp => kvp.Key == "{OriginalFormat}" && (string)kvp.Value == "Hello {greeting}");

        if (includeRenderedMessage)
        {
            Assert.Contains(logEntry.State, kvp => kvp.Key == "serilog.rendered_message" && (string)kvp.Value == "Hello \"World\"");
        }
        else
        {
            Assert.DoesNotContain(logEntry.State, kvp => kvp.Key == "serilog.rendered_message");
        }

        Assert.Contains(logEntry.State, kvp => kvp.Key == "Timestamp" && kvp.Value is DateTime);
    }

    [Fact]
    public void SerilogBasicLogWithActivityTest()
    {
        using var activity = new Activity("Test");
        activity.Start();

        var testLoggerProvider = new TestLoggerProvider();

        Log.Logger = new LoggerConfiguration()
            .WriteTo.OpenTelemetry(testLoggerProvider, disposeProvider: true)
            .CreateLogger();

        Log.Logger.Information("Hello {greeting}", "World");

        Log.CloseAndFlush();

        Assert.Single(testLoggerProvider.Logger.LogEntries);

        var logEntry = testLoggerProvider.Logger.LogEntries[0];

        Assert.Contains(logEntry.State, kvp => kvp.Key == "SpanId" && (string)kvp.Value == activity.SpanId.ToHexString());
        Assert.Contains(logEntry.State, kvp => kvp.Key == "TraceId" && (string)kvp.Value == activity.TraceId.ToHexString());
        Assert.Contains(logEntry.State, kvp => kvp.Key == "TraceFlags" && (string)kvp.Value == activity.ActivityTraceFlags.ToString());
    }

    [Fact]
    public void SerilogCategoryNameTest()
    {
        var testLoggerProvider = new TestLoggerProvider();

        Log.Logger = new LoggerConfiguration()
            .WriteTo.OpenTelemetry(testLoggerProvider, disposeProvider: true)
            .CreateLogger();

        // Note: Serilog ForContext API is used to set "CategoryName" on log messages
        SerilogILogger logger = Log.Logger.ForContext<OpenTelemetrySerilogSinkTests>();

        logger.Information("Hello {greeting}", "World");

        Log.CloseAndFlush();

        Assert.Single(testLoggerProvider.Logger.LogEntries);

        var logEntry = testLoggerProvider.Logger.LogEntries[0];

        // The new implementation adds the source context as a property
        Assert.Contains(
            logEntry.State,
            kvp => kvp.Key == "serilog.source_context"
                && (string)kvp.Value == "OpenTelemetry.Appenders.Serilog.Tests.OpenTelemetrySerilogSinkTests");
    }

    [Fact]
    public void SerilogComplexMessageTemplateTest()
    {
        var testLoggerProvider = new TestLoggerProvider();

        Log.Logger = new LoggerConfiguration()
            .WriteTo.OpenTelemetry(testLoggerProvider, disposeProvider: true)
            .CreateLogger();

        ComplexType complexType = new();

        Log.Logger.Information("Hello {greeting} {id} {@complexObj} {$complexStr}", "World", 18, complexType, complexType);

        Log.CloseAndFlush();

        Assert.Single(testLoggerProvider.Logger.LogEntries);

        var logEntry = testLoggerProvider.Logger.LogEntries[0];

        Assert.Contains(logEntry.State, kvp => kvp.Key == "greeting" && (string)kvp.Value == "World");
        Assert.Contains(logEntry.State, kvp => kvp.Key == "id" && (int)kvp.Value == 18);
        Assert.Contains(logEntry.State, kvp => kvp.Key == "complexStr" && (string)kvp.Value == "ComplexTypeToString");
    }

    [Fact]
    public void SerilogArrayMessageTemplateTest()
    {
        var testLoggerProvider = new TestLoggerProvider();

        Log.Logger = new LoggerConfiguration()
            .WriteTo.OpenTelemetry(testLoggerProvider, disposeProvider: true)
            .CreateLogger();

        ComplexType complexType = new();

        var intArray = new int[] { 0, 1, 2, 3, 4 };
        var mixedArray = new object?[] { 0, null, "3", 18.0D };

        Log.Logger.Information("Int array {data}", intArray);
        Log.Logger.Information("Mixed array {data}", new object[] { mixedArray });

        Log.CloseAndFlush();

        Assert.Equal(2, testLoggerProvider.Logger.LogEntries.Count);

        var logEntry = testLoggerProvider.Logger.LogEntries[0];
        Assert.Contains(logEntry.State, kvp => kvp.Key == "data" && kvp.Value is object[] arrayVal && arrayVal.Length == intArray.Length);

        logEntry = testLoggerProvider.Logger.LogEntries[1];
        Assert.Contains(logEntry.State, kvp => kvp.Key == "data" && kvp.Value is object[] arrayVal);
    }

    [Fact]
    public void SerilogExceptionTest()
    {
        var testLoggerProvider = new TestLoggerProvider();

        InvalidOperationException ex = new();

        Log.Logger = new LoggerConfiguration()
            .WriteTo.OpenTelemetry(testLoggerProvider, disposeProvider: true)
            .CreateLogger();

        Log.Logger.Information(ex, "Exception");

        Log.CloseAndFlush();

        Assert.Single(testLoggerProvider.Logger.LogEntries);

        var logEntry = testLoggerProvider.Logger.LogEntries[0];

        Assert.Equal(ex, logEntry.Exception);
    }

    private sealed class TestLoggerProvider : ILoggerProvider, IDisposable
    {
        public TestLogger Logger { get; } = new();

        public bool IsDisposed { get; private set; }

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
        {
            return this.Logger;
        }

        public void Dispose()
        {
            this.IsDisposed = true;
        }
    }

    private sealed class TestLogger : Microsoft.Extensions.Logging.ILogger
    {
        public List<LogEntry> LogEntries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);
            List<KeyValuePair<string, object>> stateList = new();

            if (state is IEnumerable<KeyValuePair<string, object>> stateEnumerable)
            {
                stateList.AddRange(stateEnumerable);
            }

            this.LogEntries.Add(new LogEntry
            {
                LogLevel = logLevel,
                EventId = eventId,
                State = stateList,
                Exception = exception,
                Message = message,
            });
        }
    }

    private sealed class LogEntry
    {
        public LogLevel LogLevel { get; set; }

        public EventId EventId { get; set; }

        public List<KeyValuePair<string, object>> State { get; set; } = new();

        public Exception? Exception { get; set; }

        public string Message { get; set; } = string.Empty;
    }

    private sealed class ComplexType
    {
        public override string ToString()
        {
            return "ComplexTypeToString";
        }
    }
}
