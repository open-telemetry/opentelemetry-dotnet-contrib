// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Resources.Azure;

namespace OpenTelemetry.Resources;

/// <summary>
/// Extension methods to simplify registering of Azure resource detectors.
/// </summary>
public static class AzureResourceBuilderExtensions
{
    /// <summary>
    /// Enables Azure AppService resource detector.
    /// </summary>
    /// <param name="builder"><see cref="ResourceBuilder" /> being configured.</param>
    /// <returns>The instance of <see cref="ResourceBuilder" /> being configured.</returns>
    public static ResourceBuilder AddAppServiceDetector(this ResourceBuilder builder)
    {
        Guard.ThrowIfNull(builder);
        return builder.AddDetector(new AppServiceResourceDetector());
    }

    /// <summary>
    /// Enables Azure VM resource detector.
    /// </summary>
    /// <param name="builder"><see cref="ResourceBuilder" /> being configured.</param>
    /// <returns>The instance of <see cref="ResourceBuilder" /> being configured.</returns>
    public static ResourceBuilder AddAzureVMDetector(this ResourceBuilder builder)
    {
        Guard.ThrowIfNull(builder);
        return builder.AddDetector(new AzureVMResourceDetector());
    }

    /// <summary>
    /// Enables Azure Container Apps resource detector.
    /// </summary>
    /// <param name="builder"><see cref="ResourceBuilder" /> being configured.</param>
    /// <returns>The instance of <see cref="ResourceBuilder" /> being configured.</returns>
    public static ResourceBuilder AddAzureContainerAppsDetector(this ResourceBuilder builder)
    {
        Guard.ThrowIfNull(builder);
        return builder.AddDetector(new AzureContainerAppsResourceDetector());
    }
}
