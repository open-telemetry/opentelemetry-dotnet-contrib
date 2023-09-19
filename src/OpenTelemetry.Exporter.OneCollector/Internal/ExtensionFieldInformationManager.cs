// <copyright file="ExtensionFieldInformationManager.cs" company="OpenTelemetry Authors">
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

using System.Collections;
using System.Diagnostics;
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Text.Json;

namespace OpenTelemetry.Exporter.OneCollector;

internal sealed class ExtensionFieldInformationManager
{
    public const int MaxNumberOfCachedFieldInformations = 2048;
    private readonly Hashtable fieldInformationCache = new(16, StringComparer.OrdinalIgnoreCase);

    public static ExtensionFieldInformationManager SharedCache { get; } = new();

    public int CountOfCachedExtensionFields => this.fieldInformationCache.Count;

    public bool TryResolveExtensionFieldInformation(
        string fullFieldName,
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
        [NotNullWhen(true)]
#endif
        out ExtensionFieldInformation? resolvedFieldInformation)
    {
        if (this.fieldInformationCache[fullFieldName] is not ExtensionFieldInformation fieldInformation)
        {
            fieldInformation = this.ResolveExtensionFieldInformationRare(fullFieldName);
        }

        if (!fieldInformation.IsValid)
        {
            resolvedFieldInformation = default;
            return false;
        }

        resolvedFieldInformation = fieldInformation;
        return true;
    }

    private static ExtensionFieldInformation BuildFieldInformation(string fullFieldName)
    {
        Debug.Assert(fullFieldName.Length >= 4, "fullFieldName length was invalid");
        Debug.Assert(fullFieldName.StartsWith("ext.", StringComparison.OrdinalIgnoreCase), "fullFieldName did not start with 'ext.'");

        var extensionName = fullFieldName.AsSpan().Slice(4).TrimEnd();
        var locationOfDot = extensionName.IndexOf('.');
        if (locationOfDot <= 0)
        {
            return new();
        }

        var fieldName = extensionName.Slice(locationOfDot + 1).TrimStart();
        if (fieldName.Length <= 0)
        {
            return new();
        }

        extensionName = extensionName.Slice(0, locationOfDot).TrimEnd();
        if (extensionName.Length <= 0)
        {
            return new();
        }

        return new ExtensionFieldInformation
        {
            ExtensionName = extensionName.ToString(),
            EncodedExtensionName = JsonEncodedText.Encode(extensionName),
            FieldName = fieldName.ToString(),
            EncodedFieldName = JsonEncodedText.Encode(fieldName),
        };
    }

    private ExtensionFieldInformation ResolveExtensionFieldInformationRare(string fullFieldName)
    {
        if (this.fieldInformationCache.Count >= MaxNumberOfCachedFieldInformations)
        {
            return BuildFieldInformation(fullFieldName);
        }

        lock (this.fieldInformationCache)
        {
            if (this.fieldInformationCache[fullFieldName] is not ExtensionFieldInformation fieldInformation)
            {
                fieldInformation = BuildFieldInformation(fullFieldName);
                if (this.fieldInformationCache.Count < MaxNumberOfCachedFieldInformations)
                {
                    this.fieldInformationCache[fullFieldName] = fieldInformation;
                }
            }

            return fieldInformation;
        }
    }
}
