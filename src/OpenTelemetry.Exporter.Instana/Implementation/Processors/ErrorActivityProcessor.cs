// <copyright file="ErrorActivityProcessor.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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
