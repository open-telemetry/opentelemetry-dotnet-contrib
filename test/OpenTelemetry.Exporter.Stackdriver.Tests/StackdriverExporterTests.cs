// <copyright file="StackdriverExporterTests.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Google.Api.Gax.Grpc;
using Google.Cloud.Trace.V2;
using Grpc.Core;
using Moq;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Xunit;
using Status = Grpc.Core.Status;

namespace OpenTelemetry.Exporter.Stackdriver.Tests;

public class StackdriverExporterTests
{
    static StackdriverExporterTests()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
        };

        ActivitySource.AddActivityListener(listener);
    }

    [Fact]
    public void StackdriverExporter_CustomActivityProcessor()
    {
        const string ActivitySourceName = "stackdriver.test";
        Guid requestId = Guid.NewGuid();
        TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

        bool startCalled = false;
        bool endCalled = false;

        testActivityProcessor.StartAction =
            (a) =>
            {
                startCalled = true;
            };

        testActivityProcessor.EndAction =
            (a) =>
            {
                endCalled = true;
            };

        var openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
            .AddSource(ActivitySourceName)
            .AddProcessor(testActivityProcessor)
            .UseStackdriverExporter("test").Build();

        var source = new ActivitySource(ActivitySourceName);
        var activity = source.StartActivity("Test Activity");
        activity?.Stop();

        Assert.True(startCalled);
        Assert.True(endCalled);
    }

    [Fact]
    public void StackdriverExporter_TraceClientThrows_ExportResultFailure()
    {
        Exception? exception;
        ExportResult result = ExportResult.Success;
        var exportedItems = new List<Activity>();
        const string ActivitySourceName = "stackdriver.test";
        var source = new ActivitySource(ActivitySourceName);
        var traceClientMock = new Mock<TraceServiceClient>(MockBehavior.Strict);
        traceClientMock.Setup(x =>
                x.BatchWriteSpans(It.IsAny<BatchWriteSpansRequest>(), It.IsAny<CallSettings>()))
            .Throws(new RpcException(Status.DefaultCancelled))
            .Verifiable($"{nameof(TraceServiceClient.BatchWriteSpans)} was never called");
        var activityExporter = new StackdriverTraceExporter("test", traceClientMock.Object);

        var processor = new BatchActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems));

        for (int i = 0; i < 10; i++)
        {
            using Activity activity = CreateTestActivity();
            processor.OnEnd(activity);
        }

        processor.Shutdown();

        var batch = new Batch<Activity>(exportedItems.ToArray(), exportedItems.Count);
        RunTest(batch);

        void RunTest(Batch<Activity> batch)
        {
            exception = Record.Exception(() =>
            {
                result = activityExporter.Export(batch);
            });
        }

        Assert.Null(exception);
        Assert.StrictEqual(ExportResult.Failure, result);
        traceClientMock.VerifyAll();
    }

    [Fact]
    public void StackdriverExporter_TraceClientDoesNotTrow_ExportResultSuccess()
    {
        Exception? exception;
        ExportResult result = ExportResult.Failure;
        var exportedItems = new List<Activity>();
        const string ActivitySourceName = "stackdriver.test";
        var source = new ActivitySource(ActivitySourceName);
        var traceClientMock = new Mock<TraceServiceClient>(MockBehavior.Strict);
        traceClientMock.Setup(x =>
                x.BatchWriteSpans(It.IsAny<BatchWriteSpansRequest>(), It.IsAny<CallSettings>()))
            .Verifiable($"{nameof(TraceServiceClient.BatchWriteSpans)} was never called");
        var activityExporter = new StackdriverTraceExporter("test", traceClientMock.Object);

        var processor = new BatchActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems));

        for (int i = 0; i < 10; i++)
        {
            using Activity activity = CreateTestActivity();
            processor.OnEnd(activity);
        }

        processor.Shutdown();

        var batch = new Batch<Activity>(exportedItems.ToArray(), exportedItems.Count);
        RunTest(batch);

        void RunTest(Batch<Activity> batch)
        {
            exception = Record.Exception(() =>
            {
                result = activityExporter.Export(batch);
            });
        }

        Assert.Null(exception);
        Assert.StrictEqual(ExportResult.Success, result);
        traceClientMock.VerifyAll();
    }

    internal static Activity CreateTestActivity(
        bool setAttributes = true,
        Dictionary<string, object>? additionalAttributes = null,
        bool addEvents = true,
        bool addLinks = true,
        Resource? resource = null,
        ActivityKind kind = ActivityKind.Client)
    {
        var startTimestamp = DateTime.UtcNow;
        var endTimestamp = startTimestamp.AddSeconds(60);
        var eventTimestamp = DateTime.UtcNow;
        var traceId = ActivityTraceId.CreateFromString("e8ea7e9ac72de94e91fabc613f9686b2".AsSpan());

        var parentSpanId = ActivitySpanId.CreateFromBytes(new byte[] { 12, 23, 34, 45, 56, 67, 78, 89 });

        var attributes = new Dictionary<string, object?>
        {
            { "stringKey", "value" },
            { "longKey", 1L },
            { "longKey2", 1 },
            { "doubleKey", 1D },
            { "doubleKey2", 1F },
            { "boolKey", true },
            { "nullKey", null },
            { "http.url", null },
        };
        if (additionalAttributes != null)
        {
            foreach (var attribute in additionalAttributes)
            {
                attributes.Add(attribute.Key, attribute.Value);
            }
        }

        var events = new List<ActivityEvent>
        {
            new(
                "Event1",
                eventTimestamp,
                new ActivityTagsCollection
                {
                    { "key", "value" },
                }),
            new(
                "Event2",
                eventTimestamp,
                new ActivityTagsCollection
                {
                    { "key", "value" },
                }),
        };

        var linkedSpanId = ActivitySpanId.CreateFromString("888915b6286b9c41".AsSpan());

        var activitySource = new ActivitySource(nameof(CreateTestActivity));

        var tags = setAttributes ?
            attributes.Select(kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value?.ToString()))
            : null;
        var links = addLinks ?
            new[]
            {
                new ActivityLink(new ActivityContext(
                    traceId,
                    linkedSpanId,
                    ActivityTraceFlags.Recorded)),
            }
            : null;

        var activity = activitySource.StartActivity(
            "Name",
            kind,
            parentContext: new ActivityContext(traceId, parentSpanId, ActivityTraceFlags.Recorded),
            tags,
            links,
            startTime: startTimestamp)!;

        if (addEvents)
        {
            foreach (var evnt in events)
            {
                activity.AddEvent(evnt);
            }
        }

        activity.SetEndTime(endTimestamp);
        activity.Stop();

        return activity;
    }
}
