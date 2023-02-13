// <copyright file="OneCollectorLogExporterOptions.cs" company="OpenTelemetry Authors">
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

using System.Reflection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.OneCollector;

namespace OpenTelemetry.Logs;

public sealed class OneCollectorLogExporterOptions : OneCollectorExporterOptions<LogRecord>
{
    public string DefaultEventNamespace { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;

    public string DefaultEventName { get; set; } = "Log";

    /// <summary>
    /// Gets the <see cref="BatchExportProcessorOptions{T}"/> options.
    /// </summary>
    public BatchExportProcessorOptions<LogRecord> BatchOptions { get; } = new();

    internal override void Validate()
    {
        if (string.IsNullOrWhiteSpace(this.DefaultEventNamespace))
        {
            throw new InvalidOperationException($"{nameof(this.DefaultEventNamespace)} was not specified on {nameof(OneCollectorLogExporterOptions)} options.");
        }

        if (string.IsNullOrWhiteSpace(this.DefaultEventName))
        {
            throw new InvalidOperationException($"{nameof(this.DefaultEventName)} was not specified on {nameof(OneCollectorLogExporterOptions)} options.");
        }

        base.Validate();
    }

    internal override ISink<LogRecord> CreateSink()
    {
        this.Validate();

        var transportOptions = this.TransportOptions;

        return new WriteDirectlyToTransportSink<LogRecord>(
            new LogRecordCommonSchemaJsonSerializer(
                new EventNameManager(this.DefaultEventNamespace, this.DefaultEventName),
                this.TenantToken!,
                transportOptions.MaxPayloadSizeInBytes == -1 ? int.MaxValue : transportOptions.MaxPayloadSizeInBytes,
                transportOptions.MaxNumberOfItemsPerPayload == -1 ? int.MaxValue : transportOptions.MaxNumberOfItemsPerPayload),
            new HttpJsonPostTransport(
                this.InstrumentationKey!,
                transportOptions.Endpoint,
                transportOptions.HttpCompression,
                transportOptions.HttpClientFactory() ?? throw new InvalidOperationException($"{this.GetType().Name} was missing HttpClientFactory or it returned null.")));
    }
}
