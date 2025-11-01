// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable disable

using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Context;
using OpenTelemetry.Exporter.Geneva.MsgPack;
using OpenTelemetry.Logs;
using Xunit;

namespace OpenTelemetry.Exporter.Geneva.Tests;

[Collection(nameof(GenevaCorrelationFixture))]
public class GenevaLogExporterAFDCorrelationTests
{
    [Fact(Skip = "Run locally to evaluate with multi-threaded scenario.")]
    public void AFDCorrelationIdLogProcessor_MultithreadedAccess_HandlesGracefully()
    {
        var path = string.Empty;
        Socket senderSocket = null;
        Socket receiverSocket = null;

        try
        {
            var exporterOptions = new GenevaExporterOptions();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                exporterOptions.ConnectionString = "EtwSession=OpenTelemetry;PrivatePreviewEnableAFDCorrelationIdEnrichment=true";
            }
            else
            {
                path = GenerateTempFilePath();
                exporterOptions.ConnectionString = "Endpoint=unix:" + path + ";PrivatePreviewEnableAFDCorrelationIdEnrichment=true";
                var endpoint = new UnixDomainSocketEndPoint(path);
                senderSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                senderSocket.Bind(endpoint);
                senderSocket.Listen(1);
            }

            var exportedItems = new List<LogRecord>();
            var syncObj = new Lock();

            using var loggerFactory = LoggerFactory.Create(builder => builder
                .AddOpenTelemetry(options =>
                {
                    options.IncludeScopes = true;
                    options.AddInMemoryExporter(exportedItems);
                    options.AddGenevaLogExporter(options =>
                    {
                        options.ConnectionString = exporterOptions.ConnectionString;
                    });
                }));

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                receiverSocket = senderSocket.Accept();
                receiverSocket.ReceiveTimeout = 10000;
            }

            // Create a test exporter
            using var exporter = new GenevaLogExporter(exporterOptions);

            // Now create multiple threads to simulate concurrent access
            var logger = loggerFactory.CreateLogger<GenevaLogExporterTests>();
            const int threadCount = 10;
            var threads = new Thread[threadCount];
            var countWithCorrelationId = 0;
            var countWithoutCorrelationId = 0;

