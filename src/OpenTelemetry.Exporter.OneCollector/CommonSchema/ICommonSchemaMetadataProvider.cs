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
    /// Defines the callback contract used by <see
    /// cref="ForEachCommonSchemaMetadataFieldDefinition"/>.
    /// </summary>
    /// <typeparam name="TState">State type.</typeparam>
    /// <param name="commonSchemaMetadataFieldDefinition"><see
    /// cref="CommonSchemaMetadataFieldDefinition"/>.</param>
    /// <param name="state">State.</param>
    public delegate void CommonSchemaMetadataFieldDefinitionForEachAction<TState>(
        in CommonSchemaMetadataFieldDefinition commonSchemaMetadataFieldDefinition,
        ref TState state);

    /// <summary>
    /// Executes the supplied callback for each <see
    /// cref="CommonSchemaMetadataFieldDefinition"/> contained in the provider.
    /// </summary>
    /// <remarks>Note to implementors: All callbacks should be called inline
    /// from this method.</remarks>
    /// <typeparam name="TState">State type.</typeparam>
    /// <param name="callback">The callback to be executed for every <see
    /// cref="CommonSchemaMetadataFieldDefinition"/>.</param>
    /// <param name="state">The state instance to be passed into the
    /// callback.</param>
    void ForEachCommonSchemaMetadataFieldDefinition<TState>(
        CommonSchemaMetadataFieldDefinitionForEachAction<TState> callback,
        ref TState state);
}
