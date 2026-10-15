// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net.Sockets;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.Geneva.MsgPack;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

/*
Linux only. Each record is written to a Unix domain socket that a background
thread drains, so the socket send cost is included.
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmarks;

#pragma warning disable CA1873 // Avoid potentially expensive logging

[MemoryDiagnoser]
public class UnixDomainSocketLogExporterBenchmarks
{
    private string socketPath = string.Empty;
    private Socket? server;
    private Thread? receiver;
    private MsgPackLogExporter? exporter;
    private LogRecord[] logRecords = [];

    [Params(false, true)]
    public bool Batching { get; set; }

    [Params(1, 64, 512)]
    public int BatchSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.socketPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
        server.Bind(new UnixDomainSocketEndPoint(this.socketPath));
        server.Listen(1);
        this.server = server;

        this.receiver = new Thread(() => Drain(server)) { IsBackground = true };
        this.receiver.Start();

        this.exporter = new MsgPackLogExporter(
            new GenevaExporterOptions
            {
                ConnectionString = "Endpoint=unix:" + this.socketPath + (this.Batching ? ";PrivatePreviewEnableUnixDomainSocketBatching=true" : string.Empty),
                PrepopulatedFields = new Dictionary<string, object>
                {
                    ["cloud.role"] = "BusyWorker",
                    ["cloud.roleInstance"] = "CY1SCH030021417",
                    ["cloud.roleVer"] = "9.0.15289.2",
                },
            },
            () => Resource.Empty);

        this.logRecords = GenerateTestLogRecords(this.BatchSize);
    }

    [Benchmark]
    public ExportResult Export()
        => this.exporter!.Export(new Batch<LogRecord>(this.logRecords, this.logRecords.Length));

    [GlobalCleanup]
    public void Cleanup()
    {
        this.exporter?.Dispose();
        this.receiver?.Join(TimeSpan.FromSeconds(5));
        this.server?.Dispose();

        try
        {
            File.Delete(this.socketPath);
        }
        catch
        {
        }
    }

    private static void Drain(Socket server)
    {
        try
        {
            using var connection = server.Accept();
            var buffer = new byte[64 * 1024];
            while (connection.Receive(buffer) > 0)
            {
            }
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static LogRecord[] GenerateTestLogRecords(int count)
    {
        var items = new List<LogRecord>(count);
        using var factory = LoggerFactory.Create(builder => builder
            .AddOpenTelemetry(loggerOptions =>
            {
                loggerOptions.AddInMemoryExporter(items);
            }));

        var logger = factory.CreateLogger("TestCompany.TestNamespace.TestLogger");
        for (var i = 0; i < count; i++)
        {
            logger.LogInformation("Hello from {Food} {Price} {Index}.", "artichoke", 3.99, i);
        }

        return [.. items];
    }
}
