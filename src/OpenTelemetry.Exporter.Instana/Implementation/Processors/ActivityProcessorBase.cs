// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Exporter.Instana.Implementation.Processors;

internal abstract class ActivityProcessorBase : IActivityProcessor
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public IActivityProcessor NextProcessor { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public virtual async Task ProcessAsync(Activity activity, InstanaSpan instanaSpan)
    {
        if (this.NextProcessor != null)
        {
            await this.NextProcessor.ProcessAsync(activity, instanaSpan).ConfigureAwait(false);
        }
    }

    protected virtual void PreProcess(Activity activity, InstanaSpan instanaSpan)
    {
        if (instanaSpan.TransformInfo == null)
        {
            instanaSpan.TransformInfo = new InstanaSpanTransformInfo();
        }

        if (instanaSpan.Data == null)
        {
            instanaSpan.Data = new Data()
            {
                data = new Dictionary<string, object>(),
                Events = new List<SpanEvent>(8),
                Tags = new Dictionary<string, string>(),
            };
        }
    }
}
