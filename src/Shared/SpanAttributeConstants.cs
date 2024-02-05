// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Trace;

/// <summary>
/// Defines well-known span attribute keys.
/// </summary>
internal static class SpanAttributeConstants
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public const string StatusCodeKey = "otel.status_code";
    public const string StatusDescriptionKey = "otel.status_description";

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
