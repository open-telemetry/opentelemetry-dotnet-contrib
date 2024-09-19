// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Resources.Host;

namespace OpenTelemetry.Resources;

/// <summary>
/// Extension methods to simplify registering of host resource detectors.
/// </summary>
public static class HostResourceBuilderExtensions
{
    /// <summary>
    /// Enables host resource detector.
    /// </summary>
    /// <param name="builder">The <see cref="ResourceBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="ResourceBuilder"/> being configured.</returns>
    public static ResourceBuilder AddHostDetector(this ResourceBuilder builder)
    {
        return AddHostDetector(builder, includeHostName: true);
    }

    /// <summary>
    /// Enables host resource detector.
    /// </summary>
    /// <param name="builder">The <see cref="ResourceBuilder"/> being configured.</param>
    /// <param name="includeHostName">Determines whether to include host name in the resource attributes.</param>
    /// <returns>The instance of <see cref="ResourceBuilder"/> being configured.</returns>
    public static ResourceBuilder AddHostDetector(this ResourceBuilder builder, bool includeHostName)
    {
        Guard.ThrowIfNull(builder);
        return builder.AddDetector(new HostDetector(includeHostName));
    }
}
