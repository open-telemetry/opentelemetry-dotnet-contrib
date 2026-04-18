// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Instana.Implementation;

internal static class InstanaSpanFactory
{
    internal static InstanaSpan CreateSpan() => new()
    {
        Data = new Data()
        {
            Values = [],
            Tags = [],
            Events = new(8),
        },
        TransformInfo = new(),
    };
}
