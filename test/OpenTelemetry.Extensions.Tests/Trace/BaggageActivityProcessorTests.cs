// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.Tests.Trace;

public class BaggageActivityProcessorTests
{
    [Fact]
    public void BaggageActivityProcessor_CanAddAllowAllBaggageKeysPredicate()
    {
        var sourceName = GetTestMethodName();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddBaggageActivityProcessor(BaggageActivityProcessor.AllowAllBaggageKeys)
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
    public void BaggageActivityProcessor_CanUseCustomPredicate()
    {
        var sourceName = GetTestMethodName();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddBaggageActivityProcessor((baggageKey) => baggageKey.StartsWith("key", StringComparison.Ordinal))
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
    public void BaggageActivityProcessor_CanUseRegex()
    {
        var sourceName = GetTestMethodName();

        var regex = new Regex("^mykey", RegexOptions.Compiled);
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddBaggageActivityProcessor(regex.IsMatch)
            .AddSource(sourceName)
            .Build();

        Baggage.SetBaggage("mykey", "value");
        Baggage.SetBaggage("other_key", "other_value");

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Client);
        Assert.NotNull(activity);
        activity.Stop();

        Assert.Contains(activity.Tags, kv => kv.Key == "mykey" && kv.Value == "value");
        Assert.DoesNotContain(activity.Tags, kv => kv.Key == "other_key" && kv.Value == "other_value");
    }

    [Fact]
    public void BaggageActivityProcessor_PredicateThrows_DoesNothing()
    {
        var sourceName = GetTestMethodName();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddBaggageActivityProcessor(_ => throw new Exception("Predicate throws an exception."))
            .AddSource(sourceName)
            .Build();

        Baggage.SetBaggage("key", "value");

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Server);
        Assert.NotNull(activity);
        activity.Stop();

        Assert.DoesNotContain(activity.Tags, kv => kv.Key == "key" && kv.Value == "value");
    }

    [Fact]
    public void BaggageActivityProcessor_PredicateThrows_OnlyDropsEntriesThatThrow()
    {
        var sourceName = GetTestMethodName();

        // First call to predicate should not throw, second call should.
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddBaggageActivityProcessor(key =>
            {
                return key == "key" ? throw new Exception("Predicate throws an exception.") : true;
            })
            .AddSource(sourceName)
            .Build();

        Baggage.SetBaggage("key", "value");
        Baggage.SetBaggage("other_key", "other_value");
        Baggage.SetBaggage("another_key", "another_value");

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Server);
        Assert.NotNull(activity);
        activity.Stop();

