// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Http.Implementation;

namespace OpenTelemetry.Instrumentation.Http;

/// <summary>
/// HttpClient instrumentation.
/// </summary>
internal sealed class HttpClientInstrumentation : IDisposable
{
    internal static readonly Version SemanticConventionsVersion = new(1, 41, 0);

    // The names of the events written by DiagnosticsHandler that the instrumentation does not use.
    // https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Net.Http/src/System/Net/Http/DiagnosticsHandlerLoggingStrings.cs
    private const string RequestEvent = "System.Net.Http.Request";
    private const string ResponseEvent = "System.Net.Http.Response";
    private const string ActivityEvent = "System.Net.Http.HttpRequestOut";

    private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpClientInstrumentation"/> class.
    /// </summary>
    /// <param name="options">Configuration options for HTTP client instrumentation.</param>
    public HttpClientInstrumentation(HttpClientTraceInstrumentationOptions options)
    {
        this.diagnosticSourceSubscriber = new(
            new HttpHandlerDiagnosticListener(options),
            CreateIsEnabledFilter(options),
            HttpInstrumentationEventSource.Log.UnknownErrorProcessingEvent);

        this.diagnosticSourceSubscriber.Subscribe();
    }

    /// <inheritdoc/>
    public void Dispose()
        => this.diagnosticSourceSubscriber?.Dispose();

    internal static Func<string, object?, object?, bool> CreateIsEnabledFilter(HttpClientTraceInstrumentationOptions options)
    {
        // For .NET 7+ activity will be created using ActivitySource.
        // https://github.com/dotnet/runtime/blob/main/src/libraries/System.Net.Http/src/System/Net/Http/DiagnosticsHandler.cs
        // However, in case when activity creation returns null (due to sampling)
        // the framework will fall back to creating an activity anyway due to active diagnostic source listener.
        // To prevent this, isEnabled is implemented which will return false always
        // so that the sampler's decision is respected.
        if (HttpHandlerDiagnosticListener.IsNet10OrGreater)
        {
            // On .NET 10+ the runtime sets the response tags and the status of the activity natively
            // and always stops the activity itself, so the Stop event is only needed for enrichment.
            // https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Net.Http/src/System/Net/Http/DiagnosticsHandler.cs#L194-L233
            return (eventName, _, _) => eventName switch
            {
                RequestEvent or ResponseEvent or ActivityEvent => false,
                HttpHandlerDiagnosticListener.OnStopEvent => options.EnrichWithHttpResponseMessage != null,
                _ => true,
            };
        }

        return HttpHandlerDiagnosticListener.IsNet7OrGreater
            ? static (eventName, _, _) => eventName is not (RequestEvent or ResponseEvent or ActivityEvent)
            : static (eventName, _, _) => eventName is not (RequestEvent or ResponseEvent);
    }
}
