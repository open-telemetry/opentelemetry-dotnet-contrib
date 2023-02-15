// <copyright file="OneCollectorExporter.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OneCollector;

/// <summary>
/// OpenTelemetry exporter implementation for sending telemetry data to
/// Microsoft OneCollector.
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
public class OneCollectorExporter<T> : BaseExporter<T>
    where T : class
{
    private readonly string typeName;
    private readonly ISink<T> sink;
    private Resource? resource;

    /// <summary>
    /// Initializes a new instance of the <see cref="OneCollectorExporter{T}"/> class.
    /// </summary>
    /// <param name="sinkFactory"><see cref="ISinkFactory{T}"/>.</param>
    internal OneCollectorExporter(ISinkFactory<T> sinkFactory)
    {
        Guard.ThrowIfNull(sinkFactory);

        this.typeName = typeof(T).Name;
        this.sink = sinkFactory.CreateSink();
    }

    /// <inheritdoc/>
    public sealed override ExportResult Export(in Batch<T> batch)
    {
        try
        {
            var resource = this.resource ??= this.ParentProvider?.GetResource() ?? Resource.Empty;

            var numberOfRecordsWritten = this.sink.Write(resource, in batch);

            return numberOfRecordsWritten > 0
                ? ExportResult.Success
                : ExportResult.Failure;
        }
        catch (Exception ex)
        {
            OneCollectorExporterEventSource.Log.WriteExportExceptionThrownEventIfEnabled(this.typeName, ex);

            return ExportResult.Failure;
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            (this.sink as IDisposable)?.Dispose();
        }

        base.Dispose(disposing);
    }
}
