// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Exporter.Instana.Implementation.Processors;

internal sealed class ErrorActivityProcessor : ActivityProcessorBase
{
    public override void Process(Activity activity, InstanaSpan instanaSpan)
    {
        this.PreProcess(activity, instanaSpan);

        if (activity.Status == ActivityStatusCode.Error)
        {
            instanaSpan.Ec = 1;
            if (instanaSpan.Data.data != null)
            {
                instanaSpan.Data.data[InstanaExporterConstants.ERROR_FIELD] = activity.Status.ToString();
                if (activity.StatusDescription != null && !string.IsNullOrEmpty(activity.StatusDescription))
                {
                    instanaSpan.Data.data[InstanaExporterConstants.ERROR_DETAIL_FIELD] = activity.StatusDescription;
                }
            }
        }
        else
        {
            instanaSpan.Ec = instanaSpan.TransformInfo != null && instanaSpan.TransformInfo.HasExceptionEvent ? 1 : 0;
        }

        if (activity != null && instanaSpan != null)
        {
            base.Process(activity, instanaSpan);
        }
    }
}
