// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Instana.Implementation;

internal class InstanaSpanFactory
{
    internal static InstanaSpan CreateSpan()
    {
        InstanaSpan instanaSpan = new InstanaSpan
        {
            Data = new Data()
            {
                data = new Dictionary<string, object>(),
                Tags = new Dictionary<string, string>(),
                Events = new List<SpanEvent>(8),
            },

            TransformInfo = new InstanaSpanTransformInfo(),
        };

        return instanaSpan;
    }
}
