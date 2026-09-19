// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using static OpenTelemetry.Internal.DatabaseSemanticConventionHelper;

namespace OpenTelemetry.Instrumentation.ElasticsearchClient;

/// <summary>
/// Options for Elasticsearch client instrumentation.
/// </summary>
public class ElasticsearchClientInstrumentationOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ElasticsearchClientInstrumentationOptions"/> class.
    /// </summary>
    public ElasticsearchClientInstrumentationOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    internal ElasticsearchClientInstrumentationOptions(IConfiguration configuration)
    {
        var databaseSemanticConvention = GetSemanticConventionOptIn(configuration);
        this.EmitOldAttributes = databaseSemanticConvention.HasFlag(DatabaseSemanticConvention.Old);
        this.EmitNewAttributes = databaseSemanticConvention.HasFlag(DatabaseSemanticConvention.New);
    }

    /// <summary>
    /// Gets or sets a value indicating whether down stream instrumentation (HttpClient) is suppressed (disabled).
    /// </summary>
    public bool SuppressDownstreamInstrumentation { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the JSON request body should be parsed out of the request debug information and formatted as indented JSON.
    /// </summary>
    public bool ParseAndFormatRequest { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether or not the OpenTelemetry.Instrumentation.ElasticsearchClient
    /// should add the request information as db.statement attribute tag. Default value: True.
    /// </summary>
    public bool SetDbStatementForRequest { get; set; } = true;

    /// <summary>
    /// Gets or sets a max length allowed for the db.statement attribute. Default value: 4096.
    /// </summary>
    public int MaxDbStatementLength { get; set; } = 4096;

    /// <summary>
    /// Gets or sets an action to enrich an Activity.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para>string: the name of the event.</para>
    /// <para>object: the raw object from which additional information can be extracted to enrich the activity.
    /// The type of this object depends on the event, which is given by the above parameter.</para>
    /// </remarks>
    public Action<Activity, string, object?>? Enrich { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the old database attributes should be emitted.
    /// </summary>
    internal bool EmitOldAttributes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the new database attributes should be emitted.
    /// </summary>
    internal bool EmitNewAttributes { get; set; }
}
