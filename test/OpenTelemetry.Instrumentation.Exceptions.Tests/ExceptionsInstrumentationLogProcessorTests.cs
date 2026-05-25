// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Instrumentation.Exceptions.Implementation;
using OpenTelemetry.Logs;
using Xunit;

namespace OpenTelemetry.Instrumentation.Exceptions.Tests;

public class ExceptionsInstrumentationLogProcessorTests
{
    [Fact]
    public void UnhandledException_IsCapturedAsCritical_WhenTerminating()
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = CreateLoggerFactory(logRecords);
        using var processor = new ExceptionsInstrumentationLogProcessor(
            loggerFactory,
            new ExceptionsInstrumentationOptions());

        var exception = new InvalidOperationException("boom");

        processor.OnUnhandledException(this, new UnhandledExceptionEventArgs(exception, isTerminating: true));

        var logRecord = Assert.Single(logRecords);
        Assert.Equal(LogLevel.Critical, logRecord.LogLevel);
        Assert.Equal("Unhandled exception.", logRecord.FormattedMessage);
        Assert.Equal("exception", logRecord.EventId.Name);
        Assert.Same(exception, logRecord.Exception);
    }

    [Fact]
    public void UnhandledException_IsCapturedAsError_WhenNonTerminating()
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = CreateLoggerFactory(logRecords);
        using var processor = new ExceptionsInstrumentationLogProcessor(
            loggerFactory,
            new ExceptionsInstrumentationOptions());

        var exception = new InvalidOperationException("boom");

        processor.OnUnhandledException(this, new UnhandledExceptionEventArgs(exception, isTerminating: false));

        var logRecord = Assert.Single(logRecords);
        Assert.Equal(LogLevel.Error, logRecord.LogLevel);
        Assert.Equal("Unhandled exception.", logRecord.FormattedMessage);
        Assert.Equal("exception", logRecord.EventId.Name);
        Assert.Same(exception, logRecord.Exception);
    }

    [Fact]
    public void UnobservedTaskException_IsCapturedAsError()
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = CreateLoggerFactory(logRecords);
        using var processor = new ExceptionsInstrumentationLogProcessor(
            loggerFactory,
            new ExceptionsInstrumentationOptions());

        var exception = new AggregateException(new InvalidOperationException("task failed"));

        processor.OnUnobservedTaskException(
            sender: null,
            new UnobservedTaskExceptionEventArgs(exception));

        var logRecord = Assert.Single(logRecords);
        Assert.Equal(LogLevel.Error, logRecord.LogLevel);
        Assert.Equal("Unobserved task exception.", logRecord.FormattedMessage);
        Assert.Equal("exception", logRecord.EventId.Name);
        Assert.Same(exception, logRecord.Exception);
    }

    [Fact]
    public void UnhandledException_NonExceptionObject_EmitsSemanticAttributes()
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = CreateLoggerFactory(logRecords);
        using var processor = new ExceptionsInstrumentationLogProcessor(
            loggerFactory,
            new ExceptionsInstrumentationOptions());

        var exceptionObject = new NonExceptionObject("boom");

        processor.OnUnhandledException(this, new UnhandledExceptionEventArgs(exceptionObject, isTerminating: false));

        var logRecord = Assert.Single(logRecords);
        Assert.Null(logRecord.Exception);
        Assert.Equal(LogLevel.Error, logRecord.LogLevel);
        Assert.Equal("exception", logRecord.EventId.Name);

        var attributes = logRecord.Attributes!.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Assert.Equal("boom", attributes["exception.message"]);
        Assert.Equal(typeof(NonExceptionObject).FullName, attributes["exception.type"]);
    }

    [Fact]
    public void UnhandledException_NullExceptionObject_EmitsFallbackMessage()
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = CreateLoggerFactory(logRecords);
        using var processor = new ExceptionsInstrumentationLogProcessor(
            loggerFactory,
            new ExceptionsInstrumentationOptions());

        processor.OnUnhandledException(this, new UnhandledExceptionEventArgs(null!, isTerminating: false));

        var logRecord = Assert.Single(logRecords);
        var attributes = logRecord.Attributes!.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        Assert.Equal("Unknown exception object.", attributes["exception.message"]);
        Assert.DoesNotContain("exception.type", attributes.Keys);
    }

    [Fact]
    public void OptionsDefaultToCapturingBothHandlers()
    {
        var options = new ExceptionsInstrumentationOptions();

        Assert.True(options.CaptureUnhandledExceptions);
        Assert.True(options.CaptureUnobservedTaskExceptions);
    }

    [Fact]
    public void DisposeCanBeCalledMultipleTimes()
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = CreateLoggerFactory(logRecords);
        var processor = new ExceptionsInstrumentationLogProcessor(
            loggerFactory,
            new ExceptionsInstrumentationOptions());

        processor.Dispose();
        processor.Dispose();
    }

    [Fact]
    public void DisposeCanBeCalled_WhenHandlersDisabled()
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = CreateLoggerFactory(logRecords);
        using var processor = new ExceptionsInstrumentationLogProcessor(
            loggerFactory,
            new ExceptionsInstrumentationOptions
            {
                CaptureUnhandledExceptions = false,
                CaptureUnobservedTaskExceptions = false,
            });
    }

    [Fact]
    public void OnEndDoesNothing()
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = CreateLoggerFactory(logRecords);
        using var processor = new ExceptionsInstrumentationLogProcessor(
            loggerFactory,
            new ExceptionsInstrumentationOptions());
        using var testLoggerFactory = CreateLoggerFactory(logRecords);

        var logger = testLoggerFactory.CreateLogger("test");
        logger.LogInformation("hello");

        processor.OnEnd(logRecords[0]);

        Assert.Single(logRecords);
    }

    [Fact]
    public void AddExceptionsInstrumentation_GuardsBuilder()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ExceptionsLoggerProviderBuilderExtensions.AddExceptionsInstrumentation((LoggerProviderBuilder)null!));
    }

    [Fact]
    public void AddExceptionsInstrumentation_GuardsOptions()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ExceptionsLoggerProviderBuilderExtensions.AddExceptionsInstrumentation((OpenTelemetryLoggerOptions)null!));
    }

    [Fact]
    public void AddExceptionsInstrumentation_DefaultLoggerProviderBuilderOverload_CapturesUnhandledExceptions()
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder => builder
            .AddOpenTelemetry(builder =>
            {
                Assert.NotNull(builder.AddExceptionsInstrumentation());
                builder.AddInMemoryExporter(logRecords);
            })
            .AddFilter("*", LogLevel.Trace));

        var exception = new InvalidOperationException("boom");
        RaiseUnhandledException(loggerFactory, exception, isTerminating: false);

        var logRecord = Assert.Single(logRecords);
        Assert.Same(exception, logRecord.Exception);
    }

    [Fact]
    public void AddExceptionsInstrumentation_OnOpenTelemetryLoggerOptions_CapturesUnobservedTaskExceptions()
    {
        var logRecords = new List<LogRecord>();
        var options = new OpenTelemetryLoggerOptions
        {
            IncludeFormattedMessage = true,
            ParseStateValues = true,
        };

        Assert.Same(options, options.AddExceptionsInstrumentation(configuration => configuration.CaptureUnhandledExceptions = false));

        using var loggerFactory = CreateLoggerFactory(logRecords);
        using var services = new ServiceCollection()
            .AddSingleton(loggerFactory)
            .BuildServiceProvider();

        var processorFactory = Assert.Single(GetProcessorFactories(options));
        using var processor = Assert.IsType<ExceptionsInstrumentationLogProcessor>(processorFactory(services));

        processor.OnUnobservedTaskException(
            sender: null,
            new UnobservedTaskExceptionEventArgs(new AggregateException(new InvalidOperationException("task failed"))));

        var logRecord = Assert.Single(logRecords);
        Assert.Equal("exception", logRecord.EventId.Name);
        Assert.NotNull(logRecord.Exception);
    }

    [Fact]
    public void AddExceptionsInstrumentation_DefaultOpenTelemetryLoggerOptionsOverload_CapturesUnhandledExceptions()
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder => builder
            .AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.ParseStateValues = true;
                Assert.NotNull(options.AddExceptionsInstrumentation());
                options.AddInMemoryExporter(logRecords);
            })
            .AddFilter("*", LogLevel.Trace));

        var exception = new InvalidOperationException("boom");
        RaiseUnhandledException(loggerFactory, exception, isTerminating: false);

        var logRecord = Assert.Single(logRecords);
        Assert.Same(exception, logRecord.Exception);
    }

    [Fact]
    public void AddExceptionsInstrumentation_OnLoggerProviderBuilder_CanDisableUnobservedTaskExceptions()
    {
        var builder = new TestLoggerProviderBuilder();

        Assert.Same(builder, builder.AddExceptionsInstrumentation(options => options.CaptureUnobservedTaskExceptions = false));
    }

    [Fact]
    public void UnhandledException_NonExceptionObject_IsIgnored_WhenLogLevelDisabled()
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder => builder
            .AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.ParseStateValues = true;
                options.AddInMemoryExporter(logRecords);
            })
            .AddFilter("*", LogLevel.Critical));
        using var processor = new ExceptionsInstrumentationLogProcessor(
            loggerFactory,
            new ExceptionsInstrumentationOptions());

        processor.OnUnhandledException(this, new UnhandledExceptionEventArgs(new NonExceptionObject("boom"), isTerminating: false));

        Assert.Empty(logRecords);
    }

    [Fact]
    public void DisposeFalse_DoesNotUnsubscribeOrDispose()
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = CreateLoggerFactory(logRecords);
        using var testLoggerFactory = CreateLoggerFactory(logRecords);
        var processor = new ExceptionsInstrumentationLogProcessor(
            loggerFactory,
            new ExceptionsInstrumentationOptions());

        typeof(ExceptionsInstrumentationLogProcessor)
            .GetMethod("Dispose", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(processor, [false]);

        RaiseUnhandledException(testLoggerFactory, new InvalidOperationException("boom"), isTerminating: false);

        var logRecord = Assert.Single(logRecords);
        Assert.Equal("Unhandled exception.", logRecord.FormattedMessage);

        processor.Dispose();
    }

    [Fact]
    public void ExceptionObjectLogState_IndexerAndEnumeratorCoverAllBranches()
    {
        var stateType = typeof(ExceptionsInstrumentationLogProcessor).GetNestedType("ExceptionObjectLogState", BindingFlags.NonPublic);
        Assert.NotNull(stateType);

        var typedState = Activator.CreateInstance(stateType!, ["Unhandled exception.", new NonExceptionObject("boom")]);
        var nullState = Activator.CreateInstance(stateType!, ["Unhandled exception.", null!]);

        Assert.Equal("Unhandled exception.", stateType!.GetProperty("MessageTemplate")!.GetValue(typedState));
        Assert.Equal(3, stateType.GetProperty("Count")!.GetValue(typedState));
        Assert.Equal(2, stateType.GetProperty("Count")!.GetValue(nullState));

        Assert.Equal("exception.message", GetStateItemKey(typedState!, 0));
        Assert.Equal("exception.type", GetStateItemKey(typedState!, 1));
        Assert.Equal("{OriginalFormat}", GetStateItemKey(typedState!, 2));
        Assert.Equal("{OriginalFormat}", GetStateItemKey(nullState!, 1));

        Assert.Throws<TargetInvocationException>(() => _ = stateType.GetProperty("Item")!.GetValue(typedState, [3]));

        var typedEnumerable = (System.Collections.IEnumerable)typedState!;
        var typedItems = typedEnumerable.Cast<KeyValuePair<string, object?>>().ToArray();
        Assert.Equal(3, typedItems.Length);

        var nullEnumerable = (System.Collections.IEnumerable)nullState!;
        var nullItems = nullEnumerable.Cast<KeyValuePair<string, object?>>().ToArray();
        Assert.Equal(2, nullItems.Length);

        var nongenericEnumerator = stateType.GetMethod(
            "System.Collections.IEnumerable.GetEnumerator",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(nongenericEnumerator);
        Assert.NotNull(nongenericEnumerator!.Invoke(typedState, null));
    }

    private static ILoggerFactory CreateLoggerFactory(List<LogRecord> logRecords)
    {
        return LoggerFactory.Create(builder => builder
            .AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.ParseStateValues = true;
                options.AddInMemoryExporter(logRecords);
            })
            .AddFilter("*", LogLevel.Trace));
    }

    private static string GetStateItemKey(object state, int index)
    {
        return ((KeyValuePair<string, object?>)state.GetType().GetProperty("Item")!.GetValue(state, [index])!).Key;
    }

    private static IReadOnlyList<Func<IServiceProvider, BaseProcessor<LogRecord>>> GetProcessorFactories(OpenTelemetryLoggerOptions options)
    {
        var field = typeof(OpenTelemetryLoggerOptions).GetField("ProcessorFactories", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        return Assert.IsAssignableFrom<IReadOnlyList<Func<IServiceProvider, BaseProcessor<LogRecord>>>>(field!.GetValue(options));
    }

    private static void RaiseUnhandledException(ILoggerFactory loggerFactory, Exception exception, bool isTerminating)
    {
        using var processor = new ExceptionsInstrumentationLogProcessor(
            loggerFactory,
            new ExceptionsInstrumentationOptions
            {
                CaptureUnobservedTaskExceptions = false,
            });

        processor.OnUnhandledException(typeof(ExceptionsInstrumentationLogProcessorTests), new UnhandledExceptionEventArgs(exception, isTerminating));
    }

    private static void RaiseUnobservedTaskException()
    {
        var task = Task.Run(static () => throw new InvalidOperationException("task failed"));

        SpinWait.SpinUntil(() => task.IsCompleted, TimeSpan.FromSeconds(5));

        var weakReference = new WeakReference(task);
        task = null!;

        for (var i = 0; i < 10 && weakReference.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Thread.Sleep(50);
        }
    }

    private sealed class NonExceptionObject(string message)
    {
        public override string ToString() => message;
    }

    private sealed class TestLoggerProviderBuilder : LoggerProviderBuilder
    {
        public override LoggerProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
        {
            return this;
        }
    }
}
