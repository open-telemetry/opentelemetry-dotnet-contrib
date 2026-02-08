// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Events.SurrealDb;
using OpenTelemetry.Instrumentation.SurrealDb.Implementation;

namespace OpenTelemetry.Instrumentation.SurrealDb;

internal sealed class SurrealDbInstrumentation : IDisposable
{
    public static readonly SurrealDbInstrumentation Instance = new();

    public static SurrealDbTraceInstrumentationOptions TracingOptions { get; set; } = new();

    internal const string SurrealDbDiagnosticListenerName = nameof(SurrealDbDiagnosticListener);

    public readonly InstrumentationHandleManager HandleManager = new();

    private static readonly HashSet<string> DiagnosticSourceEvents =
    [
        SurrealDbBeforeExecuteMethod.Name,
        SurrealDbBeforeExecuteQuery.Name,
        SurrealDbExecuteMethod.Name,
        SurrealDbAfterExecuteMethod.Name,
        SurrealDbExecuteError.Name,
    ];

    private readonly Func<string, object?, object?, bool> isEnabled = (eventName, _, _)
        => DiagnosticSourceEvents.Contains(eventName);

    private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurrealDbInstrumentation"/> class.
    /// </summary>
    private SurrealDbInstrumentation()
    {
        this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(
            name => new SurrealDbDiagnosticListener(name),
            listener => listener.Name == SurrealDbDiagnosticListenerName,
            this.isEnabled,
            SurrealDbInstrumentationEventSource.Log.UnknownErrorProcessingEvent);
        this.diagnosticSourceSubscriber.Subscribe();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.diagnosticSourceSubscriber.Dispose();
    }
}
