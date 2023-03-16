// <copyright file="OneCollectorLogExportProcessorBuilder.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter.OneCollector;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains methods for building <see cref="BaseProcessor{T}"/> and <see
/// cref="OneCollectorExporter{T}"/> instances for exporting <see
/// cref="LogRecord"/> telemetry data.
/// </summary>
public sealed class OneCollectorLogExportProcessorBuilder
{
    private static readonly Func<HttpClient> DefaultHttpClientFactory = () => new HttpClient();
    private readonly OneCollectorLogExporterOptions exporterOptions = new();
    private readonly BatchExportProcessorOptions<LogRecord> batchOptions = new();
    private readonly List<Action<OneCollectorExporter<LogRecord>>> configureExporterActions = new();
    private Func<HttpClient>? httpClientFactory;

    internal OneCollectorLogExportProcessorBuilder(
        IConfiguration? configuration)
    {
        if (configuration != null)
        {
            configuration.Bind(this.exporterOptions);
            configuration.GetSection("BatchOptions").Bind(this.batchOptions);
        }
    }

    /// <summary>
    /// Register a callback action for configuring the batch options of the
    /// processor used to invoke the <see cref="OneCollectorExporter{T}"/>.
    /// </summary>
    /// <param name="configure">Callback action for configuring <see
    /// cref="BatchExportProcessorOptions{T}"/>.</param>
    /// <returns>The supplied <see
    /// cref="OneCollectorLogExportProcessorBuilder"/> for call
    /// chaining.</returns>
    public OneCollectorLogExportProcessorBuilder ConfigureBatchOptions(
        Action<BatchExportProcessorOptions<LogRecord>> configure)
    {
        Guard.ThrowIfNull(configure);

        configure(this.batchOptions);

        return this;
    }

    /// <summary>
    /// Register a callback action for configuring the <see
    /// cref="OneCollectorExporter{T}"/> created by the builder.
    /// </summary>
    /// <param name="configure">Callback action for configuring <see
    /// cref="OneCollectorExporter{T}"/>.</param>
    /// <returns>The supplied <see
    /// cref="OneCollectorLogExportProcessorBuilder"/> for call
    /// chaining.</returns>
    public OneCollectorLogExportProcessorBuilder ConfigureExporter(
        Action<OneCollectorExporter<LogRecord>> configure)
    {
        Guard.ThrowIfNull(configure);

        this.configureExporterActions.Add(configure);

        return this;
    }

    /// <summary>
    /// Register a callback action for configuring the serialization options
    /// used by the <see cref="OneCollectorExporter{T}"/> created by the
    /// builder.
    /// </summary>
    /// <param name="configure">Callback action for configuring <see
    /// cref="OneCollectorLogExporterSerializationOptions"/>.</param>
    /// <returns>The supplied <see
    /// cref="OneCollectorLogExportProcessorBuilder"/> for call
    /// chaining.</returns>
    public OneCollectorLogExportProcessorBuilder ConfigureSerializationOptions(
        Action<OneCollectorLogExporterSerializationOptions> configure)
    {
        Guard.ThrowIfNull(configure);

        configure(this.exporterOptions.SerializationOptions);

        return this;
    }

    /// <summary>
    /// Register a callback action for configuring the transport options used by
    /// the <see cref="OneCollectorExporter{T}"/> created by the builder.
    /// </summary>
    /// <param name="configure">Callback action for configuring <see
    /// cref="OneCollectorExporterTransportOptions"/>.</param>
    /// <returns>The supplied <see
    /// cref="OneCollectorLogExportProcessorBuilder"/> for call
    /// chaining.</returns>
    public OneCollectorLogExportProcessorBuilder ConfigureTransportOptions(
        Action<OneCollectorExporterTransportOptions> configure)
    {
        Guard.ThrowIfNull(configure);

        configure(this.exporterOptions.TransportOptions);

        return this;
    }

    /// <summary>
    /// Sets the connection string used by the <see
    /// cref="OneCollectorExporter{T}"/> created by the builder.
    /// </summary>
    /// <remarks><inheritdoc
    /// cref="OneCollectorExporterOptions.ConnectionString"
    /// path="/remarks"/></remarks>
    /// <param name="connectionString">Connection string.</param>
    /// <returns>The supplied <see
    /// cref="OneCollectorLogExportProcessorBuilder"/> for call
    /// chaining.</returns>
    public OneCollectorLogExportProcessorBuilder SetConnectionString(
        string connectionString)
    {
        Guard.ThrowIfNullOrWhitespace(connectionString);

        this.exporterOptions.ConnectionString = connectionString;

        return this;
    }

