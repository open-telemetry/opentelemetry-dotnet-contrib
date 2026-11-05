// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Tracing;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Instrumentation.GrpcCore.Tests;

public class RpcScopeTests
{
    [Fact]
    public void ConcurrentRequestsAndResponsesDoNotExceedMessageEventLimit()
    {
        const int Limit = RpcScope<FoobarRequest, FoobarResponse>.MaxMessageEvents;

        using var source = new ActivitySource("OpenTelemetry.Instrumentation.GrpcCore.Tests.RpcScope");
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = s => ReferenceEquals(s, source),
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(activityListener);

        using var eventListener = new InMemoryEventListener(GrpcCoreInstrumentationEventSource.Log, EventLevel.Warning);
        using var activity = source.StartActivity("test");
        Assert.NotNull(activity);

        using var scope = new TestRpcScope(activity);

        // Request and response messages are recorded concurrently on duplex calls.
        Parallel.For(
            0,
            Limit * 20,
            i =>
            {
                if (i % 2 == 0)
                {
                    scope.RecordRequest(FoobarService.DefaultRequestMessage);
                }
                else
                {
                    scope.RecordResponse(FoobarService.DefaultResponseMessage);
                }
            });

        Assert.Equal(Limit, activity.Events.Count());

        // The event source is shared, so ignore warnings logged by calls in other tests.
        Assert.Single(eventListener.Events, e => e.EventId == 1 && (string?)e.Payload?[0] == "foobar/method");
    }

    private sealed class TestRpcScope : RpcScope<FoobarRequest, FoobarResponse>
    {
        public TestRpcScope(Activity activity)
            : base("localhost", "foobar/method", recordMessageEvents: true, recordException: false)
        {
            this.SetActivity(activity);
        }
    }
}
