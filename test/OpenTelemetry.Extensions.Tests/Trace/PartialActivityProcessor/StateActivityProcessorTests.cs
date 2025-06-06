// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Extensions.Trace.PartialActivityProcessor;
using Xunit;

namespace OpenTelemetry.Extensions.Tests.Trace.PartialActivityProcessor;

public class StateActivityProcessorTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithValidLogger()
    {
        var testLogger = new TestLogger();
        var processor = new StateActivityProcessor(testLogger);

        Assert.NotNull(processor);
    }

    [Fact]
    public void Constructor_ShouldThrowExceptionWhenLoggerIsNull() =>
        Assert.Throws<ArgumentNullException>(() => new StateActivityProcessor(null!));

    [Fact]
    public void OnStart_ShouldCallLogger()
    {
        var testLogger = new TestLogger();
        var processor = new StateActivityProcessor(testLogger);
        var activity = new Activity("TestActivity");
        activity.Start();

        processor.OnStart(activity);

        Assert.Equal(1, testLogger.LogCallCount);
    }

    [Fact]
    public void OnEnd_ShouldCallLogger()
    {
        var testLogger = new TestLogger();
        var processor = new StateActivityProcessor(testLogger);
        var activity = new Activity("TestActivity");
        activity.Start();
        activity.Stop();

        processor.OnEnd(activity);

        Assert.Equal(1, testLogger.LogCallCount);
    }

    private class TestLogger : ILogger
    {
        public int LogCallCount { get; private set; }

        IDisposable ILogger.BeginScope<TState>(TState state) => null!;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            this.LogCallCount++;
    }
}
