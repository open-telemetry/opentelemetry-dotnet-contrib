// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Extensions.Trace.PartialActivityProcessor;
using Xunit;

namespace OpenTelemetry.Extensions.Tests.Trace.PartialActivityProcessor;

public class ScopeSpansTests
{
    [Fact]
    public void Constructor_ShouldInitializeScopeAndSpans()
    {
        var activity = new Activity("TestActivity");
        var signal = TracesData.Signal.Heartbeat;

        var scopeSpans = new ScopeSpans(activity, signal);

        Assert.NotNull(scopeSpans.Scope);
        Assert.NotNull(scopeSpans.Spans);
        Assert.Single(scopeSpans.Spans);
    }

    [Fact]
    public void Scope_ShouldBeSettable()
    {
        var activity = new Activity("TestActivity");
        var signal = TracesData.Signal.Heartbeat;
        var scopeSpans = new ScopeSpans(activity, signal);
        var newScope = new InstrumentationScope(activity);

        scopeSpans.Scope = newScope;

        Assert.Equal(newScope, scopeSpans.Scope);
    }
}
