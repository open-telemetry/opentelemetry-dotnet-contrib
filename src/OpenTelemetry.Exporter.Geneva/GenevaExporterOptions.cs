// <copyright file="GenevaExporterOptions.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Geneva;

public class GenevaExporterOptions
{
    private IReadOnlyDictionary<string, object> _fields = new Dictionary<string, object>(1)
    {
        [Schema.V40.PartA.Ver] = "4.0",
    };

    private IReadOnlyDictionary<string, string> _tableNameMappings;

    public string ConnectionString { get; set; }

    public IEnumerable<string> CustomFields { get; set; }

    public IReadOnlyDictionary<string, string> TableNameMappings
    {
        get => this._tableNameMappings;
        set
        {
            Guard.ThrowIfNull(value);

            var copy = new Dictionary<string, string>(value.Count);

            foreach (var entry in value)
            {
                if (entry.Value is null)
                {
                    throw new ArgumentNullException(entry.Key, $"{nameof(this.TableNameMappings)} must not contain null values.");
                }

                copy[entry.Key] = entry.Value;
            }

            this._tableNameMappings = copy;
        }
    }

    public IReadOnlyDictionary<string, object> PrepopulatedFields
    {
        get => this._fields;
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

            this._fields = copy;
        }
    }
}
