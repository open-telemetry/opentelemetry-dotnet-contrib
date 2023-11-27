// <copyright file="BatchActivityExportProcessorWithFilter.cs" company="OpenTelemetry Authors">
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
/// BatchActivityExportProcessor with a filter internal and do filtering before export.
/// </summary>
/// <remarks>
/// the default values are copied from OpenTelemetry and need to keep same with
/// these of BatchExportProcessor,as these consts are internal and not
/// visible to this project.They will be removed in one of the two conditions below.
/// - These consts will be public in Open Telemery.
/// - This class will be added to Open Telmetry and can touch these consts.
/// </remarks>
internal sealed class BatchActivityExportProcessorWithFilter : BatchActivityExportProcessor
{
    /// <summary>
    /// default max value.
    /// </summary>
    internal const int DefaultMaxQueueSize = 2048;

    /// <summary>
    /// default scheduled delay milliseconds.
    /// </summary>
    internal const int DefaultScheduledDelayMilliseconds = 5000;

    /// <summary>
    /// default Exporter timeout milliseconds.
    /// </summary>
    internal const int DefaultExporterTimeoutMilliseconds = 30000;

    /// <summary>
    /// default max Export batch size.
    /// </summary>
    internal const int DefaultMaxExportBatchSize = 512;

    /// <summary>
    /// internal filter.
    /// </summary>
    internal readonly BaseFilter<Activity> Filter;

    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchActivityExportProcessorWithFilter"/> class.
    /// </summary>
    /// <param name="exporter">Exporter instance.</param>
    /// <param name="filter">Filter instance.</param>
    /// <param name="maxQueueSize">The maximum queue size. After the size is reached data are dropped. The default value is 2048.</param>
    /// <param name="scheduledDelayMilliseconds">The delay interval in milliseconds between two consecutive exports. The default value is 5000.</param>
    /// <param name="exporterTimeoutMilliseconds">How long the export can run before it is cancelled. The default value is 30000.</param>
    /// <param name="maxExportBatchSize">The maximum batch size of every export. It must be smaller or equal to maxQueueSize. The default value is 512.</param>
    public BatchActivityExportProcessorWithFilter(
        BaseExporter<Activity> exporter,
        BaseFilter<Activity> filter,
        int maxQueueSize = DefaultMaxQueueSize,
        int scheduledDelayMilliseconds = DefaultScheduledDelayMilliseconds,
        int exporterTimeoutMilliseconds = DefaultExporterTimeoutMilliseconds,
        int maxExportBatchSize = DefaultMaxExportBatchSize)
        : base(
            exporter,
            maxQueueSize,
            scheduledDelayMilliseconds,
            exporterTimeoutMilliseconds,
            maxExportBatchSize)
    {
        this.Filter = filter;
    }

    /// <summary>
    /// filter the data before they are actually exported.
    /// </summary>
    /// <param name="data">completed activity.</param>
    public override void OnEnd(Activity data)
    {
        if (!this.Filter.ShouldFilter(data))
        {
            base.OnEnd(data);
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
