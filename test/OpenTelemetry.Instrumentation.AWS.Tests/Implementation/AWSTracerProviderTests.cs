// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.AWS;
using OpenTelemetry.Instrumentation.AWS.Implementation.Tracing;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWS.Tests.Implementation;

public class AWSTracerProviderTests
{
    [Fact]
    public void GetTracer_AfterDispose_ReturnsUsableHandleForTheSameScope()
    {
        const string Scope = "OpenTelemetry.Instrumentation.AWS.Tests.ReusedTracer";
        const string SpanName = "reused_span";

        using var listener = new ActivityListener
        {
            ShouldListenTo = activitySource => activitySource.Name == Scope,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(listener);

        var tracerProvider = new AWSTracerProvider(AWSSemanticConventions.DefaultSemanticConventionVersion);
        var disposedTracer = tracerProvider.GetTracer(Scope);

        disposedTracer.Dispose();

        var reusedTracer = tracerProvider.GetTracer(Scope);
        using var span = reusedTracer.CreateSpan(SpanName);

        Assert.NotSame(disposedTracer, reusedTracer);
        Assert.Equal(SpanName, Activity.Current?.DisplayName);
    }
}
