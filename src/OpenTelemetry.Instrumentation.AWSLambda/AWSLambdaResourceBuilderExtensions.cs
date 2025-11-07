// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.AWS;
using OpenTelemetry.Instrumentation.AWSLambda.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Instrumentation.AWSLambda;

/// <summary>
/// Extension methods to simplify registering of AWS Lambda resource detectors.
/// </summary>
public static class AWSLambdaResourceBuilderExtensions
{
    /// <summary>
    /// Enables AWS Lambda resource detector.
    /// </summary>
    /// <param name="builder">The <see cref="ResourceBuilder"/> being configured.</param>
    /// <param name="configure">Optional callback action for configuring <see cref="AWSLambdaInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="ResourceBuilder"/> being configured.</returns>
    public static ResourceBuilder AddAWSLambdaDetector(this ResourceBuilder builder, Action<AWSLambdaInstrumentationOptions>? configure = null)
    {
        Guard.ThrowIfNull(builder);

        var options = new AWSLambdaInstrumentationOptions();
        configure?.Invoke(options);

        var semanticConventionBuilder = new AWSSemanticConventions(options.SemanticConventionVersion);

        return builder.AddDetector(new AWSLambdaResourceDetector(semanticConventionBuilder));
    }
}
