// <copyright file="EntityFrameworkInstrumentationOptions.cs" company="OpenTelemetry Authors">
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
using System.Data;
using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore;

/// <summary>
/// Options for <see cref="EntityFrameworkInstrumentation"/>.
/// </summary>
public class EntityFrameworkInstrumentationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether or not the <see cref="EntityFrameworkInstrumentation"/> should add the names of <see cref="CommandType.StoredProcedure"/> commands as the <see cref="SemanticConventions.AttributeDbStatement"/> tag. Default value: True.
    /// </summary>
    public bool SetDbStatementForStoredProcedure { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether or not the <see cref="EntityFrameworkInstrumentation"/> should add the text of <see cref="CommandType.Text"/> commands as the <see cref="SemanticConventions.AttributeDbStatement"/> tag. Default value: False.
    /// </summary>
    public bool SetDbStatementForText { get; set; }

    /// <summary>
    /// Gets or sets an action to enrich an Activity from the db command.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para><see cref="IDbCommand"/>: db command to allow access to command.</para>
    /// </remarks>
    public Action<Activity, IDbCommand> EnrichWithIDbCommand { get; set; }

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
    public Func<string, IDbCommand, bool> Filter { get; set; }
}