            for (int i = 0; i < threadCount; i++)
            {
                var threadIndex = i;
                threads[i] = new Thread(() =>
                {
                    if (threadIndex % 2 == 0)
                    {
                        // This thread sets AFDCorrelationId before logging
                        var actualCorrelationId = $"CorrelationId-{threadIndex}";
                        OpenTelemetryContext.SetAFDCorrelationId(actualCorrelationId);
#pragma warning disable CA2254 // Template should be a static expression
                        logger.LogInformation($"Thread {threadIndex} with correlation ID");
#pragma warning restore CA2254 // Template should be a static expression
                        lock (syncObj)
                        {
                            countWithCorrelationId++;
                        }

                        byte[] serializedData;
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            serializedData = MsgPackLogExporter.Buffer.Value;
                        }
                        else
                        {
                            // Read the data sent via socket.
                            serializedData = new byte[65360];
                            _ = receiverSocket.Receive(serializedData);
                        }

                        var fluentdData = MessagePack.MessagePackSerializer.Deserialize<object>(serializedData, MessagePack.Resolvers.ContractlessStandardResolver.Options);
                        var signal = (fluentdData as object[])[0] as string;
                        var TimeStampAndMappings = ((fluentdData as object[])[1] as object[])[0];
                        var mapping = (TimeStampAndMappings as object[])[1] as Dictionary<object, object>;

                        var expectedCorrelationId = mapping["AFDCorrelationId"] as string;
                        Assert.Equal(actualCorrelationId, expectedCorrelationId);
                    }
                    else
                    {
#pragma warning disable CA2254 // Template should be a static expression
                        logger.LogInformation($"Thread {threadIndex} without correlation ID");
#pragma warning restore CA2254 // Template should be a static expression
                        lock (syncObj)
                        {
                            countWithoutCorrelationId++;
                        }

                        byte[] serializedData;
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            serializedData = MsgPackLogExporter.Buffer.Value;
                        }
                        else
                        {
                            // Read the data sent via socket
                            serializedData = new byte[65360];
                            _ = receiverSocket.Receive(serializedData);
                        }

                        var fluentdData = MessagePack.MessagePackSerializer.Deserialize<object>(serializedData, MessagePack.Resolvers.ContractlessStandardResolver.Options);
                        var signal = (fluentdData as object[])[0] as string;
                        var TimeStampAndMappings = ((fluentdData as object[])[1] as object[])[0];
                        var mapping = (TimeStampAndMappings as object[])[1] as Dictionary<object, object>;

                        // Verify that AFDCorrelationId is not present in the serialized data
                        Assert.False(mapping.ContainsKey("AFDCorrelationId"));
                    }
                });
            }

            // Start all threads
            foreach (var thread in threads)
            {
                thread.Start();
            }

            // Wait for all threads to finish
            foreach (var thread in threads)
            {
                thread.Join();
            }

            // Verify the correct number of logs were created
            Assert.Equal(threadCount, exportedItems.Count);
            Assert.Equal(threadCount / 2, countWithCorrelationId);
            Assert.Equal(threadCount / 2, countWithoutCorrelationId);

            // Check that no exceptions were thrown
            // If our implementation is correct, logs from threads without correlation ID
            // should have been processed without exceptions
        }
        finally
        {
            senderSocket?.Dispose();
            receiverSocket?.Dispose();
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void AFDCorrelationIdLogProcessor_WithoutCorrelationId_HandlesGracefully()
    {
        var path = string.Empty;
        Socket senderSocket = null;
        Socket receiverSocket = null;

        try
        {
            var exporterOptions = new GenevaExporterOptions();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                exporterOptions.ConnectionString = "EtwSession=OpenTelemetry;PrivatePreviewEnableAFDCorrelationIdEnrichment=true";
            }
            else
            {
                path = GenerateTempFilePath();
                exporterOptions.ConnectionString = "Endpoint=unix:" + path + ";PrivatePreviewEnableAFDCorrelationIdEnrichment=true";
                var endpoint = new UnixDomainSocketEndPoint(path);
                senderSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                senderSocket.Bind(endpoint);
                senderSocket.Listen(1);
            }

            var exportedItems = new List<LogRecord>();

            using var loggerFactory = LoggerFactory.Create(builder => builder
                .AddOpenTelemetry(options =>
                {
                    options.IncludeScopes = true;
                    options.AddInMemoryExporter(exportedItems);
                    options.AddGenevaLogExporter(options =>
                    {
                        options.ConnectionString = exporterOptions.ConnectionString;
                    });
                }));

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                receiverSocket = senderSocket.Accept();
                receiverSocket.ReceiveTimeout = 10000;
            }

            // Create a test exporter to get MessagePack byte data for validation
            using var exporter = new GenevaLogExporter(exporterOptions);

            // In this test, AFDCorrelationId is not set in RuntimeContext
            var logger = loggerFactory.CreateLogger<GenevaLogExporterTests>();
            logger.LogInformation("No correlation ID should be present");

            byte[] serializedData;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                serializedData = MsgPackLogExporter.Buffer.Value;
            }
            else
            {
                // Read the data sent via socket
                serializedData = new byte[65360];
                _ = receiverSocket.Receive(serializedData);
            }

            var fluentdData = MessagePack.MessagePackSerializer.Deserialize<object>(serializedData, MessagePack.Resolvers.ContractlessStandardResolver.Options);
            var signal = (fluentdData as object[])[0] as string;
            var TimeStampAndMappings = ((fluentdData as object[])[1] as object[])[0];
            var mapping = (TimeStampAndMappings as object[])[1] as Dictionary<object, object>;

            // Verify that AFDCorrelationId is not present in the serialized data
            Assert.False(mapping.ContainsKey("AFDCorrelationId"));

            // Verify the log record was processed successfully
            Assert.Single(exportedItems);
            Assert.Equal("No correlation ID should be present", exportedItems[0].Body);
        }
        finally
        {
            senderSocket?.Dispose();
            receiverSocket?.Dispose();
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void GenevaExporter_WithAFDCorrelationId_IncludesCorrelationId()
    {
        var path = string.Empty;
        Socket senderSocket = null;
        Socket receiverSocket = null;

        OpenTelemetryContext.SetAFDCorrelationId("TestId");

        try
        {
            var exporterOptions = new GenevaExporterOptions();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                exporterOptions.ConnectionString = "EtwSession=OpenTelemetry;PrivatePreviewEnableAFDCorrelationIdEnrichment=true";
            }
            else
            {
                path = GenerateTempFilePath();
                exporterOptions.ConnectionString = "Endpoint=unix:" + path + ";PrivatePreviewEnableAFDCorrelationIdEnrichment=true";
                var endpoint = new UnixDomainSocketEndPoint(path);
                senderSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                senderSocket.Bind(endpoint);
                senderSocket.Listen(1);
            }

            var exportedItems = new List<LogRecord>();

            using var loggerFactory = LoggerFactory.Create(builder => builder
                .AddOpenTelemetry(options =>
                {
                    options.IncludeScopes = true;
                    options.AddInMemoryExporter(exportedItems);
                    options.AddGenevaLogExporter(options =>
                    {
                        options.ConnectionString = exporterOptions.ConnectionString;
                    });
                }));

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                receiverSocket = senderSocket.Accept();
                receiverSocket.ReceiveTimeout = 10000;
            }

            // Create a test exporter to get MessagePack byte data to validate if the data was serialized correctly.
            using var exporter = new GenevaLogExporter(exporterOptions);

            // Emit a LogRecord and grab a copy of internal buffer for validation.
            var logger = loggerFactory.CreateLogger<GenevaLogExporterTests>();
            logger.LogInformation("Hello from {Food} {Price}.", "artichoke", 3.99);

            byte[] serializedData;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                serializedData = MsgPackLogExporter.Buffer.Value;
            }
            else
            {
                // Read the data sent via socket.
                serializedData = new byte[65360];
                _ = receiverSocket.Receive(serializedData);
            }

            var fluentdData = MessagePack.MessagePackSerializer.Deserialize<object>(serializedData, MessagePack.Resolvers.ContractlessStandardResolver.Options);
            var signal = (fluentdData as object[])[0] as string;
            var TimeStampAndMappings = ((fluentdData as object[])[1] as object[])[0];
            var mapping = (TimeStampAndMappings as object[])[1] as Dictionary<object, object>;

            var correlationId = mapping["AFDCorrelationId"] as string;
            Assert.Equal("TestId", correlationId);
        }
        finally
        {
            senderSocket?.Dispose();
            receiverSocket?.Dispose();
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }

    private static void ClearRunTimeContext()
    {
        var type = typeof(RuntimeContext);
        var method = type.GetMethod("Clear", BindingFlags.Static | BindingFlags.NonPublic);
        if (method != null)
        {
            method.Invoke(null, null);
        }
    }

    private static string GenerateTempFilePath()
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

    private static class OpenTelemetryContext
    {
        private const string AFDCorrelationId = "AFDCorrelationId";
        private static readonly RuntimeContextSlot<string> AFDCorrelationContextSlot = RuntimeContext.RegisterSlot<string>(AFDCorrelationId);

        internal static void SetAFDCorrelationId(string correlationId)
        {
            if (!string.IsNullOrEmpty(correlationId))
            {
                AFDCorrelationContextSlot.Set(correlationId);
            }
        }
    }
}
