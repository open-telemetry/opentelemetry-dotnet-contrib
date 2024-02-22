// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Trace;

/// <summary>
/// A collection of helper methods to be used when building spans.
/// </summary>
internal static class SpanHelper
{
    /// <summary>
    /// Helper method that populates span properties from http status code according
    /// to https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/http.md#status.
    /// </summary>
    /// <param name="httpStatusCode">Http status code.</param>
    /// <returns>Resolved span <see cref="Status"/> for the Http status code.</returns>
    public static Status ResolveSpanStatusForHttpStatusCode(int httpStatusCode)
    {
        if (httpStatusCode >= 100 && httpStatusCode <= 399)
        {
            return Status.Unset;
        }

        if (httpStatusCode == 404)
        {
            return Status.Unset;
        }

        return Status.Error;
    }

    /// <summary>
    /// Helper method that populates span properties from http status code according
    /// to https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/http.md#status.
    /// </summary>
    /// <param name="kind">The span kind.</param>
    /// <param name="httpStatusCode">Http status code.</param>
    /// <returns>Resolved span <see cref="Status"/> for the Http status code.</returns>
    public static Status ResolveSpanStatusForHttpStatusCode(ActivityKind kind, int httpStatusCode)
    {
        var lowerBound = kind == ActivityKind.Client ? 400 : 500;
        var upperBound = 599;
        if (httpStatusCode >= lowerBound && httpStatusCode <= upperBound)
        {
            return Status.Error;
        }

        return Status.Unset;
    }
}
