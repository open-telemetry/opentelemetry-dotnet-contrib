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
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.OneCollector;

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains options for the <see cref="OneCollectorLogExporter"/> class.
/// </summary>
public sealed class OneCollectorLogExporterOptions : OneCollectorExporterOptions, ISinkFactory<LogRecord>
{
    /// <summary>
    /// Gets or sets the default event namespace. Default value: <see
    /// cref="AssemblyName.Name"/> from the assembly returned by <see
    /// cref="Assembly.GetEntryAssembly"/>.
    /// </summary>
    /// <remarks>
    /// Note: The default event namespace is used if a <see
    /// cref="LogRecord.CategoryName"/> is not supplied.
    /// </remarks>
    internal string DefaultEventNamespace { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;

    /// <summary>
    /// Gets or sets the default event name. Default value: Log.
    /// </summary>
    /// <remarks>
    /// Note: The default event name is used when an <see
    /// cref="LogRecord.EventId"/> has a null or whitespace <see
    /// cref="EventId.Name"/>.
    /// </remarks>
    public string DefaultEventName { get; set; } = "Log";

    /// <summary>
    /// Gets the <see cref="BatchExportProcessorOptions{T}"/> options.
    /// </summary>
    public BatchExportProcessorOptions<LogRecord> BatchOptions { get; } = new();

    ISink<LogRecord> ISinkFactory<LogRecord>.CreateSink()
    {
        this.Validate();

        var transportOptions = this.TransportOptions;

#pragma warning disable CA2000 // Dispose objects before losing scope
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
                transportOptions.HttpClientFactory() ?? throw new InvalidOperationException($"{nameof(OneCollectorLogExporterOptions)} was missing HttpClientFactory or it returned null.")));
#pragma warning restore CA2000 // Dispose objects before losing scope
    }

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
}
