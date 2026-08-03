// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Net;
using System.Text;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.InfluxDB.Tests.Utils;

internal class InfluxDBFakeServer : IDisposable
{
    private static readonly char[] SplitChars = Environment.NewLine.ToCharArray();
    private readonly IDisposable httpServer;
    private readonly BlockingCollection<string> lines;

    // Read on the listener thread, written from test threads; keep it volatile so a
    // recovery (flipping the status code back to OK) is observed by the handler.
    private volatile int responseStatusCode = (int)HttpStatusCode.OK;

    public InfluxDBFakeServer()
    {
        this.lines = [];
        this.httpServer = TestHttpServer.RunServer(
            context =>
            {
                // Read the whole body: Stream.Read may return fewer bytes than requested,
                // so a single Read can truncate larger write payloads.
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                var text = reader.ReadToEnd();

                var statusCode = (HttpStatusCode)this.responseStatusCode;
                if (statusCode == HttpStatusCode.OK)
                {
                    foreach (var line in text.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries))
                    {
                        this.lines.Add(line);
                    }
                }

                context.Response.StatusCode = (int)statusCode;
                context.Response.Close();
            },
            out var baseAddress);
        this.Endpoint = baseAddress;
    }

    public Uri Endpoint { get; }

    /// <summary>
    /// Gets or sets the HTTP status code returned for write requests. Setting a
    /// non-success code (for example <see cref="HttpStatusCode.BadRequest"/>)
    /// simulates an unavailable/erroring InfluxDB backend; write payloads received
    /// while a non-OK code is configured are discarded.
    /// </summary>
    public HttpStatusCode ResponseStatusCode
    {
        get => (HttpStatusCode)this.responseStatusCode;
        set => this.responseStatusCode = (int)value;
    }

    public void Dispose()
        => this.httpServer.Dispose();

    public PointData ReadPoint() =>
        this.lines.TryTake(out var line, TimeSpan.FromSeconds(5))
            ? LineProtocolParser.ParseLine(line)
            : throw new InvalidOperationException(
                "Failed to read a data point from the InfluxDB server within the 5-second timeout.");
}
