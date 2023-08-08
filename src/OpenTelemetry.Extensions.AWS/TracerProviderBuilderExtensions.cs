// <copyright file="TracerProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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

#if NET6_0_OR_GREATER
    /// <summary>
    /// Replace the trace id of root activity.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="sampler">Unused. (See deprecation message.)</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/>.</returns>
    [System.Obsolete($"When targeting .NET 6.0 or later, the X-Ray ID generator does not need to update the sampling decision. Use ${nameof(AddXRayTraceId)} instead.")]
#else
    /// <summary>
    /// 1. Replace the trace id of root activity.
    /// 2. Update the sampling decision for root activity when it's created through ActivitySource.StartActivity().
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="sampler"><see cref="Sampler"/> being used.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/>.</returns>
#endif
    public static TracerProviderBuilder AddXRayTraceIdWithSampler(this TracerProviderBuilder builder, Sampler sampler)
    {
        Guard.ThrowIfNull(builder);

        AWSXRayIdGenerator.ReplaceTraceId(sampler);
        return builder;
    }
}
