// <copyright file="ElasticsearchClientInstrumentationOptions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Diagnostics;

namespace OpenTelemetry.Instrumentation.ElasticsearchClient;

/// <summary>
/// Options for Elasticsearch client instrumentation.
/// </summary>
public class ElasticsearchClientInstrumentationOptions
{
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
    public Action<Activity, string, object> Enrich { get; set; }
}
