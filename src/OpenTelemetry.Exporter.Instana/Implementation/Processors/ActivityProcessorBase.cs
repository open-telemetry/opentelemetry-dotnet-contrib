// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Exporter.Instana.Implementation.Processors;

internal abstract class ActivityProcessorBase : IActivityProcessor
{
    public IActivityProcessor? NextProcessor { get; set; }

    public virtual void Process(Activity activity, InstanaSpan instanaSpan)
        => this.NextProcessor?.Process(activity, instanaSpan);

    protected virtual void PreProcess(Activity activity, InstanaSpan instanaSpan)
    {
        instanaSpan.TransformInfo ??= new InstanaSpanTransformInfo();

        instanaSpan.Data ??= new Data()
        {
            data = [],
            Events = new(8),
            Tags = [],
        };
    }
}
