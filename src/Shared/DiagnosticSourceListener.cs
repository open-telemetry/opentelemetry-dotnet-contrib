// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation;

internal sealed class DiagnosticSourceListener : IObserver<KeyValuePair<string, object?>>
{
    private static readonly Func<bool> IsInstrumentationSuppressed = CreateIsInstrumentationSuppressed();

    private readonly ListenerHandler handler;

    private readonly Action<string, string, Exception>? logUnknownException;

    public DiagnosticSourceListener(ListenerHandler handler, Action<string, string, Exception>? logUnknownException)
    {
        Guard.ThrowIfNull(handler);

        this.handler = handler;
        this.logUnknownException = logUnknownException;
    }

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public void OnNext(KeyValuePair<string, object?> value)
    {
        if (!this.handler.SupportsNullActivity && Activity.Current == null)
        {
            return;
        }

        if (IsInstrumentationSuppressed()
            && !value.Key.EndsWith("Start", StringComparison.Ordinal)
            && !value.Key.EndsWith("Stop", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            this.handler.OnEventWritten(value.Key, value.Value);
        }
        catch (Exception ex)
        {
            this.logUnknownException?.Invoke(this.handler.SourceName, value.Key, ex);
        }
    }

    private static Func<bool> CreateIsInstrumentationSuppressed()
    {
        var getter = Type.GetType("OpenTelemetry.Sdk, OpenTelemetry", throwOnError: false)?
            .GetProperty("SuppressInstrumentation", BindingFlags.Public | BindingFlags.Static)?
            .GetMethod;

        return getter != null
            ? getter.CreateDelegate<Func<bool>>()
            : static () => false;
    }
}
