// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;

namespace Microsoft.Extensions.Logging;

internal sealed class AddTraceIdLogger : ILogger
{
    private readonly ILogger innerLogger;

    public AddTraceIdLogger(ILogger baseLogger)
    {
        this.innerLogger = baseLogger;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => this.innerLogger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => this.innerLogger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (Activity.Current is { TraceId: { } traceId })
        {
            this.innerLogger.Log(logLevel, eventId, new FormattedLogValues<TState>(state, traceId), exception, (state, ex) => formatter(state.Inner, ex));
        }
        else
        {
            this.innerLogger.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
