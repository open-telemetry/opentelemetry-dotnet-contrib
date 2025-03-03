// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
#pragma warning disable OTEL1001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using System.Diagnostics;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Xunit;
using ILogger = Serilog.ILogger;

namespace OpenTelemetry.Appenders.Serilog.Tests;

public class OpenTelemetrySerilogSinkTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SerilogDisposesProviderTests(bool dispose)
    {
        var disposeTrackingProcessor = new DisposeTrackingProcessor();

        using (var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .AddProcessor(disposeTrackingProcessor)
            .Build())
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.OpenTelemetry(loggerProvider, disposeProvider: dispose)
                .CreateLogger();

            Log.CloseAndFlush();

            Assert.Equal(dispose, disposeTrackingProcessor.Disposed);
        }

        Assert.True(disposeTrackingProcessor.Disposed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SerilogBasicLogTests(bool includeRenderedMessage)
    {
        List<LogRecord> exportedItems = new();

        var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .WriteTo.OpenTelemetry(
                loggerProvider,
                options: new() { IncludeRenderedMessage = includeRenderedMessage },
                disposeProvider: true)
            .CreateLogger();

        Log.Logger.Information("Hello {greeting}", "World");

        Log.CloseAndFlush();

        Assert.Single(exportedItems);

        LogRecord logRecord = exportedItems[0];

        Assert.Equal("Hello {greeting}", logRecord.Body);

        Assert.Null(logRecord.FormattedMessage);

        Assert.NotNull(logRecord.Attributes);

        if (!includeRenderedMessage)
        {
            Assert.Single(logRecord.Attributes);
        }
        else
        {
            Assert.Contains(logRecord.Attributes, kvp => kvp.Key == "serilog.rendered_message" && (string?)kvp.Value == "Hello \"World\"");
        }

        Assert.NotEqual(DateTime.MinValue, logRecord.Timestamp);
        Assert.Equal(DateTimeKind.Utc, logRecord.Timestamp.Kind);
        Assert.Equal(LogRecordSeverity.Info, logRecord.Severity);
        Assert.Equal(nameof(LogEventLevel.Information), logRecord.SeverityText);
        Assert.Equal("OpenTelemetry.Appenders.Serilog", logRecord.CategoryName);

        Assert.Contains(logRecord.Attributes, kvp => kvp.Key == "greeting" && (string?)kvp.Value == "World");

        Assert.Equal(default, logRecord.TraceId);
        Assert.Equal(default, logRecord.SpanId);
        Assert.Null(logRecord.TraceState);
        Assert.Equal(ActivityTraceFlags.None, logRecord.TraceFlags);
    }

    [Fact]
    public void SerilogBasicLogWithActivityTest()
    {
        using var activity = new Activity("Test");
        activity.Start();

        List<LogRecord> exportedItems = new();

        var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .WriteTo.OpenTelemetry(loggerProvider, disposeProvider: true)
            .CreateLogger();

        Log.Logger.Information("Hello {greeting}", "World");

        Log.CloseAndFlush();

        Assert.Single(exportedItems);

        var logRecord = exportedItems[0];

        Assert.NotEqual(default, logRecord.TraceId);

        Assert.Equal(activity.TraceId, logRecord.TraceId);
        Assert.Equal(activity.SpanId, logRecord.SpanId);
        Assert.Equal(activity.TraceStateString, logRecord.TraceState);
        Assert.Equal(activity.ActivityTraceFlags, logRecord.TraceFlags);
    }

    [Fact]
    public void SerilogCategoryNameTest()
    {
        List<LogRecord> exportedItems = new();

        var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .WriteTo.OpenTelemetry(loggerProvider, disposeProvider: true)
            .CreateLogger();

        // Note: Serilog ForContext API is used to set "CategoryName" on log messages
        ILogger logger = Log.Logger.ForContext<OpenTelemetrySerilogSinkTests>();

        logger.Information("Hello {greeting}", "World");

        Log.CloseAndFlush();

        Assert.Single(exportedItems);

        LogRecord logRecord = exportedItems[0];

        Assert.Equal("OpenTelemetry.Appenders.Serilog", logRecord.CategoryName);

        Assert.NotNull(logRecord.Attributes);

        Assert.Contains(
            logRecord.Attributes,
            kvp => kvp.Key == "serilog.source_context"
                && (string?)kvp.Value == "OpenTelemetry.Appenders.Serilog.Tests.OpenTelemetrySerilogSinkTests");
    }

    [Fact]
    public void SerilogComplexMessageTemplateTest()
    {
        List<LogRecord> exportedItems = new();

        var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .WriteTo.OpenTelemetry(loggerProvider, disposeProvider: true)
            .CreateLogger();

        ComplexType complexType = new();

        Log.Logger.Information("Hello {greeting} {id} {@complexObj} {$complexStr}", "World", 18, complexType, complexType);

        Log.CloseAndFlush();

        Assert.Single(exportedItems);

        LogRecord logRecord = exportedItems[0];

        Assert.NotNull(logRecord.Attributes);
        Assert.Equal(3, logRecord.Attributes!.Count); // Note: complexObj is currently not supported/ignored.
        Assert.Contains(logRecord.Attributes, kvp => kvp.Key == "greeting" && (string?)kvp.Value == "World");
        Assert.Contains(logRecord.Attributes, kvp => kvp.Key == "id" && (int?)kvp.Value == 18);
        Assert.Contains(logRecord.Attributes, kvp => kvp.Key == "complexStr" && (string?)kvp.Value == "ComplexTypeToString");
    }

    [Fact]
    public void SerilogArrayMessageTemplateTest()
    {
        List<LogRecord> exportedItems = new();

        var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .WriteTo.OpenTelemetry(loggerProvider, disposeProvider: true)
            .CreateLogger();

        ComplexType complexType = new();

        var intArray = new int[] { 0, 1, 2, 3, 4 };
        var mixedArray = new object?[] { 0, null, "3", 18.0D };

        Log.Logger.Information("Int array {data}", intArray);
        Log.Logger.Information("Mixed array {data}", new object[] { mixedArray });

        Log.CloseAndFlush();

        Assert.Equal(2, exportedItems.Count);

        LogRecord logRecord = exportedItems[0];

        Assert.NotNull(logRecord.Attributes);
        Assert.Contains(logRecord.Attributes, kvp => kvp.Key == "data" && kvp.Value is int[] typedArray && intArray.SequenceEqual(typedArray));

        logRecord = exportedItems[1];
        Assert.NotNull(logRecord.Attributes);
        Assert.Contains(logRecord.Attributes, kvp => kvp.Key == "data" && kvp.Value is object?[] typedArray && mixedArray.SequenceEqual(typedArray));
    }

    [Fact]
    public void SerilogExceptionTest()
    {
        List<LogRecord> exportedItems = new();

        InvalidOperationException ex = new();

        var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .WriteTo.OpenTelemetry(loggerProvider, disposeProvider: true)
            .CreateLogger();

        Log.Logger.Information(ex, "Exception");

        Log.CloseAndFlush();

        Assert.Single(exportedItems);

        LogRecord logRecord = exportedItems[0];

        Assert.Null(logRecord.Exception);

        Assert.NotNull(logRecord.Attributes);

        Assert.Contains(logRecord.Attributes, kvp => kvp.Key == SemanticConventions.AttributeExceptionType && (string?)kvp.Value == ex.GetType().Name);
        Assert.Contains(logRecord.Attributes, kvp => kvp.Key == SemanticConventions.AttributeExceptionMessage && (string?)kvp.Value == ex.Message);
        Assert.Contains(logRecord.Attributes, kvp => kvp.Key == SemanticConventions.AttributeExceptionStacktrace && (string?)kvp.Value == ex.ToString());
    }

    private sealed class DisposeTrackingProcessor : BaseProcessor<LogRecord>
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            this.Disposed = true;

            base.Dispose(disposing);
        }
    }

    private sealed class ComplexType
    {
        public override string ToString() => "ComplexTypeToString";
    }
}
