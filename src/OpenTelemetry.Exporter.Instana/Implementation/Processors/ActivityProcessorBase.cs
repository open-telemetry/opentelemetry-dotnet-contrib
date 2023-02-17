// <copyright file="ActivityProcessorBase.cs" company="OpenTelemetry Authors">
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

internal abstract class ActivityProcessorBase : IActivityProcessor
{
    public IActivityProcessor NextProcessor { get; set; }

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
