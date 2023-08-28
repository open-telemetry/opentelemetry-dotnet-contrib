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
    [ThreadStatic]
    private static ExtensionFieldInformationCacheKey? threadCacheKey;
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
        var cacheKey = BuildCacheKey(
            fullFieldName,
            extensionName: null,
            fieldName: null);

        return this.TryResolveExtensionFieldInformation(cacheKey, out resolvedFieldInformation);
    }

    public bool TryResolveExtensionFieldInformation(
        string extensionName,
        string fieldName,
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
        [NotNullWhen(true)]
#endif
        out ExtensionFieldInformation? resolvedFieldInformation)
    {
        var cacheKey = BuildCacheKey(
            fullFieldName: null,
            extensionName,
            fieldName);

        return this.TryResolveExtensionFieldInformation(cacheKey, out resolvedFieldInformation);
    }

    private static ExtensionFieldInformationCacheKey BuildCacheKey(
        string? fullFieldName,
        string? extensionName,
        string? fieldName)
    {
        var cacheKey = threadCacheKey ??= new();
        cacheKey.FullFieldName = fullFieldName;
        cacheKey.ExtensionName = extensionName;
        cacheKey.FieldName = fieldName;
        return cacheKey;
    }

    private bool TryResolveExtensionFieldInformation(
        ExtensionFieldInformationCacheKey cacheKey,
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
        [NotNullWhen(true)]
#endif
        out ExtensionFieldInformation? resolvedFieldInformation)
    {
        if (this.fieldInformationCache[cacheKey] is not ExtensionFieldInformation fieldInformation)
        {
            fieldInformation = this.ResolveExtensionFieldInformationRare(cacheKey);
        }

        if (!fieldInformation.IsValid)
        {
            resolvedFieldInformation = default;
            return false;
        }

        resolvedFieldInformation = fieldInformation;
        return true;
    }

    private ExtensionFieldInformation ResolveExtensionFieldInformationRare(ExtensionFieldInformationCacheKey cacheKey)
    {
        if (this.fieldInformationCache.Count >= MaxNumberOfCachedFieldInformations)
        {
            return cacheKey.ToFieldInformation();
        }

        lock (this.fieldInformationCache)
        {
            if (this.fieldInformationCache[cacheKey] is not ExtensionFieldInformation fieldInformation)
            {
                fieldInformation = cacheKey.ToFieldInformation();
                if (this.fieldInformationCache.Count < MaxNumberOfCachedFieldInformations)
                {
                    // Note: We make a copy of the [ThreadStatic] key here so it
                    // remains immutable in the hastable.
                    this.fieldInformationCache[cacheKey.Clone()] = fieldInformation;
                }
            }

            return fieldInformation;
        }
    }

    internal sealed class ExtensionFieldInformationCacheKey : IEquatable<ExtensionFieldInformationCacheKey>
    {
        public string? FullFieldName;

        public string? ExtensionName;

        public string? FieldName;

        public static bool operator ==(ExtensionFieldInformationCacheKey? left, ExtensionFieldInformationCacheKey? right)
        {
            if (left is null)
            {
                return right is null;
            }

            return left.Equals(right);
        }

        public static bool operator !=(ExtensionFieldInformationCacheKey? left, ExtensionFieldInformationCacheKey? right)
            => !(left == right);

        public ExtensionFieldInformationCacheKey Clone()
        {
            return new()
            {
                FullFieldName = this.FullFieldName,
                ExtensionName = this.ExtensionName,
                FieldName = this.FieldName,
            };
        }

        public override int GetHashCode()
        {
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
            HashCode hash = default;
            hash.Add(this.FullFieldName, StringComparer.OrdinalIgnoreCase);
            hash.Add(this.ExtensionName, StringComparer.OrdinalIgnoreCase);
            hash.Add(this.FieldName, StringComparer.OrdinalIgnoreCase);
            return hash.ToHashCode();
#else
            var hash = 17;
            unchecked
            {
                hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(this.FullFieldName ?? string.Empty);
                hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(this.ExtensionName ?? string.Empty);
                hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(this.FieldName ?? string.Empty);
            }

            return hash;
#endif
        }

        public bool Equals(ExtensionFieldInformationCacheKey? other)
        {
            if (other is null)
            {
                return false;
            }

            return string.Equals(this.FullFieldName, other.FullFieldName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(this.ExtensionName, other.ExtensionName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(this.FieldName, other.FieldName, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj)
            => this.Equals(obj as ExtensionFieldInformationCacheKey);

        public ExtensionFieldInformation ToFieldInformation()
        {
            if (this.FullFieldName != null)
            {
                return BuildFieldInformationFromFullFieldName(this.FullFieldName);
            }
            else
            {
                Debug.Assert(this.ExtensionName != null, "ExtensionName was null");
                Debug.Assert(this.FieldName != null, "FieldName was null");

                var fieldName = this.FieldName!.Trim();
                if (fieldName.Length <= 0)
                {
                    return new();
                }

                return new ExtensionFieldInformation
                {
                    ExtensionName = this.ExtensionName,
                    EncodedExtensionName = JsonEncodedText.Encode(this.ExtensionName!),
                    FieldName = fieldName,
                    EncodedFieldName = JsonEncodedText.Encode(fieldName),
                };
            }
        }

        private static ExtensionFieldInformation BuildFieldInformationFromFullFieldName(string fullFieldName)
        {
            Debug.Assert(fullFieldName.Length >= 4, "fullFieldName length was invalid");
            Debug.Assert(fullFieldName.StartsWith("ext.", StringComparison.OrdinalIgnoreCase), "fullFieldName did not start with 'ext.'");

            var extensionName = fullFieldName.AsSpan().Slice(4).Trim();
            var locationOfDot = extensionName.IndexOf('.');
            if (locationOfDot < 0)
            {
                if (extensionName.Length <= 0)
                {
                    return new();
                }

                return new ExtensionFieldInformation
                {
                    ExtensionName = extensionName.ToString(),
                    EncodedExtensionName = JsonEncodedText.Encode(extensionName),
                };
            }

            var fieldName = extensionName.Slice(locationOfDot + 1).Trim();
            if (fieldName.Length <= 0)
            {
                return new();
            }

            extensionName = extensionName.Slice(0, locationOfDot).Trim();
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
    }
}
