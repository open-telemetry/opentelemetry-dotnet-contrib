// <copyright file="ReentrantActivityExportProcessor.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Geneva;

// This export processor exports without synchronization.
// Once OpenTelemetry .NET officially support this,
// we can get rid of this class.
// This is currently only used in ETW export, where we know
// that the underlying system is safe under concurrent calls.
internal sealed class ReentrantActivityExportProcessor : ReentrantExportProcessor<Activity>
{
    public ReentrantActivityExportProcessor(BaseExporter<Activity> exporter)
        : base(exporter)
    {
    }

    protected override void OnExport(Activity data)
    {
        if (data.Recorded)
        {
            base.OnExport(data);
        }
    }
}
