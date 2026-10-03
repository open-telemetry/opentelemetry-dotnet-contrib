// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods for <see cref="MeterProviderBuilder"/> to configure from <see cref="IConfigurationSection"/>.
/// </summary>
public static class MeterProviderBuilderConfigurationExtensions
{
    /// <summary>
    /// Adds Meter names to the <see cref="MeterProviderBuilder"/> from the provided <see cref="IConfigurationSection"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="section"><see cref="IConfigurationSection"/> containing the names.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddMetersFromConfiguration(
        this MeterProviderBuilder builder,
        IConfigurationSection section)
    {
        Guard.ThrowIfNull(builder);
        Guard.ThrowIfNull(section);

        foreach (var child in section.GetChildren())
        {
            if (string.IsNullOrWhiteSpace(child.Value))
            {
                throw new ArgumentException($"Configuration section '{child.Path}' contains a null, empty or whitespace-only meter name.");
            }

            builder.AddMeter(child.Value!);
        }

        return builder;
    }
}
