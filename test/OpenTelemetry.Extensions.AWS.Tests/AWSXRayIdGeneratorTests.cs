// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.AWS.Tests;

public class AWSXRayIdGeneratorTests
{
    [Fact]
    public void TestGenerateTraceIdForRootNode()
    {
        var activity = new Activity("Test");
        var originalTraceId = activity.TraceId;
        var originalParentSpanId = activity.ParentSpanId;
        var originalTraceFlag = activity.ActivityTraceFlags;

        using (Sdk.CreateTracerProviderBuilder().AddXRayTraceId().Build())
        {
            activity.Start();

            Assert.NotEqual(originalTraceId, activity.TraceId);
            Assert.Equal(originalParentSpanId, activity.ParentSpanId);
            Assert.Equal("0000000000000000", activity.ParentSpanId.ToHexString());
            Assert.Equal(originalTraceFlag, activity.ActivityTraceFlags);
        }
    }

    [Fact]
    public void TestGenerateTraceIdForNonRootNode()
    {
        var activity = new Activity("Test");
        var traceId = ActivityTraceId.CreateFromString("12345678901234567890123456789012".AsSpan());
        var parentId = ActivitySpanId.CreateFromString("1234567890123456".AsSpan());
        activity.SetParentId(traceId, parentId, ActivityTraceFlags.Recorded);

        using (Sdk.CreateTracerProviderBuilder().AddXRayTraceId().Build())
        {
            activity.Start();

            Assert.Equal("12345678901234567890123456789012", activity.TraceId.ToHexString());
            Assert.Equal("1234567890123456", activity.ParentSpanId.ToHexString());
            Assert.Equal(ActivityTraceFlags.Recorded, activity.ActivityTraceFlags);
        }
    }

    [Fact]
    public void TestGenerateTraceIdForNonRootNodeNotSampled()
    {
        var activity = new Activity("Test");
        var traceId = ActivityTraceId.CreateFromString("12345678901234567890123456789012".AsSpan());
        var parentId = ActivitySpanId.CreateFromString("1234567890123456".AsSpan());
        activity.SetParentId(traceId, parentId, ActivityTraceFlags.None);

        using (Sdk.CreateTracerProviderBuilder().AddXRayTraceId().Build())
        {
            activity.Start();

            Assert.Equal("12345678901234567890123456789012", activity.TraceId.ToHexString());
            Assert.Equal("1234567890123456", activity.ParentSpanId.ToHexString());
            Assert.Equal(ActivityTraceFlags.None, activity.ActivityTraceFlags);
        }
    }

    [Fact]
    public void TestGenerateTraceIdForRootNodeUsingActivitySourceWithTraceIdBasedSamplerOn()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        using (Sdk.CreateTracerProviderBuilder()
                   .AddXRayTraceIdWithSampler(new TraceIdRatioBasedSampler(1.0))
                   .AddSource("TestTraceIdBasedSamplerOn")
                   .SetSampler(new TraceIdRatioBasedSampler(1.0))
                   .Build())
#pragma warning restore CS0618 // Type or member is obsolete
        {
            using var activitySource = new ActivitySource("TestTraceIdBasedSamplerOn");
            using var activity = activitySource.StartActivity("RootActivity", ActivityKind.Internal);

            Assert.Equal(ActivityTraceFlags.Recorded, activity?.ActivityTraceFlags);
        }
    }

    [Fact]
    public void TestGenerateTraceIdForRootNodeUsingActivitySourceWithTraceIdBasedSamplerOff()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        using (Sdk.CreateTracerProviderBuilder()
                   .AddXRayTraceIdWithSampler(new TraceIdRatioBasedSampler(0.0))
                   .AddSource("TestTraceIdBasedSamplerOff")
                   .SetSampler(new TraceIdRatioBasedSampler(0.0))
                   .Build())
#pragma warning restore CS0618 // Type or member is obsolete
        {
            using var activitySource = new ActivitySource("TestTraceIdBasedSamplerOff");
            using var activity = activitySource.StartActivity("RootActivity", ActivityKind.Internal);

            Assert.Equal(ActivityTraceFlags.None, activity?.ActivityTraceFlags);
        }
    }

#if NETFRAMEWORK
    [Fact(Skip = "https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/1615")]
#else
    [Fact]
#endif
    public void AddXRayTraceId_WithActivitySource_SetsXRayCompatibleTraceIdWithoutExceptions()
    {
        Exception? exception = null;

        void Handler(object? sender, FirstChanceExceptionEventArgs args)
        {
            if (args.Exception is InvalidOperationException ex &&
                ex.Message.Contains("parent", StringComparison.OrdinalIgnoreCase))
            {
                exception = args.Exception;
            }
        }

        AppDomain.CurrentDomain.FirstChanceException += Handler;

        try
        {
            ActivityTraceId capturedTraceId = default;

            using (Sdk.CreateTracerProviderBuilder()
                       .AddXRayTraceId()
                       .AddSource("TestXRayRegressionSource")
                       .Build())
            {
                using var activitySource = new ActivitySource("TestXRayRegressionSource");
                using var activity = activitySource.StartActivity("TestRootActivity");

                Assert.NotNull(activity);

                capturedTraceId = activity.TraceId;
            }

            Assert.Null(exception);

            // Verify the trace ID is X-Ray compatible:
            // the first 8 hex characters encode a big-endian Unix timestamp (seconds since epoch).
            var traceIdHex = capturedTraceId.ToHexString();
            var timestampSeconds = Convert.ToUInt32(traceIdHex.Substring(0, 8), 16);
            var nowSeconds = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            Assert.True(
                Math.Abs((int)(nowSeconds - timestampSeconds)) < 60,
                $"Expected an X-Ray compatible trace ID whose first 8 hex digits are a recent Unix timestamp. Value: {traceIdHex}");
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= Handler;
        }
    }
}
