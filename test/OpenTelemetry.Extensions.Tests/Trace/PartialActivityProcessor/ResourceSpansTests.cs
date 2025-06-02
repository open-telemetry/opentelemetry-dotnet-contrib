// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Extensions.Trace.PartialActivityProcessor;
using Xunit;

namespace OpenTelemetry.Extensions.Tests.Trace.PartialActivityProcessor;

public class ResourceSpansTests
{
    [Fact]
    public void Constructor_ShouldInitializeScopeSpans()
    {
        var activity = new Activity("TestActivity");
        var signal = TracesData.Signal.Start;

        var resourceSpans = new ResourceSpans(activity, signal);

        Assert.NotNull(resourceSpans.ScopeSpans);
        Assert.Single(resourceSpans.ScopeSpans);
        Assert.IsType<ScopeSpans>(resourceSpans.ScopeSpans[0]);
    }
}
