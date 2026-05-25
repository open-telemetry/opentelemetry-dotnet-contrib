// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Instrumentation.Exceptions.Implementation;

internal sealed class ExceptionsInstrumentationLogProcessor : BaseProcessor<LogRecord>
{
    internal const string LoggerName = "OpenTelemetry.Instrumentation.Exceptions";

    private static readonly EventId ExceptionEventId = new(0, "exception");

    private static readonly Action<ILogger, Exception?> LogUnhandledExceptionAsCritical =
        LoggerMessage.Define(
            LogLevel.Critical,
            ExceptionEventId,
            "Unhandled exception.");

    private static readonly Action<ILogger, Exception?> LogUnhandledExceptionAsError =
        LoggerMessage.Define(
            LogLevel.Error,
            ExceptionEventId,
            "Unhandled exception.");

    private static readonly Action<ILogger, Exception?> LogUnobservedTaskException =
        LoggerMessage.Define(
            LogLevel.Error,
            ExceptionEventId,
            "Unobserved task exception.");

    private static readonly Func<ExceptionObjectLogState, Exception?, string> ExceptionObjectLogFormatter = static (state, _) => state.MessageTemplate;

    private readonly ILogger logger;
    private readonly ExceptionsInstrumentationOptions options;
    private bool disposed;

    public ExceptionsInstrumentationLogProcessor(
        ILoggerFactory loggerFactory,
        ExceptionsInstrumentationOptions options)
    {
        this.logger = loggerFactory.CreateLogger(LoggerName);
        this.options = options;

        if (options.CaptureUnhandledExceptions)
        {
            AppDomain.CurrentDomain.UnhandledException += this.OnUnhandledException;
        }

        if (options.CaptureUnobservedTaskExceptions)
        {
            TaskScheduler.UnobservedTaskException += this.OnUnobservedTaskException;
        }
    }

    public override void OnEnd(LogRecord data)
    {
    }

    internal void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        this.WriteExceptionLog(
            args.IsTerminating ? LogLevel.Critical : LogLevel.Error,
            "Unhandled exception.",
            args.ExceptionObject);
    }

    internal void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        this.WriteExceptionLog(
            LogLevel.Error,
            "Unobserved task exception.",
            args.Exception);
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing || this.disposed)
        {
            return;
        }

        if (this.options.CaptureUnhandledExceptions)
        {
            AppDomain.CurrentDomain.UnhandledException -= this.OnUnhandledException;
        }

        if (this.options.CaptureUnobservedTaskExceptions)
        {
            TaskScheduler.UnobservedTaskException -= this.OnUnobservedTaskException;
        }

        this.disposed = true;

        base.Dispose(disposing);
    }

    private void WriteExceptionLog(LogLevel logLevel, string message, object? exceptionObject)
    {
        if (exceptionObject is Exception exception)
        {
            if (logLevel == LogLevel.Critical)
            {
                LogUnhandledExceptionAsCritical(this.logger, exception);
            }
            else if (message == "Unobserved task exception.")
            {
                LogUnobservedTaskException(this.logger, exception);
            }
            else
            {
                LogUnhandledExceptionAsError(this.logger, exception);
            }

            return;
        }

        if (!this.logger.IsEnabled(logLevel))
        {
            return;
        }

        this.logger.Log<ExceptionObjectLogState>(
            logLevel,
            ExceptionEventId,
            new ExceptionObjectLogState(message, exceptionObject),
            exception: null,
            ExceptionObjectLogFormatter);
    }

    private sealed class ExceptionObjectLogState : IReadOnlyList<KeyValuePair<string, object?>>
    {
        private readonly string message;
        private readonly string? exceptionType;

        public ExceptionObjectLogState(string messageTemplate, object? exceptionObject)
        {
            this.MessageTemplate = messageTemplate;
            this.message = exceptionObject?.ToString() ?? "Unknown exception object.";
            this.exceptionType = exceptionObject?.GetType().FullName;
        }

        public string MessageTemplate { get; }

        public int Count => this.exceptionType is null ? 2 : 3;

        public KeyValuePair<string, object?> this[int index] => index switch
        {
            0 => new("exception.message", this.message),
            1 when this.exceptionType is not null => new("exception.type", this.exceptionType),
            1 => new("{OriginalFormat}", this.MessageTemplate),
            2 => new("{OriginalFormat}", this.MessageTemplate),
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            yield return new("exception.message", this.message);

            if (this.exceptionType is not null)
            {
                yield return new("exception.type", this.exceptionType);
            }

            yield return new("{OriginalFormat}", this.MessageTemplate);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
