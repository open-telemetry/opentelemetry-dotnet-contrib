// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Moq;
using Moq.Protected;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Tests.Trace;

public class AutoFlushActivityProcessorTests
{
    [Fact(Skip = "Unstable")]
    public void AutoFlushActivityProcessor_FlushAfterLocalServerSideRootSpans_EndMatchingSpan_Flush()
    {
        var mockExporting = new Mock<BaseProcessor<Activity>>();
        var sourceName = GetTestMethodName();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(mockExporting.Object)
            .AddAutoFlushActivityProcessor(a => a.Parent == null && (a.Kind == ActivityKind.Server || a.Kind == ActivityKind.Consumer), 5000)
            .AddSource(sourceName)
            .Build();

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Server);
        Assert.NotNull(activity);
        activity.Stop();

        mockExporting.Protected().Verify("OnForceFlush", Times.Once(), 5_000);
    }

    [Fact]
    public void AutoFlushActivityProcessor_FlushAfterLocalServerSideRootSpans_EndNonMatchingSpan_DoesNothing()
    {
        var mockExporting = new Mock<BaseProcessor<Activity>>();
        var sourceName = GetTestMethodName();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(mockExporting.Object)
            .AddAutoFlushActivityProcessor(a => a.Parent == null && (a.Kind == ActivityKind.Server || a.Kind == ActivityKind.Consumer))
            .AddSource(sourceName)
            .Build();

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Client);
        Assert.NotNull(activity);
        activity.Stop();

        mockExporting.Protected().Verify("OnForceFlush", Times.Never(), It.IsAny<int>());
    }

    [Fact]
    public void AutoFlushActivityProcessor_PredicateThrows_DoesNothing()
    {
        var mockExporting = new Mock<BaseProcessor<Activity>>();
        var sourceName = GetTestMethodName();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(mockExporting.Object)
            .AddAutoFlushActivityProcessor(_ => throw new Exception("Predicate throws an exception."))
            .AddSource(sourceName)
            .Build();

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Server);
        Assert.NotNull(activity);
        activity.Stop();

        mockExporting.Protected().Verify("OnForceFlush", Times.Never(), 5_000);
    }

    private static string GetTestMethodName([CallerMemberName] string callingMethodName = "")
    {
        return callingMethodName;
    }
}
