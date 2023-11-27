// <copyright file="ReentrantActivityExportProcessorWithFilter.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter.Geneva;

namespace OpenTelemetry.Exporter.Filters;

/// <summary>
/// ReentrantActivityExportProcessor with a filter internal and do filtering before export.
/// </summary>
internal sealed class ReentrantActivityExportProcessorWithFilter : ReentrantExportProcessor<Activity>
{
    internal readonly BaseFilter<Activity> Filter;

    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReentrantActivityExportProcessorWithFilter"/> class.
    /// </summary>
    /// <param name="exporter">Exporter instance.</param>
    /// <param name="filter">Filter instance.</param>
    public ReentrantActivityExportProcessorWithFilter(BaseExporter<Activity> exporter, BaseFilter<Activity> filter)
        : base(exporter)
    {
        this.Filter = filter;
    }

    /// <summary>
    /// filter the data before they are actually exported.
    /// </summary>
    /// <param name="data">completed activity.</param>
    protected override void OnExport(Activity data)
    {
        if (!this.Filter.ShouldFilter(data))
        {
            base.OnExport(data);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                this.Filter.Dispose();
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }
}
