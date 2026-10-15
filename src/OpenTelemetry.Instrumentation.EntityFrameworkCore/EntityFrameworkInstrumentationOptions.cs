// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Instrumentation.EntityFrameworkCore.Implementation;
using static OpenTelemetry.Internal.DatabaseSemanticConventionHelper;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore;

/// <summary>
/// Options for <see cref="EntityFrameworkInstrumentation"/>.
/// </summary>
public class EntityFrameworkInstrumentationOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkInstrumentationOptions"/> class.
    /// </summary>
    public EntityFrameworkInstrumentationOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    internal EntityFrameworkInstrumentationOptions(IConfiguration configuration)
    {
        var databaseSemanticConvention = GetSemanticConventionOptIn(configuration);
        this.EmitOldAttributes = databaseSemanticConvention.HasFlag(DatabaseSemanticConvention.Old);
        this.EmitNewAttributes = databaseSemanticConvention.HasFlag(DatabaseSemanticConvention.New);

        if (configuration["OTEL_DOTNET_EXPERIMENTAL_EFCORE_ENABLE_TRACE_DB_QUERY_PARAMETERS"] is { Length: > 0 } value &&
            bool.TryParse(value, out var setDbQueryParameters))
        {
            this.SetDbQueryParameters = setDbQueryParameters;
        }

        this.QueryTextSanitizer = EntityFrameworkQueryTextProcessor.SanitizeQueryText;
    }

    /// <summary>
    /// Gets or sets an action to enrich an Activity from the db command.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para><see cref="IDbCommand"/>: db command to allow access to command.</para>
    /// </remarks>
    public Action<Activity, IDbCommand>? EnrichWithIDbCommand { get; set; }

    /// <summary>
    /// Gets or sets a filter function that determines whether or not to
    /// collect telemetry about a command from a particular provider.
    /// </summary>
    /// <remarks>
    /// <b>Notes:</b>
    /// <list type="bullet">
    /// <item>The first parameter passed to the filter function is the provider name.</item>
    /// <item>The second parameter passed to the filter function is <see cref="IDbCommand"/> from which additional
    /// information can be extracted.</item>
    /// <item>The return value for the filter:
    /// <list type="number">
    /// <item>If filter returns <see langword="true" />, the command is
    /// collected.</item>
    /// <item>If filter returns <see langword="false" /> or throws an
    /// exception, the command is <b>NOT</b> collected.</item>
    /// </list></item>
    /// </list>
    /// </remarks>
    public Func<string?, IDbCommand, bool>? Filter { get; set; }

    /// <summary>
    /// Gets or sets a function that sanitizes command text before it is emitted as the
    /// <c>db.statement</c> or <c>db.query.text</c> tag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The function receives a <see cref="DbQueryTextSanitizationContext"/> and is called only
    /// for commands whose <see cref="IDbCommand.CommandType"/> is <see cref="CommandType.Text"/>.
    /// Its return value controls whether the original, sanitized, or no query text is emitted;
    /// see <see cref="DbQueryTextSanitizationResult"/>.
    /// </para>
    /// <para>
    /// By default, SQL-like queries are sanitized by replacing literal values with <c>?</c>;
    /// other query dialects are emitted unchanged. The default function is assigned before the
    /// configuration callback runs, so it can be captured and wrapped.
    /// </para>
    /// <para>
    /// If the function throws, query text is omitted and the rest of the command telemetry is
    /// collected. Setting this property to <see langword="null"/> disables sanitization and
    /// emits raw query text, which may contain sensitive data.
    /// </para>
    /// </remarks>
    public DbQueryTextSanitizer? QueryTextSanitizer { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether or not the <see cref="EntityFrameworkInstrumentation"/>
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
    /// Gets or sets a value indicating whether the old database attributes should be emitted.
    /// </summary>
    internal bool EmitOldAttributes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the new database attributes should be emitted.
    /// </summary>
    internal bool EmitNewAttributes { get; set; }
}
