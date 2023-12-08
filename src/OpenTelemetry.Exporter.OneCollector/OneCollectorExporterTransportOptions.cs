// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace OpenTelemetry.Exporter.OneCollector;

/// <summary>
/// Contains transport options for the <see cref="OneCollectorExporter{T}"/> class.
/// </summary>
public sealed class OneCollectorExporterTransportOptions
{
    internal const string DefaultOneCollectorEndpoint = "https://mobile.events.data.microsoft.com/OneCollector/1.0/";
    internal const int DefaultMaxPayloadSizeInBytes = 1024 * 1024 * 4;
    internal const int DefaultMaxNumberOfItemsPerPayload = 1500;

    internal OneCollectorExporterTransportOptions()
    {
    }

    /// <summary>
    /// Gets or sets OneCollector endpoint address. Default value:
    /// <c>https://mobile.events.data.microsoft.com/OneCollector/1.0/</c>.
    /// </summary>
    /// <remarks>
    /// Note: Endpoint is required.
    /// </remarks>
    [Required]
    public Uri Endpoint { get; set; } = new Uri(DefaultOneCollectorEndpoint);

    /// <summary>
    /// Gets or sets OneCollector transport protocol. Default value: <see
    /// cref="OneCollectorExporterTransportProtocolType.HttpJsonPost"/>.
    /// </summary>
    internal OneCollectorExporterTransportProtocolType Protocol { get; set; } = OneCollectorExporterTransportProtocolType.HttpJsonPost;

    /// <summary>
    /// Gets or sets the maximum request payload size in bytes when sending data
    /// to OneCollector. Default value: <c>4,194,304</c>.
    /// </summary>
    /// <remarks>
    /// Note: Set to -1 for unlimited request payload size.
    /// </remarks>
    internal int MaxPayloadSizeInBytes { get; set; } = DefaultMaxPayloadSizeInBytes;

    /// <summary>
    /// Gets or sets the maximum number of items per request payload when
    /// sending data to OneCollector. Default value: <c>1500</c>.
    /// </summary>
    /// <remarks>
    /// Note: Set to -1 for unlimited number of items per request payload.
    /// </remarks>
    internal int MaxNumberOfItemsPerPayload { get; set; } = DefaultMaxNumberOfItemsPerPayload;

    /// <summary>
    /// Gets or sets the compression type to use when transmiting telemetry over
    /// HTTP. Default value: <see
    /// cref="OneCollectorExporterHttpTransportCompressionType.Deflate"/>.
    /// </summary>
    internal OneCollectorExporterHttpTransportCompressionType HttpCompression { get; set; } = OneCollectorExporterHttpTransportCompressionType.Deflate;

    internal void Validate()
    {
        if (this.Endpoint == null)
        {
            throw new OneCollectorExporterValidationException($"{nameof(this.Endpoint)} was not specified on {this.GetType().Name} options.");
        }

        if (this.MaxPayloadSizeInBytes <= 0 && this.MaxPayloadSizeInBytes != -1)
        {
            throw new OneCollectorExporterValidationException($"{nameof(this.MaxPayloadSizeInBytes)} was invalid on {this.GetType().Name} options.");
        }

        if (this.MaxNumberOfItemsPerPayload <= 0 && this.MaxNumberOfItemsPerPayload != -1)
        {
            throw new OneCollectorExporterValidationException($"{nameof(this.MaxNumberOfItemsPerPayload)} was invalid on {this.GetType().Name} options.");
        }
    }
}
