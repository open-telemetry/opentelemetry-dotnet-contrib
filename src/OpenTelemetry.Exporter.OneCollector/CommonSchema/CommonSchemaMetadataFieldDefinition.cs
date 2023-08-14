// <copyright file="CommonSchemaMetadataFieldDefinition.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OneCollector;

/// <summary>
/// Stores metadata details about a Common Schema field.
/// </summary>
public readonly record struct CommonSchemaMetadataFieldDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommonSchemaMetadataFieldDefinition"/> class.
    /// </summary>
    /// <param name="name">Field name.</param>
    public CommonSchemaMetadataFieldDefinition(string name)
        : this(0UL, name)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommonSchemaMetadataFieldDefinition"/> class.
    /// </summary>
    /// <param name="typeInfo">Type info.</param>
    /// <param name="name">Field name.</param>
    public CommonSchemaMetadataFieldDefinition(ulong typeInfo, string name)
    {
        Guard.ThrowIfNullOrWhitespace(name);

        this.TypeInfo = typeInfo;
        this.Name = name;
    }

    /// <summary>
    /// Gets the field type info.
    /// </summary>
    public ulong TypeInfo { get; }

    /// <summary>
    /// Gets the field name.
    /// </summary>
    public string Name { get; }
}
