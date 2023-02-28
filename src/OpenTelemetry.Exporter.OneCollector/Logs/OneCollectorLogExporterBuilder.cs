// <copyright file="OneCollectorLogExporterBuilder.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter.OneCollector;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains methods for building <see cref="OneCollectorLogExporterOptions"/> instances.
/// </summary>
public sealed class OneCollectorLogExporterBuilder
{
    internal OneCollectorLogExporterBuilder(string? connectionString)
    {
        this.Options = new()
        {
            ConnectionString = connectionString,
        };
    }

    internal OneCollectorLogExporterBuilder(IConfiguration configuration)
        : this(connectionString: null)
    {
        Debug.Assert(configuration != null, "configuration was null");

        configuration.Bind(this.Options);
    }

    internal OneCollectorLogExporterOptions Options { get; }

    /// <summary>
    /// Register a callback action for configuring the batch options of <see
    /// cref="OneCollectorLogExporterOptions"/>.
    /// </summary>
    /// <param name="configure">Callback action for configuring <see
    /// cref="BatchExportProcessorOptions{T}"/>.</param>
    /// <returns>The supplied <see cref="OneCollectorLogExporterBuilder"/> for
    /// call chaining.</returns>
    public OneCollectorLogExporterBuilder ConfigureBatchOptions(Action<BatchExportProcessorOptions<LogRecord>> configure)
    {
        Guard.ThrowIfNull(configure);

        configure(this.Options.BatchOptions);

        return this;
    }

    /// <summary>
    /// Register a callback action for configuring the transport options of <see
    /// cref="OneCollectorLogExporterOptions"/>.
    /// </summary>
    /// <param name="configure">Callback action for configuring <see
    /// cref="OneCollectorExporterTransportOptions"/>.</param>
    /// <returns>The supplied <see cref="OneCollectorLogExporterBuilder"/> for
    /// call chaining.</returns>
    public OneCollectorLogExporterBuilder ConfigureTransportOptions(Action<OneCollectorExporterTransportOptions> configure)
    {
        Guard.ThrowIfNull(configure);

        configure(this.Options.TransportOptions);

        return this;
    }

    /// <summary>
    /// Sets the <see cref="OneCollectorExporterOptions.ConnectionString"/>
    /// property.
    /// </summary>
    /// <remarks><inheritdoc
    /// cref="OneCollectorExporterOptions.ConnectionString"
    /// path="/remarks"/></remarks>
    /// <param name="connectionString">Connection string.</param>
    /// <returns>The supplied <see cref="OneCollectorLogExporterBuilder"/> for
    /// call chaining.</returns>
    public OneCollectorLogExporterBuilder SetConnectionString(string connectionString)
    {
        Guard.ThrowIfNullOrWhitespace(connectionString);

        this.Options.ConnectionString = connectionString;

        return this;
    }

    /// <summary>
    /// Sets the <see cref="OneCollectorLogExporterOptions.DefaultEventName"/>
    /// property. Default value: <c>Log</c>.
    /// </summary>
    /// <remarks><inheritdoc
    /// cref="OneCollectorLogExporterOptions.DefaultEventName"
    /// path="/remarks"/></remarks>
    /// <param name="defaultEventName">Default event name.</param>
    /// <returns>The supplied <see cref="OneCollectorLogExporterBuilder"/> for
    /// call chaining.</returns>
    public OneCollectorLogExporterBuilder SetDefaultEventName(string defaultEventName)
    {
        Guard.ThrowIfNullOrWhitespace(defaultEventName);

        this.Options.DefaultEventName = defaultEventName;

        return this;
    }
}
