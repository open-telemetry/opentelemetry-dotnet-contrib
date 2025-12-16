// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.AWS;

namespace OpenTelemetry.Instrumentation.AWSLambda;

/// <summary>
/// AWS lambda resource builder options.
/// </summary>
public class AWSLambdaResourceBuilderOptions
{
    /// <inheritdoc cref="AWSLambda.SemanticConventionVersion"/>
    public SemanticConventionVersion SemanticConventionVersion { get; set; } = AWSSemanticConventions.DefaultSemanticConventionVersion;
}
