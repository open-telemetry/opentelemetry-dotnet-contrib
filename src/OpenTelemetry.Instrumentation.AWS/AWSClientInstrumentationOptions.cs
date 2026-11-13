// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.AWS;

namespace OpenTelemetry.Instrumentation.AWS;

/// <summary>
/// Options for AWS client instrumentation.
/// </summary>
public class AWSClientInstrumentationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether downstream instrumentation is suppressed.
    /// </summary>
    public bool SuppressDownstreamInstrumentation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the exception of a failed AWS SDK call
    /// will be recorded as an ActivityEvent and as span attributes or not.
    /// </summary>
    /// <remarks>
    /// <para>The default value is <see langword="false"/>.</para>
    /// <para>Exception messages returned by AWS services may contain values
    /// from the request, so they are only recorded when this option is enabled.</para>
    /// https://github.com/open-telemetry/semantic-conventions/blob/main/docs/exceptions/exceptions-spans.md.
    /// </remarks>
    public bool RecordException { get; set; }

    /// <inheritdoc cref="AWS.SemanticConventionVersion"/>
    public SemanticConventionVersion SemanticConventionVersion { get; set; } = AWSSemanticConventions.DefaultSemanticConventionVersion;
}
