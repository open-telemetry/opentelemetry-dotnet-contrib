// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
    /// Gets or sets the table mapping options.
    /// </summary>
    public OneCollectorLogExporterTableMappingOptions TableMappingOptions { get; set; } = new();

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
