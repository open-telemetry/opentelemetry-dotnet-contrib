// <copyright file="OneCollectorExporterPayloadTransmittedCallbackArguments.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OneCollector;

/// <summary>
/// Defines a callback delegate for handling payload transmitted events in the
/// <see cref="OneCollectorExporter{T}"/> class.
/// </summary>
/// <param name="args">Arguments.</param>
public delegate void OneCollectorExporterPayloadTransmittedCallbackAction(
    in OneCollectorExporterPayloadTransmittedCallbackArguments args);

/// <summary>
/// Contains arguments for the <see cref="OneCollectorExporterPayloadTransmittedCallbackAction"/>
/// callback delegate.
/// </summary>
public readonly ref struct OneCollectorExporterPayloadTransmittedCallbackArguments
{
    private readonly Stream payloadStream;

    internal OneCollectorExporterPayloadTransmittedCallbackArguments(
        OneCollectorExporterSerializationFormatType payloadSerializationFormat,
        Stream payloadStream,
        OneCollectorExporterTransportProtocolType transportProtocol,
        Uri transportEndpoint,
        bool succeeded)
    {
        Debug.Assert(payloadStream != null, "payload stream was null");
        Debug.Assert(payloadStream!.CanSeek, "payload stream was not seekable");
        Debug.Assert(transportEndpoint != null, "transportEndpoint was null");

        this.PayloadSerializationFormat = payloadSerializationFormat;
        this.payloadStream = payloadStream;
        this.TransportProtocol = transportProtocol;
        this.TransportEndpoint = transportEndpoint!;
        this.Succeeded = succeeded;
    }

    /// <summary>
    /// Gets the payload size in bytes.
    /// </summary>
    public long PayloadSizeInBytes => this.payloadStream!.Length;

    /// <summary>
    /// Gets the transport endpoint.
    /// </summary>
    public Uri TransportEndpoint { get; }

    /// <summary>
    /// Gets a value indicating whether or not the payload transmission was successful.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>
    /// A <see langword="true"/> value indicates a request was fully transmitted
    /// and acknowledged.
    /// </item>
    /// <item>
    /// A <see langword="false"/> value indicates a request did NOT fully
    /// transmit or an acknowledgement was NOT received. Data may have been
    /// partially or fully transmitted in this case.
    /// </item>
    /// <item>
    /// <inheritdoc cref="OneCollectorExporter{T}.RegisterPayloadTransmittedCallback(OneCollectorExporterPayloadTransmittedCallbackAction)" path="/remarks"/>
    /// </item>
    /// </list>
    /// </remarks>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets the payload serialization format.
    /// </summary>
    internal OneCollectorExporterSerializationFormatType PayloadSerializationFormat { get; }

    /// <summary>
    /// Gets the transport protocol.
    /// </summary>
    internal OneCollectorExporterTransportProtocolType TransportProtocol { get; }

    /// <summary>
    /// Copy the bytes of the payload to the stream specified by the <paramref
    /// name="destination"/> parameter.
    /// </summary>
    /// <param name="destination">Destination <see cref="Stream"/>.</param>
    public void CopyPayloadToStream(Stream destination)
    {
        Guard.ThrowIfNull(destination);

        var startPosition = this.payloadStream.Position;
        try
        {
            this.payloadStream.CopyTo(destination);
        }
        finally
        {
            this.payloadStream.Position = startPosition;
        }
    }
}