    /// <summary>
    /// Sets the default event name used by the <see
    /// cref="OneCollectorExporter{T}"/> created by the builder. Default value:
    /// <c>Log</c>.
    /// </summary>
    /// <remarks><inheritdoc
    /// cref="OneCollectorLogExporterOptions.DefaultEventName"
    /// path="/remarks"/></remarks>
    /// <param name="defaultEventName">Default event name.</param>
    /// <returns>The supplied <see
    /// cref="OneCollectorLogExportProcessorBuilder"/> for call
    /// chaining.</returns>
    public OneCollectorLogExportProcessorBuilder SetDefaultEventName(
        string defaultEventName)
    {
        Guard.ThrowIfNullOrWhitespace(defaultEventName);

        this.exporterOptions.DefaultEventName = defaultEventName;

        return this;
    }

    /// <summary>
    /// Sets the factory function called to create the <see cref="HttpClient"/>
    /// instance that will be used at runtime to transmit telemetry over HTTP
    /// transports. The returned instance will be reused for all export
    /// invocations.
    /// </summary>
    /// <remarks>
    /// Note: The default behavior is an <see cref="HttpClient"/> will be
    /// instantiated directly.
    /// </remarks>
    /// <param name="httpClientFactory">Factory function which returns the <see
    /// cref="HttpClient"/> instance to use.</param>
    /// <returns>The supplied <see
    /// cref="OneCollectorLogExportProcessorBuilder"/> for call
    /// chaining.</returns>
    internal OneCollectorLogExportProcessorBuilder SetHttpClientFactory(
        Func<HttpClient> httpClientFactory)
    {
        Guard.ThrowIfNull(httpClientFactory);

        this.httpClientFactory = httpClientFactory;

        return this;
    }

    internal BaseProcessor<LogRecord> BuildProcessor()
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        return new BatchLogRecordExportProcessor(
            this.BuildExporter(),
            this.batchOptions.MaxQueueSize,
            this.batchOptions.ScheduledDelayMilliseconds,
            this.batchOptions.ExporterTimeoutMilliseconds,
            this.batchOptions.MaxExportBatchSize);
#pragma warning restore CA2000 // Dispose objects before losing scope
    }

    private OneCollectorExporter<LogRecord> BuildExporter()
    {
        var exporter = new OneCollectorExporter<LogRecord>(this.CreateSink());

        try
        {
            int index = 0;
            while (index < this.configureExporterActions.Count)
            {
                var action = this.configureExporterActions[index++];
                action(exporter);
            }
        }
        catch
        {
            exporter.Dispose();
            throw;
        }

        return exporter;
    }

    private ISink<LogRecord> CreateSink()
    {
        this.exporterOptions.Validate();

        var transportOptions = this.exporterOptions.TransportOptions;

        var httpClient = (this.httpClientFactory ?? DefaultHttpClientFactory)() ?? throw new NotSupportedException("HttpClientFactory cannot return a null instance.");

#pragma warning disable CA2000 // Dispose objects before losing scope
        return new WriteDirectlyToTransportSink<LogRecord>(
            new LogRecordCommonSchemaJsonSerializer(
                new EventNameManager(this.exporterOptions.DefaultEventNamespace, this.exporterOptions.DefaultEventName),
                this.exporterOptions.TenantToken!,
                this.exporterOptions.SerializationOptions.ExceptionStackTraceHandling,
                transportOptions.MaxPayloadSizeInBytes == -1 ? int.MaxValue : transportOptions.MaxPayloadSizeInBytes,
                transportOptions.MaxNumberOfItemsPerPayload == -1 ? int.MaxValue : transportOptions.MaxNumberOfItemsPerPayload),
            new HttpJsonPostTransport(
                this.exporterOptions.InstrumentationKey!,
                transportOptions.Endpoint,
                transportOptions.HttpCompression,
                new HttpClientWrapper(httpClient)));
#pragma warning restore CA2000 // Dispose objects before losing scope
    }
}
