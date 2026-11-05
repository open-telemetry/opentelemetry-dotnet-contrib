// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Extensions.AWS;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension method to generate AWS X-Ray compatible trace id and replace the trace id of root activity.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Replace the trace id of root activity.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/>.</returns>
    public static TracerProviderBuilder AddXRayTraceId(this TracerProviderBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        AWSXRayIdGenerator.ReplaceTraceId();
        return builder;
    }

    /// <summary>
    /// Replace the trace id of root activity.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="sampler">Unused. (See deprecation message.)</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/>.</returns>
    [Obsolete($"The X-Ray ID generator does not need to update the sampling decision. Use {nameof(AddXRayTraceId)} instead.")]
    public static TracerProviderBuilder AddXRayTraceIdWithSampler(this TracerProviderBuilder builder, Sampler sampler)
    {
        Guard.ThrowIfNull(builder);

        AWSXRayIdGenerator.ReplaceTraceId(sampler);
        return builder;
    }
}
