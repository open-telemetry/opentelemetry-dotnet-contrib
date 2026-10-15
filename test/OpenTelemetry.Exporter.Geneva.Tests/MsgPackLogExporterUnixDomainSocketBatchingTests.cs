// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.Geneva.MsgPack;
using OpenTelemetry.Exporter.Geneva.Transports;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.Geneva.Tests;

#pragma warning disable CA1873 // Avoid potentially expensive logging

public class MsgPackLogExporterUnixDomainSocketBatchingTests
{
    private const string BatchingSwitch = ";PrivatePreviewEnableUnixDomainSocketBatching=true";
    private const int PayloadFieldCount = 4;
    private const int MinTunablePayloadFieldLength = 256; // Strings of 256+ chars use a fixed-size (str16) MessagePack header.
    private const int MaxPayloadFieldLength = 16300; // Below the default 16383-char string size limit, so payloads are never truncated.

    [SkipUnlessPlatformMatchesFact(TestPlatform.Linux)]
    public void Export_BatchingDisabled_SendsEachRecordSeparately()
    {
        using var exporter = CreateExporter(enableBatching: false, out var transport);
        var frames = CaptureFrames(exporter);
        var logRecords = CreateLogRecords([.. Enumerable.Range(0, 10).Select(_ => Lengths(100))]);

        Assert.Equal(ExportResult.Success, Export(exporter, logRecords));

        Assert.Equal(logRecords.Length, transport.Attempts.Count);
        Assert.Equal(frames, transport.Attempts);
    }

    [SkipUnlessPlatformMatchesFact(TestPlatform.Linux)]
    public void Export_BatchingEnabled_EmptyBatch_DoesNotSend()
    {
        using var exporter = CreateExporter(enableBatching: true, out var transport);

        Assert.Equal(ExportResult.Success, Export(exporter, []));

        Assert.Empty(transport.Attempts);
    }

    [SkipUnlessPlatformMatchesFact(TestPlatform.Linux)]
    public void Export_BatchingEnabled_SmallRecords_AreSentInOneWrite()
    {
        using var exporter = CreateExporter(enableBatching: true, out var transport);
        var frames = CaptureFrames(exporter);
        var logRecords = CreateLogRecords([.. Enumerable.Range(0, 10).Select(_ => Lengths(100))]);

        Assert.Equal(ExportResult.Success, Export(exporter, logRecords));

        var write = Assert.Single(transport.Attempts);
        Assert.Equal(Concat(frames), write);
        Assert.Equal(Enumerable.Range(0, 10), DecodeCompleteRecordIds(write));
    }

