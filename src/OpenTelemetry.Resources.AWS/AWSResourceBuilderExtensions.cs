// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.AWS;
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
    /// <param name="builder">The <see cref="ResourceBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="ResourceBuilder"/> being configured.</returns>
    public static ResourceBuilder AddAWSEBSDetector(this ResourceBuilder builder, Action<AWSResourceBuilderOptions>? configure = null)
    {
        Guard.ThrowIfNull(builder);

        var options = new AWSResourceBuilderOptions();
        configure?.Invoke(options);

        AWSSemanticConventions.SemanticConventionVersion = options.SemanticConventionVersion;

        return builder.AddDetector(new AWSEBSDetector());
    }

    /// <summary>
    /// Enables AWS EC2 resource detector.
    /// </summary>
    /// <param name="builder">The <see cref="ResourceBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="ResourceBuilder"/> being configured.</returns>
    public static ResourceBuilder AddAWSEC2Detector(this ResourceBuilder builder, Action<AWSResourceBuilderOptions>? configure = null)
    {
        Guard.ThrowIfNull(builder);

        var options = new AWSResourceBuilderOptions();
        configure?.Invoke(options);

        AWSSemanticConventions.SemanticConventionVersion = options.SemanticConventionVersion;

        return builder.AddDetector(new AWSEC2Detector());
    }

#if NET
    /// <summary>
    /// Enables AWS ECS resource detector.
    /// </summary>
    /// <param name="builder">The <see cref="ResourceBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="ResourceBuilder"/> being configured.</returns>
    public static ResourceBuilder AddAWSECSDetector(this ResourceBuilder builder, Action<AWSResourceBuilderOptions>? configure = null)
    {
        Guard.ThrowIfNull(builder);

        var options = new AWSResourceBuilderOptions();
        configure?.Invoke(options);

        AWSSemanticConventions.SemanticConventionVersion = options.SemanticConventionVersion;

        return builder.AddDetector(new AWSECSDetector());
    }

    /// <summary>
    /// Enables AWS EKS resource detector.
    /// </summary>
    /// <param name="builder">The <see cref="ResourceBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="ResourceBuilder"/> being configured.</returns>
    public static ResourceBuilder AddAWSEKSDetector(this ResourceBuilder builder, Action<AWSResourceBuilderOptions>? configure = null)
    {
        Guard.ThrowIfNull(builder);

        var options = new AWSResourceBuilderOptions();
        configure?.Invoke(options);

        AWSSemanticConventions.SemanticConventionVersion = options.SemanticConventionVersion;

        return builder.AddDetector(new AWSEKSDetector());
    }
#endif
}
