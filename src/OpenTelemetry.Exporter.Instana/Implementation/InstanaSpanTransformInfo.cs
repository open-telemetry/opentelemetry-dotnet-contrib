// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Instana.Implementation;

internal sealed class InstanaSpanTransformInfo
{
    public InstanaSpanTransformInfo()
    {
        this.StatusCode = string.Empty;
        this.StatusDesc = string.Empty;
    }

    public string StatusCode
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }

    public string StatusDesc
    {
        get => field;
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }

    public bool HasExceptionEvent { get; set; }

    public bool IsEntrySpan { get; set; }
}