    [SkipUnlessPlatformMatchesTheory(TestPlatform.Linux)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Export_BatchingEnabled_EachWriteContainsOnlyCompleteRecords(int seed)
    {
        using var exporter = CreateExporter(enableBatching: true, out var transport);
        var frames = CaptureFrames(exporter);

#pragma warning disable CA5394 // Do not use insecure randomness
        var random = new Random(seed);
        var logRecords = CreateLogRecords([.. Enumerable.Range(0, 300).Select(_ => Enumerable.Range(0, PayloadFieldCount)
            .Select(_ => random.Next(4) == 0 ? random.Next(MaxPayloadFieldLength) : random.Next(200))
            .ToArray())]);
#pragma warning restore CA5394 // Do not use insecure randomness

        Assert.Equal(ExportResult.Success, Export(exporter, logRecords));

        var writes = transport.Attempts;
        Assert.Equal(ExpectedWriteSizes(frames), writes.Select(w => w.Length));
        Assert.All(writes, w => Assert.InRange(w.Length, 1, MsgPackLogExporter.BUFFER_SIZE));

        // Each write is decoded on its own: it must contain only whole records and nothing else.
        var ids = writes.SelectMany(DecodeCompleteRecordIds).ToList();
        Assert.Equal(Enumerable.Range(0, logRecords.Length), ids);
        Assert.Equal(Concat(frames), Concat(writes));
    }

    [SkipUnlessPlatformMatchesTheory(TestPlatform.Linux)]
    [InlineData(0, 1)] // The two records fill the buffer exactly.
    [InlineData(1, 2)] // One byte too many: the second record starts the next write.
    public void Export_BatchingEnabled_RecordsAtBufferBoundary_AreNotSplit(int bytesOverBuffer, int expectedWriteCount)
    {
        using var exporter = CreateExporter(enableBatching: true, out var transport);
        var firstFrameSize = 30000;
        var logRecords = new[]
        {
            CreateLogRecordWithFrameSize(exporter, 0, firstFrameSize),
            CreateLogRecordWithFrameSize(exporter, 1, MsgPackLogExporter.BUFFER_SIZE - firstFrameSize + bytesOverBuffer),
        };
        var frames = CaptureFrames(exporter);

        Assert.Equal(ExportResult.Success, Export(exporter, logRecords));

        Assert.Equal(expectedWriteCount, transport.Attempts.Count);
        Assert.Equal(ExpectedWriteSizes(frames), transport.Attempts.Select(w => w.Length));
        Assert.Equal([0, 1], transport.Attempts.SelectMany(DecodeCompleteRecordIds));
    }

    [SkipUnlessPlatformMatchesFact(TestPlatform.Linux)]
    public void Export_BatchingEnabled_NearMaxRecordAfterPartlyFilledBuffer_StartsNextWrite()
    {
        using var exporter = CreateExporter(enableBatching: true, out var transport);
        var logRecords = new[]
        {
            CreateLogRecords([Lengths(100)])[0],
            CreateLogRecordWithFrameSize(exporter, 1, MsgPackLogExporter.BUFFER_SIZE - 100),
            CreateLogRecords([Lengths(100)], firstId: 2)[0],
        };
        var frames = CaptureFrames(exporter);

        Assert.Equal(ExportResult.Success, Export(exporter, logRecords));

        Assert.Equal(3, transport.Attempts.Count);
        Assert.Equal(frames, transport.Attempts);
        Assert.Equal([0, 1, 2], transport.Attempts.SelectMany(DecodeCompleteRecordIds));
    }

    [SkipUnlessPlatformMatchesFact(TestPlatform.Linux)]
    public void Export_BatchingEnabled_RecordFailure_SkipsOnlyThatRecord()
    {
        using var exporter = CreateExporter(enableBatching: true, out var transport);
        var logRecords = CreateLogRecords([.. Enumerable.Range(0, 10).Select(_ => Lengths(12000))]);
        var invocation = 0;
        exporter.DataTransportListener = _ =>
        {
            if (invocation++ == 3)
            {
                throw new InvalidOperationException("Simulated record failure.");
            }
        };

        Assert.Equal(ExportResult.Failure, Export(exporter, logRecords));

        Assert.Equal(
            Enumerable.Range(0, 10).Where(id => id != 3),
            transport.Attempts.SelectMany(DecodeCompleteRecordIds));
    }

    [SkipUnlessPlatformMatchesFact(TestPlatform.Linux)]
    public void Export_BatchingEnabled_WriteFailure_ReturnsFailureAndDoesNotResendRecords()
    {
        using var exporter = CreateExporter(enableBatching: true, out var transport);
        transport.FailingAttempt = 0;

        // Three ~20 KB records fit into one write, so nine records need three writes.
        var logRecords = CreateLogRecords([.. Enumerable.Range(0, 9).Select(_ => Lengths(10000, 10000))]);

        Assert.Equal(ExportResult.Failure, Export(exporter, logRecords));

        Assert.Equal(3, transport.Attempts.Count);
        Assert.Equal([0, 1, 2], DecodeCompleteRecordIds(transport.Attempts[0]));
        Assert.Equal(Enumerable.Range(3, 6), transport.Attempts.Skip(1).SelectMany(DecodeCompleteRecordIds));
    }

    [SkipUnlessPlatformMatchesFact(TestPlatform.Linux)]
    public void Export_BatchingEnabled_ConcurrentExports_DoNotMixRecordsWithinAWrite()
    {
        using var exporter = CreateExporter(enableBatching: true, out var transport);
        const int threadCount = 4;
        const int recordsPerThread = 60;
        const int recordsPerBatch = 20;

        var recordsByThread = Enumerable.Range(0, threadCount)
            .Select(t => CreateLogRecords(
                [.. Enumerable.Range(0, recordsPerThread).Select(i => Lengths(2000 + (i * 97)))],
                firstId: t * 1000))
            .ToArray();

        using var barrier = new Barrier(threadCount);
        var results = new ConcurrentBag<ExportResult>();
        var threads = recordsByThread.Select(records => new Thread(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < records.Length; i += recordsPerBatch)
            {
                results.Add(Export(exporter, [.. records.Skip(i).Take(recordsPerBatch)]));
            }
        })).ToArray();

        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        }

        Assert.All(results, r => Assert.Equal(ExportResult.Success, r));

        var allIds = new List<int>();
        foreach (var write in transport.Attempts)
        {
            var ids = DecodeCompleteRecordIds(write);
            Assert.Single(ids.Select(id => id / 1000).Distinct());
            allIds.AddRange(ids);
        }

        Assert.Equal(
            Enumerable.Range(0, threadCount).SelectMany(t => Enumerable.Range(t * 1000, recordsPerThread)).OrderBy(id => id),
            allIds.OrderBy(id => id));
    }

    [SkipUnlessPlatformMatchesFact(TestPlatform.Linux)]
    public void Export_BatchingEnabled_UnixDomainSocket_SendsSameBytesAsPerRecordExport()
    {
        var logRecords = CreateLogRecords([.. Enumerable.Range(0, 40).Select(i => Lengths(300 * i, 50, i % 2 == 0 ? 4000 : 0))]);

        var perRecordBytes = ExportOverUnixDomainSocket(enableBatching: false, logRecords, out var perRecordFrames);
        var batchedBytes = ExportOverUnixDomainSocket(enableBatching: true, logRecords, out var batchedFrames);

        Assert.True(perRecordBytes.Length > 2 * MsgPackLogExporter.BUFFER_SIZE);
        Assert.Equal(Concat(perRecordFrames), perRecordBytes);
        Assert.Equal(perRecordFrames, batchedFrames);
        Assert.Equal(perRecordBytes, batchedBytes);
        Assert.Equal(Enumerable.Range(0, logRecords.Length), DecodeCompleteRecordIds(batchedBytes));
    }

    private static byte[] ExportOverUnixDomainSocket(bool enableBatching, LogRecord[] logRecords, out List<byte[]> frames)
    {
        var path = GetRandomFilePath();
        try
        {
            using var server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
            server.Bind(new UnixDomainSocketEndPoint(path));
            server.Listen(1);

            var options = new GenevaExporterOptions
            {
                ConnectionString = "Endpoint=unix:" + path + (enableBatching ? BatchingSwitch : string.Empty),
            };
            using var exporter = new MsgPackLogExporter(options, () => Resource.Empty);
            Assert.True(exporter.IsUsingUnixDomainSocket);
            frames = CaptureFrames(exporter);

            using var serverSocket = server.Accept();
            serverSocket.ReceiveTimeout = 10000;

            var expectedLength = logRecords.Sum(r => exporter.SerializeLogRecord(r).Count);
            var receive = Task.Run(() => ReceiveExactly(serverSocket, expectedLength));

            Assert.Equal(ExportResult.Success, Export(exporter, logRecords));

            Assert.True(receive.Wait(TimeSpan.FromSeconds(30)));
            return receive.Result;
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }

    private static byte[] ReceiveExactly(Socket socket, int count)
    {
        var buffer = new byte[count];
        var received = 0;
        while (received < count)
        {
            var read = socket.Receive(buffer, received, count - received, SocketFlags.None);
            if (read == 0)
            {
                break;
            }

            received += read;
        }

        Assert.Equal(count, received);
        return buffer;
    }

    private static MsgPackLogExporter CreateExporter(bool enableBatching, out CapturingDataTransport transport)
    {
        var options = new GenevaExporterOptions
        {
            // Nothing listens on this path; the transport is replaced below.
            ConnectionString = "Endpoint=unix:" + GetRandomFilePath() + (enableBatching ? BatchingSwitch : string.Empty),
        };

        var exporter = new MsgPackLogExporter(options, () => Resource.Empty);

        var field = typeof(MsgPackLogExporter).GetField("dataTransport", BindingFlags.NonPublic | BindingFlags.Instance)!;
        (field.GetValue(exporter) as IDisposable)?.Dispose();
        transport = new CapturingDataTransport();
        field.SetValue(exporter, transport);

        return exporter;
    }

    private static List<byte[]> CaptureFrames(MsgPackLogExporter exporter)
    {
        var frames = new List<byte[]>();
        exporter.DataTransportListener = data => frames.Add(ToArray(data));
        return frames;
    }

    private static ExportResult Export(MsgPackLogExporter exporter, LogRecord[] logRecords)
        => exporter.Export(new Batch<LogRecord>(logRecords, logRecords.Length));

    private static LogRecord[] CreateLogRecords(int[][] payloadFieldLengths, int firstId = 0)
    {
        var logRecords = new List<LogRecord>();
        using (var loggerFactory = LoggerFactory.Create(builder => builder
            .AddOpenTelemetry(options => options.AddInMemoryExporter(logRecords))))
        {
            var logger = loggerFactory.CreateLogger<MsgPackLogExporterUnixDomainSocketBatchingTests>();
            for (var i = 0; i < payloadFieldLengths.Length; i++)
            {
                var lengths = payloadFieldLengths[i];
                logger.LogInformation(
                    "{Id} {P0} {P1} {P2} {P3}",
                    firstId + i,
                    new string('a', lengths[0]),
                    new string('b', lengths[1]),
                    new string('c', lengths[2]),
                    new string('d', lengths[3]));
            }
        }

        Assert.Equal(payloadFieldLengths.Length, logRecords.Count);
        return [.. logRecords];
    }

    private static LogRecord CreateLogRecordWithFrameSize(MsgPackLogExporter exporter, int id, int frameSize)
    {
        var baseLengths = Enumerable.Repeat(MinTunablePayloadFieldLength, PayloadFieldCount).ToArray();
        var baseFrameSize = exporter.SerializeLogRecord(CreateLogRecords([baseLengths], id)[0]).Count;

        // ASCII strings with a str16 header grow the frame by exactly one byte per character.
        var extra = frameSize - baseFrameSize;
        Assert.InRange(extra, 0, PayloadFieldCount * (MaxPayloadFieldLength - MinTunablePayloadFieldLength));
        var lengths = baseLengths.Select((length, i) => length + (extra / PayloadFieldCount) + (i == 0 ? extra % PayloadFieldCount : 0)).ToArray();

        var logRecord = CreateLogRecords([lengths], id)[0];
        Assert.Equal(frameSize, exporter.SerializeLogRecord(logRecord).Count);
        return logRecord;
    }

    private static List<int> ExpectedWriteSizes(List<byte[]> frames)
    {
        var sizes = new List<int>();
        var pending = 0;
        foreach (var frame in frames)
        {
            if (pending > 0 && pending + frame.Length > MsgPackLogExporter.BUFFER_SIZE)
            {
                sizes.Add(pending);
                pending = 0;
            }

            pending += frame.Length;
        }

        if (pending > 0)
        {
            sizes.Add(pending);
        }

        return sizes;
    }

    private static List<int> DecodeCompleteRecordIds(byte[] write)
    {
        var ids = new List<int>();
        var reader = new MessagePack.MessagePackReader(write);
        while (!reader.End)
        {
            // Throws if the write ends in the middle of a record.
            var record = MessagePack.MessagePackSerializer.Deserialize<object>(
                ref reader,
                MessagePack.Resolvers.ContractlessStandardResolver.Options);

            // Fluentd Forward Mode: [ "Log", [ [<timestamp>, { map }] ], { "TimeFormat": "DateTime" } ]
            var outerArray = Assert.IsType<object[]>(record);
            Assert.Equal(3, outerArray.Length);
            Assert.Equal("Log", outerArray[0]);
            var entry = Assert.IsType<object[]>(Assert.Single(Assert.IsType<object[]>(outerArray[1])));
            var mapping = Assert.IsType<Dictionary<object, object>>(entry[1]);
            ids.Add(Convert.ToInt32(mapping["Id"], System.Globalization.CultureInfo.InvariantCulture));
        }

        Assert.Equal(write.Length, reader.Consumed);
        return ids;
    }

    private static byte[] Concat(IEnumerable<byte[]> chunks) => [.. chunks.SelectMany(c => c)];

    private static int[] Lengths(int p0, int p1 = 0, int p2 = 0, int p3 = 0) => [p0, p1, p2, p3];

    private static byte[] ToArray(ArraySegment<byte> data)
    {
        var copy = new byte[data.Count];
        Buffer.BlockCopy(data.Array!, data.Offset, copy, 0, data.Count);
        return copy;
    }

    private static string GetRandomFilePath()
    {
        while (true)
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            if (!File.Exists(path))
            {
                return path;
            }
        }
    }

    private sealed class CapturingDataTransport : IDataTransport
    {
        private readonly ConcurrentQueue<byte[]> attempts = new();

        public int FailingAttempt { get; set; } = -1;

        public List<byte[]> Attempts => [.. this.attempts];

        public bool IsEnabled() => true;

        public void Send(byte[] data, int size)
        {
            var copy = new byte[size];
            Buffer.BlockCopy(data, 0, copy, 0, size);
            this.attempts.Enqueue(copy);

            if (this.attempts.Count - 1 == this.FailingAttempt)
            {
                throw new SocketException((int)SocketError.TimedOut);
            }
        }
    }
}
