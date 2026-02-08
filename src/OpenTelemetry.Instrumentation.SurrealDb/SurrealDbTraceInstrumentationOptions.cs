// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Events.SurrealDb;
using OpenTelemetry.Instrumentation.SurrealDb.Implementation;

namespace OpenTelemetry.Instrumentation.SurrealDb;

/// <summary>
/// Options for <see cref="SurrealDbInstrumentation"/>.
/// </summary>
public class SurrealDbTraceInstrumentationOptions
{
    internal const string ContextPropagationLevelEnvVar =
        "OTEL_DOTNET_EXPERIMENTAL_SURREALDB_CLIENT_ENABLE_TRACE_CONTEXT_PROPAGATION";
    internal const string SetDbQueryParametersEnvVar =
        "OTEL_DOTNET_EXPERIMENTAL_SURREALDB_CLIENT_ENABLE_TRACE_DB_QUERY_PARAMETERS";

    /// <summary>
    /// Initializes a new instance of the <see cref="SurrealDbTraceInstrumentationOptions"/> class.
    /// </summary>
    public SurrealDbTraceInstrumentationOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build()) { }

    internal SurrealDbTraceInstrumentationOptions(IConfiguration configuration)
    {
        if (
            configuration!.TryGetBoolValue(
                SurrealDbInstrumentationEventSource.Log,
                ContextPropagationLevelEnvVar,
                out var enableTraceContextPropagation
            )
        )
        {
            this.EnableTraceContextPropagation = enableTraceContextPropagation;
        }

        if (
            configuration!.TryGetBoolValue(
                SurrealDbInstrumentationEventSource.Log,
                SetDbQueryParametersEnvVar,
                out var setDbQueryParameters
            )
        )
        {
            this.SetDbQueryParameters = setDbQueryParameters;
        }
    }

    /// <summary>
    /// Gets or sets a filter function that determines whether or not to collect telemetry about a method.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>The first parameter passed to the filter function is the event triggered before the method is being executed.</item>
    /// <item>The return value for the filter function is interpreted as:
    /// <list type="bullet">
    /// <item>If filter returns <see langword="true" />, the method is collected.</item>
    /// <item>If filter returns <see langword="false" /> or throws an exception the method is NOT collected.</item>
    /// </list></item>
    /// </list>
    /// </remarks>
    public Func<SurrealDbBeforeExecuteMethod, bool>? Filter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the exception will be
    /// recorded as <see cref="ActivityEvent"/> or not.
    /// Default value: <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para>For specification details see: <see
    /// href="https://github.com/open-telemetry/semantic-conventions/blob/main/docs/exceptions/exceptions-spans.md"/>.</para>
    /// </remarks>
    public bool RecordException { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether or not the <see cref="SurrealDbInstrumentation"/>
    /// should add the names and values of query parameters as the <c>db.query.parameter.{key}</c> tag.
    /// Default value: <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>WARNING: SetDbQueryParameters will capture the raw
    /// <c>Value</c>. Make sure your query parameters never
    /// contain any sensitive data.</b>
    /// </para>
    /// </remarks>
    internal bool SetDbQueryParameters { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to send traceparent information to a SurrealDB database.
    /// </summary>
    internal bool EnableTraceContextPropagation { get; set; }
}
