// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Web;
using OpenTelemetry.Instrumentation.AspNet.Implementation;

namespace OpenTelemetry.Instrumentation.AspNet;

/// <summary>
/// Options for ASP.NET instrumentation.
/// </summary>
public class AspNetTraceInstrumentationOptions
{
    private const string DisableQueryRedactionEnvVar = "OTEL_DOTNET_EXPERIMENTAL_ASPNET_DISABLE_URL_QUERY_REDACTION";

    /// <summary>
    /// Initializes a new instance of the <see cref="AspNetTraceInstrumentationOptions"/> class.
    /// </summary>
    public AspNetTraceInstrumentationOptions()
    {
        try
        {
            var disableQueryRedaction = Environment.GetEnvironmentVariable(DisableQueryRedactionEnvVar);
            if (disableQueryRedaction != null && disableQueryRedaction.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                this.DisableUrlQueryRedaction = true;
            }
        }
        catch (Exception ex)
        {
            AspNetInstrumentationEventSource.Log.FailedToReadEnvironmentVariable(DisableQueryRedactionEnvVar, ex);
        }
    }

    /// <summary>
    /// Gets or sets a filter callback function that determines on a per
    /// request basis whether or not to collect telemetry.
    /// </summary>
    /// <remarks>
    /// The filter callback receives the <see cref="HttpContext"/> for the
    /// current request and should return a boolean.
    /// <list type="bullet">
    /// <item>If filter returns <see langword="true"/> the request is
    /// collected.</item>
    /// <item>If filter returns <see langword="false"/> or throws an
    /// exception the request is filtered out (NOT collected).</item>
    /// </list>
    /// </remarks>
    public Func<HttpContext, bool>? Filter { get; set; }

    /// <summary>
    /// Gets or sets an action to enrich an Activity.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para><see cref="HttpRequest"/>: the HttpRequest object from which additional information can be extracted to enrich the activity.</para>
    /// </remarks>
    public Action<Activity, HttpRequest>? EnrichWithHttpRequest { get; set; }

    /// <summary>
    /// Gets or sets an action to enrich an Activity.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para><see cref="HttpResponse"/>: the HttpResponse object from which additional information can be extracted to enrich the activity.</para>
    /// </remarks>
    public Action<Activity, HttpResponse>? EnrichWithHttpResponse { get; set; }

    /// <summary>
    /// Gets or sets an action to enrich an Activity.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para><see cref="Exception"/>: the Exception object from which additional information can be extracted to enrich the activity.</para>
    /// </remarks>
    public Action<Activity, Exception>? EnrichWithException { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the exception will be recorded as ActivityEvent or not.
    /// </summary>
    /// <remarks>
    /// See: <see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/exceptions.md"/>.
    /// </remarks>
    public bool RecordException { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the url query value should be redacted or not.
    /// </summary>
    /// <remarks>
    /// The query parameter values are redacted with value set as Redacted.
    /// e.g. `?key1=value1` is set as `?key1=Redacted`.
    /// The redaction can be disabled by setting this property to <see langword="true" />.
    /// </remarks>
    internal bool DisableUrlQueryRedaction { get; set; }
}
