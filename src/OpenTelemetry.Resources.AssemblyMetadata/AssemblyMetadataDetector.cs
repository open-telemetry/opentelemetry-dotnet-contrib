// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace OpenTelemetry.Resources.AssemblyMetadata;

/// <summary>
/// Detects resource attributes applied to an <see cref="Assembly"/> at build-time using <see cref="AssemblyMetadataAttribute"/> with a <see cref="AssemblyMetadataAttribute.Key"/> prefixed by <c>otel:</c>.
/// </summary>
internal sealed class AssemblyMetadataDetector(Assembly? assembly) : IResourceDetector
{
    /// <inheritdoc />
    public Resource Detect()
    {
        if (assembly is null)
        {
            return Resource.Empty;
        }

        List<KeyValuePair<string, object>>? attrs = null;

        foreach (var attr in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
#if NET
            if (attr is not { Key: ['o', 't', 'e', 'l', ':', .. [_, ..] k], Value: [..] v })
#else
            if (attr is not { Key: { Length: > 5 } k, Value: { } v } ||
                !k.StartsWith("otel:", StringComparison.Ordinal) ||
                (k = k.Substring(5)) is "")
#endif
            {
                continue;
            }

            attrs ??= [];
            attrs.Add(new KeyValuePair<string, object>(k, v));
        }

        return attrs switch
        {
            null => Resource.Empty,
            _ => new Resource(attrs),
        };
    }
}
