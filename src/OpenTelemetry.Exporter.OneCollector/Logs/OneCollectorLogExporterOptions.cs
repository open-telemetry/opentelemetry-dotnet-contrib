// <copyright file="OneCollectorLogExporterOptions.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.OneCollector;

/// <summary>
/// Contains options used to build a <see cref="OneCollectorExporter{T}"/>
/// instance for exporting <see cref="LogRecord"/> telemetry data.
/// </summary>
public sealed class OneCollectorLogExporterOptions : OneCollectorExporterOptions
{
    /// <summary>
    /// Gets or sets the default event name. Default value: <c>Log</c>.
    /// </summary>
    /// <remarks>
    /// Note: The default event name is used when an <see
    /// cref="LogRecord.EventId"/> has a null or whitespace <see
    /// cref="EventId.Name"/>.
    /// </remarks>
    public string DefaultEventName { get; set; } = "Log";

    /// <summary>
    /// Gets the OneCollector log serialization options.
    /// </summary>
    public OneCollectorLogExporterSerializationOptions SerializationOptions { get; } = new();

    /// <summary>
    /// Gets or sets the default event namespace. Default value:
    /// <c>OpenTelemetry.Logs</c>.
    /// </summary>
    /// <remarks>
    /// Note: The default event namespace is used if a <see
    /// cref="LogRecord.CategoryName"/> is not supplied. This is internal at the
    /// moment because using the <see cref="ILogger"/> interface there should
    /// always be a category name.
    /// </remarks>
    internal string DefaultEventNamespace { get; set; } = "OpenTelemetry.Logs";

    internal override void Validate()
    {
        if (string.IsNullOrWhiteSpace(this.DefaultEventNamespace))
        {
            throw new OneCollectorExporterValidationException($"{nameof(this.DefaultEventNamespace)} was not specified on {nameof(OneCollectorLogExporterOptions)} options.");
        }

        if (string.IsNullOrWhiteSpace(this.DefaultEventName))
        {
            throw new OneCollectorExporterValidationException($"{nameof(this.DefaultEventName)} was not specified on {nameof(OneCollectorLogExporterOptions)} options.");
        }

        this.SerializationOptions.Validate();

        base.Validate();
    }
}
