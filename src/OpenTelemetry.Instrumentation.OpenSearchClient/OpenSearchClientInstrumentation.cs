// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.OpenSearchClient.Implementation;

namespace OpenTelemetry.Instrumentation.OpenSearchClient;

/// <summary>
/// OpenSearch client instrumentation.
/// </summary>
internal class OpenSearchClientInstrumentation : IDisposable
{
    private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenSearchClientInstrumentation"/> class.
    /// </summary>
    /// <param name="options">Configuration options for OpenSearch client instrumentation.</param>
    public OpenSearchClientInstrumentation(OpenSearchClientInstrumentationOptions options)
    {
        this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(new OpenSearchRequestPipelineDiagnosticListener(options), null, OpenSearchInstrumentationEventSource.Log.UnknownErrorProcessingEvent);
        this.diagnosticSourceSubscriber.Subscribe();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.diagnosticSourceSubscriber.Dispose();
    }
}