        // Only keys that do not throw should be added.
        Assert.DoesNotContain(activity.Tags, kv => kv.Key == "key" && kv.Value == "value");
        Assert.Contains(activity.Tags, kv => kv.Key == "other_key" && kv.Value == "other_value");
        Assert.Contains(activity.Tags, kv => kv.Key == "another_key" && kv.Value == "another_value");
    }

    [Fact]
    public void BaggageActivityProcessor_AddsBaggageAfterTagsSetWhileRunning()
    {
        var sourceName = GetTestMethodName();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddBaggageActivityProcessor(BaggageActivityProcessor.AllowAllBaggageKeys)
            .AddSource(sourceName)
            .Build();

        Baggage.SetBaggage("key", "baggage_value");
        Baggage.SetBaggage("other_key", "other_value");

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Server);
        Assert.NotNull(activity);

        // Baggage which becomes current after the activity started is not added.
        Baggage.SetBaggage("late_key", "late_value");

        activity.SetTag("tag", "tag_value");
        activity.SetTag("key", "tag_value");

        // Baggage is not added while the activity is running.
        Assert.Null(activity.GetTagItem("other_key"));

        activity.Stop();

        // The tags set by the application come first and are not overwritten by baggage.
        Assert.Equal(
            [new("tag", "tag_value"), new("key", "tag_value"), new("other_key", "other_value")],
            activity.TagObjects);
    }

    [Fact]
    public void BaggageActivityProcessor_DoesNotReplaceTagsActivityWasCreatedWith()
    {
        var sourceName = GetTestMethodName();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddBaggageActivityProcessor(BaggageActivityProcessor.AllowAllBaggageKeys)
            .AddSource(sourceName)
            .Build();

        Baggage.SetBaggage("tenant", "attacker");
        Baggage.SetBaggage("other_key", "other_value");

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity(
            "name",
            ActivityKind.Internal,
            default(ActivityContext),
            [new("tenant", "contoso")]);
        Assert.NotNull(activity);
        activity.Stop();

        Assert.Equal(
            [new("tenant", "contoso"), new("other_key", "other_value")],
            activity.TagObjects);
    }

    [Fact]
    public void BaggageActivityProcessor_DoesNotReplaceTagsWithNullValues()
    {
        var sourceName = GetTestMethodName();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddBaggageActivityProcessor(BaggageActivityProcessor.AllowAllBaggageKeys)
            .AddSource(sourceName)
            .Build();

        Baggage.SetBaggage("null_key", "baggage_value");
        Baggage.SetBaggage("duplicate_key", "baggage_value");
        Baggage.SetBaggage("other_key", "other_value");

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Server);
        Assert.NotNull(activity);

        activity.AddTag("null_key", null);
        activity.AddTag("duplicate_key", null);
        activity.AddTag("duplicate_key", "tag_value");
        activity.Stop();

        Assert.Equal(
            [new("null_key", null), new("duplicate_key", null), new("duplicate_key", "tag_value"), new("other_key", "other_value")],
            activity.TagObjects);
    }

    [Fact]
    public void BaggageActivityProcessor_MultipleInstancesWithDifferentPredicatesEachAddTheirBaggage()
    {
        var sourceName = GetTestMethodName();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddBaggageActivityProcessor(key => key.StartsWith("first", StringComparison.Ordinal))
            .AddBaggageActivityProcessor(key => key.StartsWith("second", StringComparison.Ordinal))
            .AddSource(sourceName)
            .Build();

        Baggage.SetBaggage("first_key", "first_value");
        Baggage.SetBaggage("second_key", "second_value");
        Baggage.SetBaggage("third_key", "third_value");

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Server);
        Assert.NotNull(activity);
        activity.Stop();

        Assert.Equal(2, activity.TagObjects.Count());
        Assert.Contains(activity.TagObjects, kv => kv.Key == "first_key" && (string?)kv.Value == "first_value");
        Assert.Contains(activity.TagObjects, kv => kv.Key == "second_key" && (string?)kv.Value == "second_value");
    }

    [Fact]
    public void BaggageActivityProcessor_DoesNotDisplaceTagsWhenAttributeLimitIsReached()
    {
        var sourceName = GetTestMethodName();
        var exported = new List<KeyValuePair<string, object?>>();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddBaggageActivityProcessor(key => key.StartsWith("my-key", StringComparison.Ordinal))
            .AddSource(sourceName)
            .AddProcessor(new TagSnapshotProcessor(exported))
            .Build();

        for (var i = 0; i < 128; i++)
        {
            Baggage.SetBaggage($"my-key{i}", "1");
        }

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Server);
        Assert.NotNull(activity);
        activity.SetTag("enduser.id", "mallory");
        activity.SetTag("order.id", "17");
        activity.Stop();

        // An exporter which keeps only the first 128 tags still sees the tags set by the application.
        var firstTags = exported.Take(128).ToList();
        Assert.Contains(firstTags, kv => kv.Key == "enduser.id" && (string?)kv.Value == "mallory");
        Assert.Contains(firstTags, kv => kv.Key == "order.id" && (string?)kv.Value == "17");
        Assert.Equal(130, exported.Count);
    }

    [Fact]
    public void BaggageActivityProcessor_AddedAfterEnrichingProcessorAndBeforeExporter_DoesNotDisplaceEnrichedTags()
    {
        var sourceName = GetTestMethodName();
        var exported = new List<KeyValuePair<string, object?>>();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .AddProcessor(new OnEndTagProcessor(("enriched.one", "1"), ("enriched.two", "2")))
            .AddBaggageActivityProcessor(key => key.StartsWith("my-key", StringComparison.Ordinal))
            .AddProcessor(new TagSnapshotProcessor(exported))
            .Build();

        for (var i = 0; i < 128; i++)
        {
            Baggage.SetBaggage($"my-key{i}", "1");
        }

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Server);
        Assert.NotNull(activity);
        activity.SetTag("app.tag", "value");
        activity.Stop();

        // An exporter which keeps only the first 128 tags still sees the tags
        // set by the application and those added by the processor before baggage.
        var firstTags = exported.Take(128).ToList();
        Assert.Contains(firstTags, kv => kv.Key == "app.tag" && (string?)kv.Value == "value");
        Assert.Contains(firstTags, kv => kv.Key == "enriched.one" && (string?)kv.Value == "1");
        Assert.Contains(firstTags, kv => kv.Key == "enriched.two" && (string?)kv.Value == "2");
        Assert.Equal(131, exported.Count);
    }

    private static string GetTestMethodName([CallerMemberName] string callingMethodName = "")
    {
        return callingMethodName;
    }

    private sealed class OnEndTagProcessor(params (string Key, string Value)[] tags) : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data)
        {
            foreach (var (key, value) in tags)
            {
                data.SetTag(key, value);
            }
        }
    }

    private sealed class TagSnapshotProcessor(List<KeyValuePair<string, object?>> tags) : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data)
        {
            tags.AddRange(data.TagObjects);
        }
    }
}
