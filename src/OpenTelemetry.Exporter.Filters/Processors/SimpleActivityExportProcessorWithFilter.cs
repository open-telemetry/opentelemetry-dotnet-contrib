// <copyright file="SimpleActivityExportProcessorWithFilter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Filters;

/// <summary>
/// SimpleActicityExporterProcessor with a filter internal and do filtering before export.
/// </summary>
internal sealed class SimpleActivityExportProcessorWithFilter : SimpleActivityExportProcessor
{
    /// <summary>
    /// internal filter.
    /// </summary>
    internal readonly BaseFilter<Activity> Filter;

    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleActivityExportProcessorWithFilter"/> class.
    /// </summary>
    /// <param name="exporter">Exporter instance.</param>
    /// <param name="filter">Filter instance.</param>
    public SimpleActivityExportProcessorWithFilter(BaseExporter<Activity> exporter, BaseFilter<Activity> filter)
        : base(exporter)
    {
        this.Filter = filter;
    }

    /// <summary>
    /// filter the data before they are actually exported.
    /// </summary>
    /// <param name="activity">completed activity.</param>
    public override void OnEnd(Activity activity)
    {
        if (!this.Filter.ShouldFilter(activity))
        {
            base.OnEnd(activity);
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
