// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Instana.Implementation;

internal class InstanaSpanTransformInfo
{
    public string? StatusCode { get; internal set; }

    public string? StatusDesc { get; internal set; }

    public bool HasExceptionEvent { get; internal set; }

    public bool IsEntrySpan { get; internal set; }
}
