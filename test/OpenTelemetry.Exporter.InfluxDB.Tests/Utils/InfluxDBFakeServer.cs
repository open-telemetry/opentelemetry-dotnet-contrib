// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Net;
using System.Text;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.InfluxDB.Tests.Utils;

public class InfluxDBFakeServer : IDisposable
{
    private static readonly char[] SplitChars = Environment.NewLine.ToCharArray();
    private readonly IDisposable httpServer;
    private readonly BlockingCollection<string> lines;

    public InfluxDBFakeServer()
    {
        this.lines = new BlockingCollection<string>();
        this.httpServer = TestHttpServer.RunServer(
            context =>
            {
                byte[] buffer = new byte[context.Request.ContentLength64];
                _ = context.Request.InputStream.Read(buffer, 0, buffer.Length);
                string text = Encoding.UTF8.GetString(buffer);
                foreach (var line in text.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries))
                {
                    this.lines.Add(line);
                }

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.Close();
            },
            out var host,
            out var port);
        this.Endpoint = new Uri($"http://{host}:{port}");
    }

    public Uri Endpoint { get; }

    public void Dispose()
    {
        this.httpServer.Dispose();
    }

    public PointData ReadPoint()
    {
        if (this.lines.TryTake(out var line, TimeSpan.FromSeconds(5)))
        {
            return LineProtocolParser.ParseLine(line);
        }

        throw new InvalidOperationException("Failed to read a data point from the InfluxDB server within the 5-second timeout.");
    }
}
