// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Tests.Trace;

public class BaggageSpanProcessorTests
{
    [Fact]
    public void BaggageSpanProcessor_CanAddAlloAllBaggageKeysPredicate()
    {
        var activityProcessor = new TestActivityProcessor();
        var sourceName = GetTestMethodName();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor)
            .AddBaggageActivityProcessor("*")
            .AddSource(sourceName)
            .Build();

        Baggage.SetBaggage("key", "value");
        Baggage.SetBaggage("other_key", "other_value");

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Server);
        Assert.NotNull(activity);
        activity.Stop();

        Assert.Contains(activity.Tags, kv => kv.Key == "key" && kv.Value == "value");
        Assert.Contains(activity.Tags, kv => kv.Key == "other_key" && kv.Value == "other_value");
    }

    [Fact]
    public void BaggageSpanProcessor_CanUseCustomPredicate()
    {
        var activityProcessor = new TestActivityProcessor();
        var sourceName = GetTestMethodName();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor)
            .AddBaggageActivityProcessor((baggageKey) => baggageKey.StartsWith("key", System.StringComparison.Ordinal))
            .AddSource(sourceName)
            .Build();

        Baggage.SetBaggage("key", "value");
        Baggage.SetBaggage("other_key", "other_value");

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Client);
        Assert.NotNull(activity);
        activity.Stop();

        Assert.Contains(activity.Tags, kv => kv.Key == "key" && kv.Value == "value");
        Assert.DoesNotContain(activity.Tags, kv => kv.Key == "other_key" && kv.Value == "other_value");
    }

    [Fact]
    public void BaggageSpanProcessor_CanUseRegex()
    {
        var activityProcessor = new TestActivityProcessor();
        var sourceName = GetTestMethodName();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor)
            .AddBaggageActivityProcessor("^other.*")
            .AddSource(sourceName)
            .Build();

        Baggage.SetBaggage("key", "value");
        Baggage.SetBaggage("other_key", "other_value");

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Client);
        Assert.NotNull(activity);
        activity.Stop();

        Assert.DoesNotContain(activity.Tags, kv => kv.Key == "key" && kv.Value == "value");
        Assert.Contains(activity.Tags, kv => kv.Key == "other_key" && kv.Value == "other_value");
    }

    private static string GetTestMethodName([CallerMemberName] string callingMethodName = "")
    {
        return callingMethodName;
    }
}
