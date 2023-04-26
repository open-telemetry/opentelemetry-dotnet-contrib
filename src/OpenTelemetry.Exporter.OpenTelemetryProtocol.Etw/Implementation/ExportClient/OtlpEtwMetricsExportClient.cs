// <copyright file="OtlpEtwMetricsExportClient.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using Google.Protobuf;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OtlpCollector = OpenTelemetry.Proto.Collector.Metrics.V1;
using OtlpMetrics = OpenTelemetry.Proto.Metrics.V1;
using OtlpResource = OpenTelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Etw.Implementation.ExportClient;

/// <summary>Class for sending OTLP metrics export request over ETW.</summary>
[EventSource(Name = "OpenTelemetryOtlpMetricExporter", Guid = "{edc24920-e004-40f6-a8e1-0e6e48f39d84}")]
internal sealed class OtlpEtwMetricsExportClient : BaseOtlpEtwExportClient<OtlpCollector.ExportMetricsServiceRequest>
{
    private const int OtlpMetricEventId = 80;
    private readonly int _maxEtwEventSize;

    public OtlpEtwMetricsExportClient(OtlpEtwExporterOptions options)
    {
        this._maxEtwEventSize = options.MaxEtwEventSizeBytes;
    }

    public override bool SendExportRequest(OtlpCollector.ExportMetricsServiceRequest request, CancellationToken cancellationToken = default)
    {
        if (request.CalculateSize() > this._maxEtwEventSize)
        {
            var resourceMetrics = request.ResourceMetrics.FirstOrDefault();
            var partialRequest = CreateNewRequest(resourceMetrics.Resource);
            var partialResourceMetrics = partialRequest.ResourceMetrics.FirstOrDefault();
            var partialRequestSize = partialRequest.CalculateSize();

            // Separate partial request for each resource metric.
            foreach (var scopeMetrics in resourceMetrics.ScopeMetrics)
            {
                var partialScopeMetrics = AddScopeMetrics(partialResourceMetrics, scopeMetrics);
                partialRequestSize += partialScopeMetrics.CalculateSize();

                foreach (var otlpMetric in scopeMetrics.Metrics)
                {
                    if (partialRequestSize + otlpMetric.CalculateSize() > this._maxEtwEventSize)
                    {
                        this.WriteEvent(OtlpMetricEventId, partialRequest.ToByteArray());

                        // Reset to continue from an empty object.
                        partialRequest.Return();
                        partialResourceMetrics.ScopeMetrics.Clear();
                        partialScopeMetrics = AddScopeMetrics(partialResourceMetrics, scopeMetrics);
                        partialRequestSize = partialRequest.CalculateSize();
                    }

                    partialScopeMetrics.Metrics.Add(otlpMetric);
                    partialRequestSize += otlpMetric.CalculateSize();
                }
            }

            this.WriteEvent(OtlpMetricEventId, partialRequest.ToByteArray());
            partialRequest.Return();
        }
        else
        {
            this.WriteEvent(OtlpMetricEventId, request.ToByteArray());
        }

        return true;
    }

    private static OtlpCollector.ExportMetricsServiceRequest CreateNewRequest(OtlpResource.Resource resource)
    {
        var partialRequest = new OtlpCollector.ExportMetricsServiceRequest();
        var partialResourceMetrics = new OtlpMetrics.ResourceMetrics
        {
            Resource = resource,
        };
        partialRequest.ResourceMetrics.Add(partialResourceMetrics);
        return partialRequest;
    }

    private static OtlpMetrics.ScopeMetrics AddScopeMetrics(OtlpMetrics.ResourceMetrics partialResourceMetrics, OtlpMetrics.ScopeMetrics scopeMetrics)
    {
        var partialScopeMetrics = MetricItemExtensions.GetMetricListFromPool(scopeMetrics.Scope.Name, scopeMetrics.Scope.Version);
        partialResourceMetrics.ScopeMetrics.Add(partialScopeMetrics);
        return partialScopeMetrics;
    }

    // Though not used, these event definitions are required so event source base has the metadata for the IDs
    // that will be generated.
    [Event(OtlpMetricEventId)]
    private void OtlpMetricEvent()
    {
    }
}
