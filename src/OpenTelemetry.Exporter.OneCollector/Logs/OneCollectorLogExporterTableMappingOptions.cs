// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.OneCollector;

/// <summary>
/// asda.
/// </summary>
public sealed class OneCollectorLogExporterTableMappingOptions
{
    /// <summary>
    /// String that represents a catch-all namespace.
    /// </summary>
    public const string CatchAllNamespace = "*";

    /// <summary>
    /// String that represents the default table name, in case no mapping is found.
    /// </summary>
    public const string DefaultTableName = "Log";

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
}
