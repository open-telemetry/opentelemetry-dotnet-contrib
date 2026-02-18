// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable disable

using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenTelemetry.Exporter.Geneva.MsgPack;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.Geneva.Tests;

/// <summary>
/// Tests that demonstrate the thread-safety issue introduced by PR #3214.
///
/// The bug: <see cref="MsgPackTraceExporter.SerializeActivity"/> uses a
/// <see cref="ThreadLocal{T}"/> buffer. On each new thread, when the buffer
/// is null, it calls <see cref="MsgPackTraceExporter.CreateFraming"/>.
/// CreateFraming mutates shared instance state without synchronization:
///   - this.prepopulatedFields (dictionary reassigned and mutated)
///   - this.bufferPrologue (byte array reassigned)
///   - this.timestampPatchIndex and this.mapSizePatchIndex (int fields overwritten)
///
/// When multiple threads call SerializeActivity concurrently, they each call
/// CreateFraming, racing on these shared fields. This causes:
///   1. The Map16 field count (cntFields) to be out of sync with actual serialized fields
///   2. The mapSizePatchIndex to point to the wrong byte offset in the buffer
///   3. Corrupted MessagePack output producing "Bad forward protocol format" on the receiver.
/// </summary>
public class MsgPackTraceExporterThreadSafetyTests
{
    /// <summary>
    /// Reproduces the race condition by calling SerializeActivity from multiple
    /// threads simultaneously. Each thread's first call triggers CreateFraming(),
    /// which mutates shared state (prepopulatedFields, bufferPrologue,
    /// mapSizePatchIndex, timestampPatchIndex) without synchronization.
    ///
    /// The test verifies that every serialized buffer can be deserialized as
    /// valid Fluentd Forward Mode MessagePack. Under the race condition, the
    /// Map16 header field count becomes inconsistent with actual content,
    /// causing deserialization to fail.
    /// </summary>
    [Fact]
    public void SerializeActivity_ConcurrentThreads_ProducesValidMsgPack()
    {
        var exporterOptions = new GenevaExporterOptions();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            exporterOptions.ConnectionString = "EtwSession=OpenTelemetry";
        }
        else
        {
            // Use a dummy path - we won't actually send data, just serialize
            exporterOptions.ConnectionString = "Endpoint=unix:/tmp/otel-test-threadsafety-" + Guid.NewGuid().ToString("N");
        }

        // Provide resource attributes including service.name and service.instanceId
        // These get auto-mapped to cloud.role / cloud.roleInstance in CreateFraming,
        // adding entries to prepopulatedFields — which is the core of the race.
        var resourceAttributes = new Dictionary<string, object>
        {
            { "service.name", "TestService" },
            { "service.instanceId", "Instance123" },
        };
        var resource = new Resource(resourceAttributes);

        using var exporter = new MsgPackTraceExporter(exporterOptions, () => resource);

        const int threadCount = 8;
        const int iterationsPerThread = 50;
        var barrier = new Barrier(threadCount);
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Create a real activity to serialize
        using var activitySource = new ActivitySource("ThreadSafetyTest");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

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
                        using var activity = activitySource.StartActivity($"Op-T{threadIndex}-I{i}", ActivityKind.Internal);
                        if (activity == null)
                        {
                            continue;
                        }

                        activity.SetTag("thread", threadIndex);
                        activity.SetTag("iteration", i);

                        // This calls CreateFraming() on the first invocation per thread,
                        // racing with other threads doing the same.
                        var serialized = exporter.SerializeActivity(activity);

