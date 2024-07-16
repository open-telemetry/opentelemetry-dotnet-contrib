// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using OpenTelemetry.Instrumentation.SqlClient.Implementation;

namespace OpenTelemetry.Instrumentation.SqlClient;

#if NET6_0_OR_GREATER
/// <summary>
/// SqlClient metric instrumentation.
/// </summary>
internal sealed class SqlClientMetricInstrumentation : IDisposable
{
    internal const string SqlClientDiagnosticListenerName = "SqlClientDiagnosticListener";
    internal const string SqlClientTrimmingUnsupportedMessage = "Trimming is not yet supported with SqlClient instrumentation.";

    private static readonly HashSet<string> DiagnosticSourceEvents = new()
    {
        "System.Data.SqlClient.WriteCommandBefore",
        "Microsoft.Data.SqlClient.WriteCommandBefore",
        "System.Data.SqlClient.WriteCommandAfter",
        "Microsoft.Data.SqlClient.WriteCommandAfter",
        "System.Data.SqlClient.WriteCommandError",
        "Microsoft.Data.SqlClient.WriteCommandError",
    };

    private readonly Func<string, object?, object?, bool> isEnabled = (eventName, _, _)
        => DiagnosticSourceEvents.Contains(eventName);

    private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlClientMetricInstrumentation"/> class.
    /// </summary>
    [RequiresUnreferencedCode(SqlClientTrimmingUnsupportedMessage)]
    public SqlClientMetricInstrumentation()
    {
        this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(
           name => new SqlClientMetricDiagnosticListener(name),
           listener => listener.Name == SqlClientDiagnosticListenerName,
           this.isEnabled,
           SqlClientInstrumentationEventSource.Log.UnknownErrorProcessingEvent);
        this.diagnosticSourceSubscriber.Subscribe();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.diagnosticSourceSubscriber?.Dispose();
    }
}
#endif
