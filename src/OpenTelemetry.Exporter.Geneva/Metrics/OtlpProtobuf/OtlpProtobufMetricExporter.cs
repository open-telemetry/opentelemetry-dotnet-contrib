// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.InteropServices;

#if NET6_0_OR_GREATER
using System.Net.Http;
using System.Net.Http.Headers;
#endif
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Geneva;

internal class OtlpProtobufMetricExporter : IDisposable
{
    private const int BufferSize = 65360; // the maximum ETW payload (inclusive)

    private readonly byte[] buffer = new byte[BufferSize];

    private readonly OtlpProtobufSerializer otlpProtobufSerializer;

    private Resource resource;

    internal Resource MetricResource => this.resource ??= this.genevaMetricExporter.ParentProvider.GetResource();

    private GenevaMetricExporter genevaMetricExporter;

    public OtlpProtobufMetricExporter(GenevaMetricExporter genevaMetricExporter)
    {
        // TODO
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Temporary until we add support for user_events.
            throw new NotSupportedException("Unix domain socket should not be used on Windows.");
        }

        this.genevaMetricExporter = genevaMetricExporter;

        this.otlpProtobufSerializer = new OtlpProtobufSerializer();
    }

    public ExportResult Export(in Batch<Metric> batch)
    {
        var result = ExportResult.Success;

        int currentPosition = 0;

        try
        {
            this.otlpProtobufSerializer.SerializeMetrics(this.buffer, ref currentPosition, this.MetricResource, batch);

            // Send request.
            MetricEtwDataTransport.Instance.SendOtlpProtobufEvent(this.buffer, currentPosition);
        }
        catch (Exception ex)
        {
            ExporterEventSource.Log.ExporterException("metric batch failed", ex);
        }

#if NET6_0_OR_GREATER
        byte[] arr = new byte[currentPosition];

        Buffer.BlockCopy(this.buffer, 0, arr, 0, arr.Length);

        SendRequest(arr);
#endif

        return result;
    }

#if NET6_0_OR_GREATER
    private static void SendRequest(byte[] buffer)
    {
        HttpClient httpClient = new HttpClient();

        // Create a new HttpRequestMessage
        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:4318/v1/metrics");

        // Set the Content to a ByteArrayContent containing the serialized data
        request.Content = new ByteArrayContent(buffer);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");

        // Version on message is needed
        // Without it HttpClient will not identify this as Http/2 request.
        // request.Version = new Version(2, 0);
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;

        // Send the request
        var response = httpClient.SendAsync(request).Result;

        // Ensure the request was successful
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Request failed with status code {response.StatusCode}");
        }
    }
#endif

    public void Dispose()
    {
    }
}