                        // Validate: the serialized data must be valid MessagePack
                        // representing a proper Fluentd Forward Mode message.
                        // Under the race condition, the Map16 header field count
                        // doesn't match actual serialized fields, causing this
                        // deserialization to throw.
                        try
                        {
                            var data = new byte[serialized.Count];
                            Array.Copy(serialized.Array!, serialized.Offset, data, 0, serialized.Count);

                            var deserialized = MessagePack.MessagePackSerializer.Deserialize<object>(
                                data,
                                MessagePack.Resolvers.ContractlessStandardResolver.Options);

                            // Verify the Fluentd Forward Mode structure:
                            // [ "Span", [ [<timestamp>, { map }] ], { "TimeFormat": "DateTime" } ]
                            var outerArray = deserialized as object[];
                            Assert.NotNull(outerArray);
                            Assert.Equal(3, outerArray.Length);
                            Assert.Equal("Span", outerArray[0] as string);

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
                            Assert.Contains("env_dt_traceId", mapping.Keys);
                            Assert.Contains("env_dt_spanId", mapping.Keys);
                            Assert.Contains("name", mapping.Keys);
                            Assert.Contains("kind", mapping.Keys);

                            // Verify resource-derived fields
                            Assert.Contains("env_cloud_role", mapping.Keys);
                            Assert.Equal("TestService", mapping["env_cloud_role"]);
                            Assert.Contains("env_cloud_roleInstance", mapping.Keys);
                            Assert.Equal("Instance123", mapping["env_cloud_roleInstance"]);

                            // Verify the epilogue
                            var epilogue = outerArray[2] as Dictionary<object, object>;
                            Assert.NotNull(epilogue);
                            Assert.Equal("DateTime", epilogue["TimeFormat"]);
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException(
                                $"Thread {threadIndex}, iteration {i}: Serialized data is not valid MessagePack / Fluentd format. " +
                                $"This indicates a race condition in CreateFraming() — the Map16 field count at mapSizePatchIndex " +
                                $"is out of sync with actual serialized content.",
                                ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
            threads[t].IsBackground = true;
        }

        // Start all threads
        foreach (var thread in threads)
        {
            thread.Start();
        }

        // Wait for all threads to complete
        foreach (var thread in threads)
        {
            thread.Join(TimeSpan.FromSeconds(30));
        }

        if (!exceptions.IsEmpty)
        {
            throw new AggregateException(
                $"Thread-safety test failed with {exceptions.Count} error(s). " +
                "This demonstrates the race condition in MsgPackTraceExporter.CreateFraming() — " +
                "concurrent calls mutate shared state (prepopulatedFields, bufferPrologue, " +
                "mapSizePatchIndex) without synchronization, corrupting the MessagePack output.",
                exceptions);
        }
    }

    /// <summary>
    /// A more targeted test: verifies that the prepopulatedFields.Count used to compute
    /// cntFields in SerializeActivity is consistent with the fields actually written
    /// in the prologue. This catches the specific scenario where CreateFraming is called
    /// multiple times and the prepopulatedFields dictionary ends up with a different
    /// count than what was serialized into the prologue buffer.
    /// </summary>
    [Fact]
    public void CreateFraming_CalledMultipleTimes_FieldCountRemainsConsistent()
    {
        var exporterOptions = new GenevaExporterOptions();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            exporterOptions.ConnectionString = "EtwSession=OpenTelemetry";
        }
        else
        {
            exporterOptions.ConnectionString = "Endpoint=unix:/tmp/otel-test-fieldcount-" + Guid.NewGuid().ToString("N");
        }

        var resourceAttributes = new Dictionary<string, object>
        {
            { "service.name", "TestService" },
            { "service.instanceId", "Instance123" },
        };
        var resource = new Resource(resourceAttributes);

        using var exporter = new MsgPackTraceExporter(exporterOptions, () => resource);

