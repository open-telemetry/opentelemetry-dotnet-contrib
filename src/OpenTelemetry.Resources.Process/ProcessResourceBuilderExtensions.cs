// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Resources.Process;

namespace OpenTelemetry.Resources;

/// <summary>
/// Extension methods to simplify registering of process resource detectors.
/// </summary>
public static class ProcessResourceBuilderExtensions
{
    /// <summary>
    /// Enables process resource detector.
    /// </summary>
    /// <param name="builder">The <see cref="ResourceBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="ResourceBuilder"/> being configured.</returns>
    public static ResourceBuilder AddProcessDetector(this ResourceBuilder builder)
    {
        return builder.AddProcessDetector(new ProcessDetectorOptions());
    }

    /// <summary>
    /// Enables process resource detector.
    /// </summary>
    /// <param name="builder">The <see cref="ResourceBuilder"/> being configured.</param>
    /// <param name="options">The <see cref="ProcessDetectorOptions"/> used to control the behaviour configured.</param>
    /// <returns>The instance of <see cref="ResourceBuilder"/> being configured.</returns>
    public static ResourceBuilder AddProcessDetector(this ResourceBuilder builder, ProcessDetectorOptions options)
    {
        Guard.ThrowIfNull(builder);
        return builder.AddDetector(new ProcessDetector(options));
    }
}
