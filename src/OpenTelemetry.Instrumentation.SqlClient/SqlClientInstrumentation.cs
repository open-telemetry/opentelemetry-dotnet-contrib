// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using OpenTelemetry.Instrumentation.SqlClient.Implementation;

namespace OpenTelemetry.Instrumentation.SqlClient;

/// <summary>
/// SqlClient instrumentation.
/// </summary>
internal sealed class SqlClientInstrumentation : IDisposable
{
    public static readonly SqlClientInstrumentation Instance = new SqlClientInstrumentation();

    internal const string SqlClientDiagnosticListenerName = "SqlClientDiagnosticListener";
#if NET
    internal const string SqlClientTrimmingUnsupportedMessage = "Trimming is not yet supported with SqlClient instrumentation.";
#endif
    internal static int TracingHandles;
#if NETFRAMEWORK
    private readonly SqlEventSourceListener sqlEventSourceListener;
#else
    private static readonly HashSet<string> DiagnosticSourceEvents =
    [
        "System.Data.SqlClient.WriteCommandBefore",
        "Microsoft.Data.SqlClient.WriteCommandBefore",
        "System.Data.SqlClient.WriteCommandAfter",
        "Microsoft.Data.SqlClient.WriteCommandAfter",
        "System.Data.SqlClient.WriteCommandError",
        "Microsoft.Data.SqlClient.WriteCommandError"
    ];

    private readonly Func<string, object?, object?, bool> isEnabled = (eventName, _, _)
        => DiagnosticSourceEvents.Contains(eventName);

    private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlClientInstrumentation"/> class.
    /// </summary>
#if NET
    [RequiresUnreferencedCode(SqlClientTrimmingUnsupportedMessage)]
#endif
    private SqlClientInstrumentation()
    {
#if NETFRAMEWORK
        this.sqlEventSourceListener = new SqlEventSourceListener();
#else
        this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(
           name => new SqlClientDiagnosticListener(name),
           listener => listener.Name == SqlClientDiagnosticListenerName,
           this.isEnabled,
           SqlClientInstrumentationEventSource.Log.UnknownErrorProcessingEvent);
        this.diagnosticSourceSubscriber.Subscribe();
#endif
    }

    public static SqlClientTraceInstrumentationOptions TracingOptions { get; set; } = new SqlClientTraceInstrumentationOptions();

    /// <inheritdoc/>
    public void Dispose()
    {
#if NETFRAMEWORK
        this.sqlEventSourceListener?.Dispose();
#else
        this.diagnosticSourceSubscriber?.Dispose();
#endif
    }

    internal class TracingHandle : IDisposable
    {
        private bool disposed;

        public TracingHandle()
        {
            Interlocked.Increment(ref TracingHandles);
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                Interlocked.Decrement(ref TracingHandles);
                this.disposed = true;
            }
        }
    }
}
