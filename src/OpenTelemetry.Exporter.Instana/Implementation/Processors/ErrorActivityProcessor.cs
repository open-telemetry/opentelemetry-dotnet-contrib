// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Threading.Tasks;

namespace OpenTelemetry.Exporter.Instana.Implementation.Processors;

internal class ErrorActivityProcessor : ActivityProcessorBase, IActivityProcessor
{
    public override async Task ProcessAsync(Activity activity, InstanaSpan instanaSpan)
    {
        this.PreProcess(activity, instanaSpan);

        if (activity.Status == ActivityStatusCode.Error)
        {
            instanaSpan.Ec = 1;
            instanaSpan.Data.data[InstanaExporterConstants.ERROR_FIELD] = activity.Status.ToString();
            if (!string.IsNullOrEmpty(activity.StatusDescription))
            {
                instanaSpan.Data.data[InstanaExporterConstants.ERROR_DETAIL_FIELD] = activity.StatusDescription;
            }
        }
        else if (instanaSpan.TransformInfo.HasExceptionEvent)
        {
            instanaSpan.Ec = 1;
        }
        else
        {
            instanaSpan.Ec = 0;
        }

        await base.ProcessAsync(activity, instanaSpan).ConfigureAwait(false);
    }
}