        // Call CreateFraming multiple times to simulate the multi-thread scenario.
        // Each call should produce the same consistent state.
        // In the buggy code, repeated calls can cause prepopulatedFields to grow
        // (duplicate entries added from resource attributes), making the Count
        // diverge from what's in the prologue.
        exporter.CreateFraming();
        var prologueAfterFirst = exporter.GetType()
            .GetField("bufferPrologue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(exporter) as byte[];
        var prepopFieldsAfterFirst = exporter.GetType()
            .GetField("prepopulatedFields", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(exporter);
        var countAfterFirst = (prepopFieldsAfterFirst as System.Collections.IDictionary)!.Count;
        var prologueLengthAfterFirst = prologueAfterFirst!.Length;

        exporter.CreateFraming();
        var prologueAfterSecond = exporter.GetType()
            .GetField("bufferPrologue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(exporter) as byte[];
        var prepopFieldsAfterSecond = exporter.GetType()
            .GetField("prepopulatedFields", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(exporter);
        var countAfterSecond = (prepopFieldsAfterSecond as System.Collections.IDictionary)!.Count;
        var prologueLengthAfterSecond = prologueAfterSecond!.Length;

        // If CreateFraming is idempotent (as it should be), repeated calls
        // must yield the same prepopulatedFields count and prologue length.
        // The bug: resource attributes are appended to prepopulatedFields on
        // every call, so the count grows and the prologue gets longer, while
        // other threads may be using the old count/prologue.
        Assert.Equal(countAfterFirst, countAfterSecond);
        Assert.Equal(prologueLengthAfterFirst, prologueLengthAfterSecond);
    }

    /// <summary>
    /// Verifies that different threads get consistent serialization results.
    /// Each thread serializes the same activity and the resulting Map16 field count
    /// (embedded in the buffer at mapSizePatchIndex) should be identical across threads.
    /// Under the race condition, different threads can end up with different field counts.
    /// </summary>
    [Fact]
    public void SerializeActivity_DifferentThreads_SameFieldCount()
    {
        var exporterOptions = new GenevaExporterOptions();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            exporterOptions.ConnectionString = "EtwSession=OpenTelemetry";
        }
        else
        {
            exporterOptions.ConnectionString = "Endpoint=unix:/tmp/otel-test-fieldcount2-" + Guid.NewGuid().ToString("N");
        }

        var resourceAttributes = new Dictionary<string, object>
        {
            { "service.name", "TestService" },
            { "service.instanceId", "Instance123" },
        };
        var resource = new Resource(resourceAttributes);

        using var exporter = new MsgPackTraceExporter(exporterOptions, () => resource);

        using var activitySource = new ActivitySource("FieldCountTest");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        const int threadCount = 8;
        var barrier = new Barrier(threadCount);
        var mapSizePatchIndex = (int)exporter.GetType()
            .GetField("mapSizePatchIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(exporter)!;

        // Collect the field counts written by each thread
        var fieldCounts = new System.Collections.Concurrent.ConcurrentBag<(int ThreadId, ushort FieldCount)>();
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        var threads = new Thread[threadCount];
        for (int t = 0; t < threadCount; t++)
        {
            var threadIndex = t;
            threads[t] = new Thread(() =>
            {
                try
                {
                    barrier.SignalAndWait();

                    using var activity = activitySource.StartActivity($"TestOp-{threadIndex}", ActivityKind.Internal);
                    if (activity == null)
                    {
                        return;
                    }

                    activity.SetTag("key", "value");

                    var serialized = exporter.SerializeActivity(activity);

                    // Read the mapSizePatchIndex — after CreateFraming runs, this field
                    // points to where the Map16 size was written. We need the *current*
                    // value since CreateFraming may have changed it.
                    var currentPatchIndex = (int)exporter.GetType()
                        .GetField("mapSizePatchIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                        .GetValue(exporter)!;

                    // Read the uint16 field count from the serialized buffer at the patch index
                    var buf = serialized.Array!;
                    ushort fieldCount = (ushort)((buf[currentPatchIndex] << 8) | buf[currentPatchIndex + 1]);

                    fieldCounts.Add((ThreadId: threadIndex, FieldCount: fieldCount));
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
            threads[t].IsBackground = true;
        }

        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            thread.Join(TimeSpan.FromSeconds(30));
        }

        if (!exceptions.IsEmpty)
        {
            throw new AggregateException("Errors during concurrent serialization", exceptions);
        }

        // All threads should report the same field count for equivalent activities
        var distinctCounts = fieldCounts.Select(x => x.FieldCount).Distinct().ToList();
        var message =
            "Expected all threads to produce the same Map16 field count, but got " + distinctCounts.Count +
            " distinct values: [" + string.Join(", ", fieldCounts.Select(x => $"T{x.ThreadId}={x.FieldCount}")) + "]. " +
            "This indicates a race condition in CreateFraming() where prepopulatedFields.Count " +
            "diverges from the fields actually serialized in the prologue.";
        Assert.True(distinctCounts.Count == 1, message);
    }
}
