// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Extensions.Logging;

internal readonly struct FormattedLogValues<TInnerState> : IReadOnlyList<KeyValuePair<string, object?>>
{
    private readonly ActivityTraceId traceId;
    private readonly IReadOnlyList<KeyValuePair<string, object?>>? properties;

    public FormattedLogValues(TInnerState inner, ActivityTraceId traceId)
    {
        this.Inner = inner;
        this.traceId = traceId;
        this.properties = inner as IReadOnlyList<KeyValuePair<string, object?>>;
    }

    public TInnerState Inner { get; }

    public int Count => (this.properties?.Count).GetValueOrDefault() + 1;

    public KeyValuePair<string, object?> this[int index] => index == 0
        ? new KeyValuePair<string, object?>("TraceId", this.traceId)
        : this.properties is { } properties
            ? properties[index - 1]
            : throw new ArgumentOutOfRangeException(nameof(index));

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        for (var i = 0; i < this.Count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    public override string? ToString() => this.Inner?.ToString();

}
