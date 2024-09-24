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
        return AddProcessDetector(builder, includeProcessOwner: true);
    }

    /// <summary>
    /// Enables process resource detector.
    /// </summary>
    /// <param name="builder">The <see cref="ResourceBuilder"/> being configured.</param>
    /// <param name="includeProcessOwner">A flag indicating whether or not to include the process owner in the resource.</param>
    /// <returns>The instance of <see cref="ResourceBuilder"/> being configured.</returns>
    public static ResourceBuilder AddProcessDetector(this ResourceBuilder builder, bool includeProcessOwner)
    {
        Guard.ThrowIfNull(builder);
        return builder.AddDetector(new ProcessDetector(includeProcessOwner));
    }
}
