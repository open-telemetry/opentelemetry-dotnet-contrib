// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable disable

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.Geneva.MsgPack;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.Geneva.Tests;

#pragma warning disable CA1873 // Avoid potentially expensive logging

/// <summary>
/// This test suite runs various multi-threaded tests to test for potential race conditions
/// associated with multi-threaded execution of the log exporter.
/// </summary>
public class MsgPackLogExporterThreadSafetyTests
{
    [Fact]
    public void SerializeLogRecord_ConcurrentThreads_ProducesValidMsgPack()
    {
        var exporterOptions = new GenevaExporterOptions();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            exporterOptions.ConnectionString = "EtwSession=OpenTelemetry";
        }
        else
        {
            var path = GetRandomFilePath();
            exporterOptions.ConnectionString = "Endpoint=unix:" + path;
        }

        // Provide resource attributes including service.name and service.instanceId
        // These get auto-mapped to cloud.role / cloud.roleInstance.
        var resourceAttributes = new Dictionary<string, object>
        {
            { "service.name", "TestService" },
            { "service.instanceId", "Instance123" },
        };
        var resource = new Resource(resourceAttributes);

        using var exporter = new MsgPackLogExporter(exporterOptions, () => resource);

        const int threadCount = 8;
        const int iterationsPerThread = 50;
        var barrier = new Barrier(threadCount);
        Exception exception = null;

        // Create a real activity to serialize
        var sourceName = GetTestMethodName();

        var threads = new Thread[threadCount];
        for (int t = 0; t < threadCount; t++)
        {
            var threadIndex = t;
            threads[t] = new Thread(() =>
            {
                try
                {
                    // Wait for all threads to be ready, maximizing contention on CreateFraming
                    barrier.SignalAndWait();

                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        List<LogRecord> logRecords = [];
                        using var loggerFactory = LoggerFactory.Create(builder => builder
                            .AddOpenTelemetry(options =>
                            {
                                options.AddInMemoryExporter(logRecords);
                            }));

                        var logger = loggerFactory.CreateLogger<GenevaLogExporterTests>();

                        logger.LogInformation("Hello from {Thread} {Iteration}.", threadIndex, i);

                        Assert.Single(logRecords);

                        var serialized = exporter.SerializeLogRecord(logRecords[0]);

                        // Validate: the serialized data must be valid MessagePack
                        // representing a proper Fluentd Forward Mode message.
                        try
                        {
                            var data = new byte[serialized.Count];
                            Array.Copy(serialized.Array!, serialized.Offset, data, 0, serialized.Count);

                            var msgPackReader = new MessagePack.MessagePackReader(data);

                            var deserialized = MessagePack.MessagePackSerializer.Deserialize<object>(
                                ref msgPackReader,
                                MessagePack.Resolvers.ContractlessStandardResolver.Options);

                            Assert.True(msgPackReader.End);
                            Assert.Equal(data.Length, msgPackReader.Consumed);

                            // Verify the Fluentd Forward Mode structure:
                            // [ "Log", [ [<timestamp>, { map }] ], { "TimeFormat": "DateTime" } ]
                            var outerArray = deserialized as object[];
                            Assert.NotNull(outerArray);
                            Assert.Equal(3, outerArray.Length);
                            Assert.Equal("Log", outerArray[0] as string);

                            var innerArray = outerArray[1] as object[];
                            Assert.NotNull(innerArray);
                            Assert.Single(innerArray);

                            var timestampAndMapping = innerArray[0] as object[];
                            Assert.NotNull(timestampAndMapping);
                            Assert.Equal(2, timestampAndMapping.Length);

                            var mapping = timestampAndMapping[1] as Dictionary<object, object>;
                            Assert.NotNull(mapping);

                            // Verify essential fields are present and correct
                            Assert.Contains("env_name", mapping.Keys);
                            Assert.Contains("env_ver", mapping.Keys);
                            Assert.Contains("env_time", mapping.Keys);
                            Assert.Contains("name", mapping.Keys);

                            // Verify resource-derived fields
                            Assert.Contains("env_cloud_role", mapping.Keys);
                            Assert.Equal("TestService", mapping["env_cloud_role"]);
                            Assert.Contains("env_cloud_roleInstance", mapping.Keys);
                            Assert.Equal("Instance123", mapping["env_cloud_roleInstance"]);

                            // Verify log fields
                            Assert.Contains("Thread", mapping.Keys);
                            Assert.Equal(threadIndex, Convert.ToInt32(mapping["Thread"]));
                            Assert.Contains("Iteration", mapping.Keys);
                            Assert.Equal(i, Convert.ToInt32(mapping["Iteration"]));
                            Assert.Contains("body", mapping.Keys);
                            Assert.Equal("Hello from {Thread} {Iteration}.", mapping["body"]);

                            Assert.Equal(11, mapping.Count);

                            // Verify the epilogue
                            var epilogue = outerArray[2] as Dictionary<object, object>;
                            Assert.NotNull(epilogue);
                            Assert.Equal("DateTime", epilogue["TimeFormat"]);
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException(
                                $"Thread {threadIndex}, iteration {i}: Serialized data is not valid MessagePack / Fluentd format. ",
                                ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            })
            {
                IsBackground = true,
            };
        }

        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        }

        if (exception != null)
        {
            throw exception;
        }
    }

    private static string GetTestMethodName([CallerMemberName] string callingMethodName = "") => callingMethodName;

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
}
