// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

#pragma warning disable IDE0005 // Using directive is unnecessary.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry.Internal;
#pragma warning restore IDE0005 // Using directive is unnecessary.

namespace OpenTelemetry.Instrumentation;

internal sealed class DiagnosticSourceListener : IObserver<KeyValuePair<string, object?>>
{
    private readonly ListenerHandler handler;

    private readonly Action<string, string, Exception> logUnknownException;

    public DiagnosticSourceListener(ListenerHandler handler, Action<string, string, Exception> logUnknownException)
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

        try
        {
            if (value.Key.EndsWith("Start", StringComparison.Ordinal))
            {
                this.handler.OnStartActivity(Activity.Current, value.Value);
            }
            else if (value.Key.EndsWith("Stop", StringComparison.Ordinal))
            {
                this.handler.OnStopActivity(Activity.Current, value.Value);
            }
            else if (value.Key.EndsWith("Exception", StringComparison.Ordinal))
            {
                this.handler.OnException(Activity.Current, value.Value);
            }
            else
            {
                this.handler.OnCustom(value.Key, Activity.Current, value.Value);
            }
        }
        catch (Exception ex)
        {
            this.logUnknownException?.Invoke(this.handler.SourceName, value.Key, ex);
        }
    }
}
