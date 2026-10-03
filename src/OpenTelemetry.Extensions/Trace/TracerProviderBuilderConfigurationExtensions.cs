// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods for <see cref="TracerProviderBuilder"/> to configure from <see cref="IConfigurationSection"/>.
/// </summary>
public static class TracerProviderBuilderConfigurationExtensions
{
    /// <summary>
    /// Adds ActivitySource names to the <see cref="TracerProviderBuilder"/> from the provided <see cref="IConfigurationSection"/>.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="section"><see cref="IConfigurationSection"/> containing the names.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddSourcesFromConfiguration(
        this TracerProviderBuilder builder,
        IConfigurationSection section)
    {
        Guard.ThrowIfNull(builder);
        Guard.ThrowIfNull(section);

        foreach (var child in section.GetChildren())
        {
            if (string.IsNullOrWhiteSpace(child.Value))
            {
                throw new ArgumentException($"Configuration section '{child.Path}' contains a null, empty or whitespace-only source name.");
            }

            builder.AddSource(child.Value!);
        }

        return builder;
    }
}
