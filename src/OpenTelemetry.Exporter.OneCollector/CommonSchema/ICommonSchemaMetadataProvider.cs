// <copyright file="ICommonSchemaMetadataProvider.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.OneCollector;

/// <summary>
/// Describes a service which will provide the Common Schema field metadata for
/// telemetry being emitted.
/// </summary>
public interface ICommonSchemaMetadataProvider
{
    /// <summary>
    /// Gets the number of Common Schema fields with metadata.
    /// </summary>
    int CommonSchemaMetadataFieldCount { get; }

    /// <summary>
    /// Gets the <see cref="CommonSchemaMetadataFieldDefinition"/> for a given index.
    /// </summary>
    /// <param name="index">Index being requested.</param>
    /// <param name="commonSchemaMetadataFieldDefinition"><see cref="CommonSchemaMetadataFieldDefinition"/>.</param>
    void GetCommonSchemaMetadataFieldDefinition(
        int index,
        out CommonSchemaMetadataFieldDefinition commonSchemaMetadataFieldDefinition);
}
