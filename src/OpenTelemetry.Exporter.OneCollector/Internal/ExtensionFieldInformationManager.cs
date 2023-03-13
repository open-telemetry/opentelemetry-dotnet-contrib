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

namespace OpenTelemetry.Exporter.OneCollector;

internal sealed class ExtensionFieldInformationManager
{
    public const int MaxNumberOfCachedFieldInformations = 2048;
    private readonly Hashtable fieldInformationCache = new(16, StringComparer.OrdinalIgnoreCase);

    public static ExtensionFieldInformationManager SharedCache { get; } = new();

    public int CountOfCachedExtensionFields => this.fieldInformationCache.Count;

    public bool TryResolveExtensionFieldInformation(string fullFieldName, out (string ExtensionName, string FieldName) resolvedFieldInformation)
    {
        if (this.fieldInformationCache[fullFieldName] is not FieldInformation fieldInformation)
        {
            fieldInformation = this.ResolveExtensionFieldInformationRare(fullFieldName);
        }

        if (!fieldInformation.IsValid)
        {
            resolvedFieldInformation = default;
            return false;
        }

        resolvedFieldInformation = new(fieldInformation.ExtensionName!, fieldInformation.FieldName!);
        return true;
    }

    private static FieldInformation BuildFieldInformation(string fullFieldName)
    {
        Debug.Assert(fullFieldName.Length >= 4, "fullFieldName length was invalid");
        Debug.Assert(fullFieldName.StartsWith("ext.", StringComparison.OrdinalIgnoreCase), "fullFieldName did not start with 'ext.'");

        var extensionName = fullFieldName.AsSpan().Slice(4);
        var locationOfDot = extensionName.IndexOf('.');
        if (locationOfDot <= 0)
        {
            return new();
        }

        var fieldName = extensionName.Slice(locationOfDot + 1);
        if (fieldName.Length <= 0)
        {
            return new();
        }

        extensionName = extensionName.Slice(0, locationOfDot);

        return new FieldInformation
        {
            ExtensionName = extensionName.ToString(),
            FieldName = fieldName.ToString(),
            IsValid = true,
        };
    }

    private FieldInformation ResolveExtensionFieldInformationRare(string fullFieldName)
    {
        if (this.fieldInformationCache.Count >= MaxNumberOfCachedFieldInformations)
        {
            return BuildFieldInformation(fullFieldName);
        }

        lock (this.fieldInformationCache)
        {
            if (this.fieldInformationCache[fullFieldName] is not FieldInformation fieldInformation)
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

    private sealed class FieldInformation
    {
        public string? ExtensionName;
        public string? FieldName;
        public bool IsValid;
    }
}
