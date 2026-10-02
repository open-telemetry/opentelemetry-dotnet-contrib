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
        => AddHostDetector(builder, configure: null);

    /// <summary>
    /// Enables host resource detector.
    /// </summary>
    /// <param name="builder">The <see cref="ResourceBuilder"/> being configured.</param>
    /// <param name="configure">Callback to configure <see cref="HostDetectorOptions"/>.
    /// Values set here override the ones read from environment variables.</param>
    /// <returns>The instance of <see cref="ResourceBuilder"/> being configured.</returns>
    public static ResourceBuilder AddHostDetector(this ResourceBuilder builder, Action<HostDetectorOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        var options = new HostDetectorOptions();
        configure?.Invoke(options);

        return builder.AddDetector(new HostDetector(options));
    }
}
