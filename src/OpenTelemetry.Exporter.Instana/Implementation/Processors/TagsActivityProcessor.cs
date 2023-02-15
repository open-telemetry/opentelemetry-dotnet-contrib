// <copyright file="TagsActivityProcessor.cs" company="OpenTelemetry Authors">
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
