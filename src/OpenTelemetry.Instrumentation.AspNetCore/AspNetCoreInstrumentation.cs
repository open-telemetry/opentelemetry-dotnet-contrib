// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.AspNetCore.Implementation;

namespace OpenTelemetry.Instrumentation.AspNetCore;

/// <summary>
/// Asp.Net Core Requests instrumentation.
/// </summary>
internal sealed class AspNetCoreInstrumentation : IDisposable
{
    internal static readonly Version SemanticConventionsVersion = new(1, 42, 0);

    internal static readonly Func<string, object?, object?, bool> IsEnabled = static (eventName, _, _)
        => IsDiagnosticSourceEventEnabled(eventName);

    private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

    public AspNetCoreInstrumentation(HttpInListener httpInListener)
    {
        this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(httpInListener, IsEnabled, AspNetCoreInstrumentationEventSource.Log.UnknownErrorProcessingEvent);
        this.diagnosticSourceSubscriber.Subscribe();
    }

    /// <inheritdoc/>
    public void Dispose()
        => this.diagnosticSourceSubscriber?.Dispose();

    /// <summary>
    /// Determines whether the specified ASP.NET Core diagnostic event is one the instrumentation handles.
    /// </summary>
    /// <param name="eventName">The name of the diagnostic event.</param>
    /// <returns>
    /// <see langword="true"/> if the event is handled by the instrumentation; otherwise <see langword="false"/>.
    /// </returns>
    internal static bool IsDiagnosticSourceEventEnabled(string eventName) => eventName switch
    {
        HttpInListener.ActivityOperationName or
        HttpInListener.OnStartEvent or
        HttpInListener.OnStopEvent or
        HttpInListener.OnUnHandledDiagnosticsExceptionEvent or
        HttpInListener.OnUnhandledHostingExceptionEvent => true,
        _ => false,
    };
}
