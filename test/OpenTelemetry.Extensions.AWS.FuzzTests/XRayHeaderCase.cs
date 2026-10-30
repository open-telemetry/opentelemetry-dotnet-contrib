// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Extensions.AWS.FuzzTests;

/// <summary>
/// An X-Ray trace header and, when the header is known to be well formed, the context it must be read as.
/// </summary>
public sealed class XRayHeaderCase(string header, string? traceId, string? spanId, bool sampled)
{
    public string Header { get; } = header;

    public string? TraceId { get; } = traceId;

    public string? SpanId { get; } = spanId;

    public bool Sampled { get; } = sampled;

    public override string ToString() => $"Header: \"{this.Header}\"";
}
