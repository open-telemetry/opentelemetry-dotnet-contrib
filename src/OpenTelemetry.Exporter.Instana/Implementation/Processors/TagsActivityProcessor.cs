// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace OpenTelemetry.Exporter.Instana.Implementation.Processors;

internal class TagsActivityProcessor : ActivityProcessorBase, IActivityProcessor
{
    public override async Task ProcessAsync(Activity activity, InstanaSpan instanaSpan)
    {
        this.PreProcess(activity, instanaSpan);

        string statusCode = string.Empty;
        string statusDesc = string.Empty;
        Dictionary<string, string> tags = new Dictionary<string, string>();
        foreach (var tag in activity.Tags)
        {
            if (tag.Key == "otel.status_code")
            {
                statusCode = tag.Value as string;
                continue;
            }

            if (tag.Key == "otel.status_description")
            {
                statusDesc = tag.Value as string;
                continue;
            }

            if (tag.Value != null)
            {
                tags[tag.Key] = tag.Value.ToString();
            }
        }

        instanaSpan.Data.Tags = tags;
        instanaSpan.TransformInfo.StatusCode = statusCode;
        instanaSpan.TransformInfo.StatusDesc = statusDesc;

        await base.ProcessAsync(activity, instanaSpan).ConfigureAwait(false);
    }
}
