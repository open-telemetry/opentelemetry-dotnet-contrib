// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources.AssemblyMetadata;

namespace OpenTelemetry.Resources;

/// <summary>
/// Extension methods to simplify registering the assembly metadata resource detector.
/// </summary>
public static class AssemblyMetadataResourceBuilderExtensions
{
    /// <summary>
    /// Enables the assembly metadata resource detector on the <see cref="Assembly"/> returned by <see cref="Assembly.GetEntryAssembly()"/>.
    /// </summary>
    /// <param name="builder">The <see cref="ResourceBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="ResourceBuilder"/> being configured.</returns>
    public static ResourceBuilder AddAssemblyMetadataDetector(this ResourceBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        return AddAssemblyMetadataDetector(builder, Assembly.GetEntryAssembly());
    }

    /// <summary>
    /// Enables the assembly metadata resource detector on <paramref name="assembly"/>.
    /// </summary>
    /// <param name="builder">The <see cref="ResourceBuilder"/> being configured.</param>
    /// <param name="assembly">The <see cref="Assembly"/> from which to detect resource attributes.</param>
    /// <returns>The instance of <see cref="ResourceBuilder"/> being configured.</returns>
    public static ResourceBuilder AddAssemblyMetadataDetector(this ResourceBuilder builder, Assembly? assembly)
    {
        Guard.ThrowIfNull(builder);

        return builder.AddDetector(new AssemblyMetadataDetector(assembly));
    }
}
