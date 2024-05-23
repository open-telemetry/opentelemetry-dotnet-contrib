// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Resources.AWS;

namespace OpenTelemetry.Resources;

/// <summary>
/// Extension methods to simplify registering of AWS resource detectors.
/// </summary>
public static class AWSResourceBuilderExtensions
{
    /// <summary>
    /// Enables AWS Elastic Beanstalk resource detector.
    /// </summary>
    /// <param name="builder"><see cref="ResourceBuilder" /> being configured.</param>
    /// <returns>The instance of <see cref="ResourceBuilder" /> being configured.</returns>
    public static ResourceBuilder AddAWSEBSResourceDetector(this ResourceBuilder builder)
    {
        Guard.ThrowIfNull(builder);
        return builder.AddDetector(new AWSEBSResourceDetector());
    }

    /// <summary>
    /// Enables AWS EC2 resource detector.
    /// </summary>
    /// <param name="builder"><see cref="ResourceBuilder" /> being configured.</param>
    /// <returns>The instance of <see cref="ResourceBuilder" /> being configured.</returns>
    public static ResourceBuilder AddAWSEC2ResourceDetector(this ResourceBuilder builder)
    {
        Guard.ThrowIfNull(builder);
        return builder.AddDetector(new AWSEC2ResourceDetector());
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Enables AWS ECS resource detector.
    /// </summary>
    /// <param name="builder"><see cref="ResourceBuilder" /> being configured.</param>
    /// <returns>The instance of <see cref="ResourceBuilder" /> being configured.</returns>
    public static ResourceBuilder AddAWSECSResourceDetector(this ResourceBuilder builder)
    {
        Guard.ThrowIfNull(builder);
        return builder.AddDetector(new AWSECSResourceDetector());
    }

    /// <summary>
    /// Enables AWS EKS resource detector.
    /// </summary>
    /// <param name="builder"><see cref="ResourceBuilder" /> being configured.</param>
    /// <returns>The instance of <see cref="ResourceBuilder" /> being configured.</returns>
    public static ResourceBuilder AddAWSEKSResourceDetector(this ResourceBuilder builder)
    {
        Guard.ThrowIfNull(builder);
        return builder.AddDetector(new AWSEKSResourceDetector());
    }
#endif
}
