// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Geneva;

/// <summary>
/// Contains Geneva exporter options.
/// </summary>
public class GenevaExporterOptions
{
    private IReadOnlyDictionary<string, object> fields = new Dictionary<string, object>(1)
    {
        [Schema.V40.PartA.Ver] = "4.0",
    };

    private IReadOnlyDictionary<string, string>? tableNameMappings;

    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets custom fields.
    /// </summary>
    public IEnumerable<string>? CustomFields { get; set; }

    /// <summary>
    /// Gets or sets the exception stack trace export mode.
    /// </summary>
    public ExceptionStackExportMode ExceptionStackExportMode { get; set; }

    /// <summary>
    /// Gets or sets the event name export mode.
    /// </summary>
    public EventNameExportMode EventNameExportMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether or not trace state should be included when exporting traces.
    /// </summary>
    public bool IncludeTraceStateForSpan { get; set; }

    /// <summary>
    /// Gets or sets table name mappings.
    /// </summary>
    public IReadOnlyDictionary<string, string>? TableNameMappings
    {
        get => this.tableNameMappings;
        set
        {
            Guard.ThrowIfNull(value);

            var copy = new Dictionary<string, string>(value.Count);

            foreach (var entry in value)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    throw new ArgumentException("A table name mapping key was null, empty, or consisted only of white-space characters.", nameof(this.TableNameMappings));
                }

                if (string.IsNullOrWhiteSpace(entry.Value))
                {
                    throw new ArgumentException($"The table name mapping value provided for key '{entry.Key}' was null, empty, or consisted only of white-space characters.", nameof(this.TableNameMappings));
                }

                if (Encoding.UTF8.GetByteCount(entry.Value) != entry.Value.Length)
                {
                    throw new ArgumentException($"The table name mapping value '{entry.Value}' provided for key '{entry.Key}' contained non-ASCII characters.", nameof(this.TableNameMappings));
                }

                /* Note: Validation disabled because it broke previously released versions.
                if (entry.Value != "*")
                {
                    if (!TableNameSerializer.IsValidTableName(entry.Value))
                    {
                        throw new ArgumentException($"The table name mapping value '{entry.Value}' provided for key '{entry.Key}' contained invalid characters or was too long.", nameof(this.TableNameMappings));
                    }

                    if (TableNameSerializer.IsReservedTableName(entry.Value))
                    {
                        throw new ArgumentException($"The table name mapping value '{entry.Value}' provided for key '{entry.Key}' is reserved and cannot be specified.", nameof(this.TableNameMappings));
                    }
                }*/

                copy[entry.Key] = entry.Value;
            }

            this.tableNameMappings = copy;
        }
    }

    /// <summary>
    /// Gets or sets prepopulated fields.
    /// </summary>
    public IReadOnlyDictionary<string, object> PrepopulatedFields
    {
        get => this.fields;
        set
        {
            Guard.ThrowIfNull(value);

            var schemaVersion = "4.0";

            if (value.ContainsKey(Schema.V40.PartA.Ver))
            {
                schemaVersion = value[Schema.V40.PartA.Ver] as string;
            }

            if (schemaVersion != "2.1" && schemaVersion != "4.0")
            {
                throw new ArgumentException("Unsupported schema version, only 2.1 and 4.0 are supported.");
            }

            if (value.ContainsKey(Schema.V40.PartA.Name))
            {
                throw new ArgumentException("Event name cannot be pre-populated.");
            }

            if (value.ContainsKey(Schema.V40.PartA.Time))
            {
                throw new ArgumentException("Event timestamp cannot be pre-populated.");
            }

            var copy = new Dictionary<string, object>(value.Count + 1) { [Schema.V40.PartA.Ver] = schemaVersion };
            foreach (var entry in value)
            {
                var val = entry.Value;
                switch (val)
                {
                    case bool:
                    case byte:
                    case sbyte:
                    case short:
                    case ushort:
                    case int:
                    case uint:
                    case long:
                    case ulong:
                    case float:
                    case double:
                    case string:
                        break;
                    case null:
                        throw new ArgumentNullException(entry.Key, $"{nameof(this.PrepopulatedFields)} must not contain null values.");
                    default:
                        throw new ArgumentException($"Type `{entry.Value.GetType()}` (key = `{entry.Key}`) is not allowed. Only bool, byte, sbyte, short, ushort, int, uint, long, ulong, float, double, and string are supported.");
                }

                copy[entry.Key] = val; // shallow copy
            }

            this.fields = copy;
        }
    }
}
