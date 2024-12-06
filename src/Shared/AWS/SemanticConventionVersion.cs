// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.AWS;

#pragma warning disable SA1300
#pragma warning disable CA1707

/// <summary>
/// <para>
/// Collection of the Open Telemetry Semantic Conventions supported by
/// the OpenTelemetry.*.AWS libraries.  Can be used to pin the version
/// of Semantic Convention emitted.
/// </para>
/// <para>
/// While these libraries are intended for production use, they rely on several
/// Semantic Conventions that are still considered Experimental, meaning they
/// may undergo additional changes before becoming Stable.  This can  impact
/// the aggregation and analysis of telemetry signals in environments with
/// multiple applications or microservices. For example, a microservice using
/// an older version of the Semantic Conventions for Http Attributes may emit
/// <c>"http.method"</c> with a value of GET, while a different microservice,
/// using a new version of Semantic Convention may instead emit the GET as
/// <c>"http.request.method"</c>.
/// </para>
/// <para>
/// Future versions the OpenTelemetry.*.AWS libraries will include updates
/// to the Semantic Convention, which may break compatibility with a previous
/// version.
/// <para>
/// To opt-out of automatic upgrades, you can pin to a specific version:
/// </para>
/// <code>
/// <![CDATA[
///  using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
///     .AddAWSLambdaConfigurations(opt =>
///     {
///         opt.SemanticConventionVersion = SemanticConventionVersion.v1_27_0_Experimental;
///     })
///     .Build()!);
/// ]]>
/// </code>
/// </para>
/// <para>
/// For additional details, see:
/// https://opentelemetry.io/docs/specs/otel/versioning-and-stability/.
/// </para>
/// </summary>
/// <remarks>
/// Once a Semantic Convention becomes Stable, OpenTelemetry.*.AWS libraries
/// will remain on that version until the next major version bump.
/// </remarks>
public enum SemanticConventionVersion
{
    /// <summary>
    /// Use Experimental Conventions until they become stable and then
    /// pin to stable.
    /// </summary>
    Latest = 0,

    /// <summary>
    /// Pin to the specific state of all Semantic Conventions as of the 1.27.0
    /// release. See:
    /// https://github.com/open-telemetry/semantic-conventions/releases/tag/v1.27.0.
    /// </summary>
    v1_27_0_Experimental = 1,

    /// <summary>
    /// Pin to the specific state of all Semantic Conventions as of the 1.29.0
    /// release. See:
    /// https://github.com/open-telemetry/semantic-conventions/releases/tag/v1.29.0.
    /// </summary>
    v1_29_0_Experimental = 2,
}
