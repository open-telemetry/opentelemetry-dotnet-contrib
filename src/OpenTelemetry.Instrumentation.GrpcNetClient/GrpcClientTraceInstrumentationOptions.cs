// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Instrumentation.GrpcNetClient;

/// <summary>
/// Options for GrpcClient instrumentation.
/// </summary>
public class GrpcClientTraceInstrumentationOptions
{
    /// <summary>
    /// Gets or sets a filter function that determines whether or not to
    /// collect telemetry on a per request basis.
    /// </summary>
    /// <remarks>
    /// <para>The return value for the filter function is interpreted as:</para>
    /// <list type="bullet">
    /// <item>If the filter returns <see langword="true" />, the request is collected.</item>
    /// <item>If the filter returns <see langword="false" /> or throws an exception, the request is not collected.</item>
    /// </list>
    /// </remarks>
    public Func<HttpRequestMessage, bool>? Filter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether down stream instrumentation is suppressed (disabled).
    /// </summary>
    public bool SuppressDownstreamInstrumentation { get; set; }

    /// <summary>
    /// Gets or sets an action to enrich the Activity with <see cref="HttpRequestMessage"/>.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para><see cref="HttpRequestMessage"/> object from which additional information can be extracted to enrich the activity.</para>
    /// </remarks>
    public Action<Activity, HttpRequestMessage>? EnrichWithHttpRequestMessage { get; set; }

    /// <summary>
    /// Gets or sets an action to enrich an Activity with <see cref="HttpResponseMessage"/>.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para><see cref="HttpResponseMessage"/> object from which additional information can be extracted to enrich the activity.</para>
    /// </remarks>
    public Action<Activity, HttpResponseMessage>? EnrichWithHttpResponseMessage { get; set; }
}
