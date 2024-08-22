// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.OneCollector;

/// <summary>
/// asda.
/// </summary>
public sealed class OneCollectorLogExporterTableMappingOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether table mapping logic should be used.
    /// </summary>
    /// <remarks>
    /// When enabled the exporter will map Log Categories to matching tables.
    /// </remarks>
    public bool UseTableMapping { get; set; }

    /// <summary>
    /// Gets or sets a dictionary that maps Log Categories to Table Names.
    /// </summary>
    public IDictionary<string, string> TableMappings { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the default table name, used when no mapping is found.
    /// </summary>
    public string DefaultTableName { get; set; } = "Log";
}
