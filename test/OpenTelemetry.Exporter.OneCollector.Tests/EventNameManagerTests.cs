// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Text;
using System.Text.Json;

namespace OpenTelemetry.Exporter.OneCollector.Tests;

public class EventNameManagerTests
{
    [Theory]
    [InlineData("Company.Product")]
    [InlineData("Company")]
    [InlineData("company.product")]
    [InlineData("company99.1product")]
    [InlineData("c")]
    public void ValidEventNamespaceTest(string eventNamespace)
        => Assert.True(EventNameManager.IsEventNamespaceValid(eventNamespace));

    [Theory]
    [InlineData("9")]
    [InlineData("Company..Product")]
    [InlineData("Company.")]
    [InlineData(".Company")]
    [InlineData("")]
    public void InvalidEventNamespaceTest(string eventNamespace)
        => Assert.False(EventNameManager.IsEventNamespaceValid(eventNamespace));

    [Theory]
    [InlineData("Opened")]
    [InlineData("closed")]
    [InlineData("c")]
    [InlineData("event9")]
    public void ValidEventNameTest(string eventNamespace)
        => Assert.True(EventNameManager.IsEventNameValid(eventNamespace));

    [Theory]
    [InlineData("9")]
    [InlineData("Some.Event")]
    [InlineData("Event.")]
    [InlineData(".Event")]
    [InlineData("")]
    public void InvalidEventNameTest(string eventNamespace)
        => Assert.False(EventNameManager.IsEventNameValid(eventNamespace));

    [Theory]
    [InlineData(null, null, "DefaultNamespace.DefaultName")]
    [InlineData("myNamespace", null, "MyNamespace.DefaultName")]
    [InlineData(null, "myEvent", "DefaultNamespace.MyEvent")]
    [InlineData("", " ", "DefaultNamespace.DefaultName")]
    [InlineData("9", "[]", "DefaultNamespace.DefaultName")]
    public void DefaultEventNamespaceAndNameUsedToGenerateFullNameTest(string? eventNamespace, string? eventName, string expectedEventFullName)
    {
        var eventNameManager = BuildEventNameManagerWithDefaultOptions();

        var resolveEventFullName = eventNameManager.ResolveEventFullName(eventNamespace, eventName);

        Assert.Equal(Encoding.ASCII.GetBytes($"\"{expectedEventFullName}\""), resolveEventFullName.EventFullName);
    }

    [Fact]
    public void DefaultEventNamespaceAndNameUsedToGenerateFullNameLengthTest()
    {
        var eventNameManager = BuildEventNameManagerWithDefaultOptions();

        var resolveEventFullName = eventNameManager.ResolveEventFullName("N", "N");

        Assert.Equal(Encoding.ASCII.GetBytes("\"DefaultNamespace.DefaultName\""), resolveEventFullName.EventFullName);

        resolveEventFullName = eventNameManager.ResolveEventFullName(new string('N', 99), "N");

        Assert.Equal(Encoding.ASCII.GetBytes("\"DefaultNamespace.DefaultName\""), resolveEventFullName.EventFullName);

        resolveEventFullName = eventNameManager.ResolveEventFullName("N", new string('N', 99));

        Assert.Equal(Encoding.ASCII.GetBytes("\"DefaultNamespace.DefaultName\""), resolveEventFullName.EventFullName);
    }

    [Fact]
    public void EventNameCacheTest()
    {
        var eventNameManager = BuildEventNameManagerWithDefaultOptions();

        Assert.Empty(eventNameManager.EventNamespaceCache);

        eventNameManager.ResolveEventFullName("Test", "Test");

        Assert.Single(eventNameManager.EventNamespaceCache);
        Assert.Single((eventNameManager.EventNamespaceCache["Test"] as Hashtable)!);

        eventNameManager.ResolveEventFullName("test", "test");

        Assert.Single(eventNameManager.EventNamespaceCache);
        Assert.Single((eventNameManager.EventNamespaceCache["Test"] as Hashtable)!);
    }

    [Fact]
    public void EventFullNameCacheIsBoundedTest()
    {
        var eventNameManager = BuildEventNameManagerWithDefaultOptions();

        var numberOfNames = EventNameManager.MaxNumberOfCachedEventFullNames + 100;

        for (var i = 0; i < numberOfNames; i++)
        {
            var eventFullName = $"Event_{i}";

            var resolved = eventNameManager.ResolveEventFullName(eventFullName);

            // Resolution keeps working correctly once the cache is full, it is just no
            // longer memoized.
            Assert.Equal(
                Encoding.ASCII.GetBytes($"\"{eventFullName}\""),
                resolved.EventFullName);
        }

        Assert.Equal(EventNameManager.MaxNumberOfCachedEventFullNames, eventNameManager.EventFullNameCache.Count);
    }

    [Fact]
    public void EventNamespaceCacheIsBoundedTest()
    {
        var eventNameManager = BuildEventNameManagerWithDefaultOptions();

        var numberOfNamespaces = EventNameManager.MaxNumberOfCachedEventNamespaces + 100;

        for (var i = 0; i < numberOfNamespaces; i++)
        {
            var resolved = eventNameManager.ResolveEventFullName($"Namespace{i}", "MyEvent");

            Assert.Equal(
                Encoding.ASCII.GetBytes($"\"Namespace{i}.MyEvent\""),
                resolved.EventFullName);
        }

        Assert.Equal(EventNameManager.MaxNumberOfCachedEventNamespaces, eventNameManager.EventNamespaceCache.Count);
    }

    [Fact]
    public void EventNameCacheIsBoundedAcrossNamespacesTest()
    {
        var eventNameManager = BuildEventNameManagerWithDefaultOptions();

        // Spread the names over a handful of namespaces so that the limit has to be applied
        // across all of them rather than per namespace.
        var numberOfNames = EventNameManager.MaxNumberOfCachedEventNames + 100;

        for (var i = 0; i < numberOfNames; i++)
        {
            var resolved = eventNameManager.ResolveEventFullName($"Namespace{i % 4}", $"Event{i}");

            Assert.Equal(
                Encoding.ASCII.GetBytes($"\"Namespace{i % 4}.Event{i}\""),
                resolved.EventFullName);
        }

        Assert.Equal(EventNameManager.MaxNumberOfCachedEventNames, eventNameManager.CachedEventNameCount);

        var cachedEventNames = 0;
        foreach (Hashtable eventNameCache in eventNameManager.EventNamespaceCache.Values)
        {
            cachedEventNames += eventNameCache.Count;
        }

        Assert.Equal(EventNameManager.MaxNumberOfCachedEventNames, cachedEventNames);
    }

    [Theory]
    [InlineData(EventNameManager.MinimumEventFullNameLength - 1, false)] // Below the minimum length is rejected.
    [InlineData(EventNameManager.MinimumEventFullNameLength, true)]
    [InlineData(EventNameManager.MaximumEventFullNameLength, true)]
    [InlineData(EventNameManager.MaximumEventFullNameLength + 1, false)] // Above the maximum length is rejected.
    [InlineData(129, false)] // Longer than the buffer BuildEventFullName writes into.
    public void ResolveEventFullNameSingleArgumentBoundsLength(int length, bool expectKept)
    {
        var eventNameManager = BuildEventNameManagerWithDefaultOptions();

        var resolved = eventNameManager.ResolveEventFullName(new string('A', length));

        if (expectKept)
        {
            Assert.Equal(Encoding.ASCII.GetBytes($"\"{new string('A', length)}\""), resolved.EventFullName);
        }
        else
        {
            Assert.Equal(Encoding.ASCII.GetBytes("\"DefaultNamespace.DefaultName\""), resolved.EventFullName);
        }
    }

    [Theory]
    [InlineData("A\",\"injected\":\"value")]
    [InlineData("name\"with\"quotes")]
    [InlineData("name\\with\\backslashes")]
    [InlineData("name with spaces")]
    [InlineData("nameĢwithĢnonAscii")] // Chars are truncated to bytes, and 'Ģ' truncates to '"'.
    [InlineData("name\nwith\nnewlines")]
    [InlineData("name{with}braces")]
    public void ResolveEventFullNameSingleArgumentRejectsUnsafeCharacters(string payload)
    {
        var eventNameManager = BuildEventNameManagerWithDefaultOptions();

        var resolved = eventNameManager.ResolveEventFullName(payload);

        Assert.Equal(
            Encoding.ASCII.GetBytes("\"DefaultNamespace.DefaultName\""),
            resolved.EventFullName);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("name");
            writer.WriteRawValue(resolved.EventFullName, skipInputValidation: true);
            writer.WriteEndObject();
        }

        var json = Encoding.UTF8.GetString(stream.ToArray());
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("DefaultNamespace.DefaultName", doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void ResolveEventFullNameSingleArgumentAllowsSafeCharacters()
    {
        var eventNameManager = BuildEventNameManagerWithDefaultOptions();

        var resolved = eventNameManager.ResolveEventFullName("Company_Product_EventName");

        Assert.Equal(
            Encoding.ASCII.GetBytes("\"Company_Product_EventName\""),
            resolved.EventFullName);
    }

    [Fact]
    public void ResolveEventFullNameReportsUnvalidatedOriginalEventNamespace()
    {
        var eventNameManager = BuildEventNameManagerWithDefaultOptions();

        // An invalid event namespace is replaced by the default one and the original is
        // reported separately, unvalidated and without a length limit. "9..." is invalid
        // because a namespace has to start with a letter; in practice any category name
        // containing a character outside [A-Za-z0-9.] (for example the '+' in a nested type
        // name) is enough.
        var invalidEventNamespace = new string('9', 129);

        Assert.False(EventNameManager.IsEventNamespaceValid(invalidEventNamespace));

        var resolved = eventNameManager.ResolveEventFullName(invalidEventNamespace, "MyEvent");

        Assert.Equal(Encoding.ASCII.GetBytes("\"DefaultNamespace.MyEvent\""), resolved.EventFullName);
        Assert.Equal(invalidEventNamespace, resolved.OriginalEventNamespace);
    }

    [Fact]
    public void ResolveEventFullNameReportsUnvalidatedOriginalEventName()
    {
        var eventNameManager = BuildEventNameManagerWithDefaultOptions();

        var invalidEventName = new string('9', 129);

        Assert.False(EventNameManager.IsEventNameValid(invalidEventName));

        var resolved = eventNameManager.ResolveEventFullName("MyNamespace", invalidEventName);

        Assert.Equal(Encoding.ASCII.GetBytes("\"MyNamespace.DefaultName\""), resolved.EventFullName);
        Assert.Equal(invalidEventName, resolved.OriginalEventName);
    }

    [Fact]
    public void EventFullNameCacheTest()
    {
        var eventNameManager = BuildEventNameManagerWithDefaultOptions();

        Assert.Empty(eventNameManager.EventFullNameCache);

        eventNameManager.ResolveEventFullName("Company_Product_EventName");

        Assert.Single(eventNameManager.EventFullNameCache);

        eventNameManager.ResolveEventFullName("company_product_eventName");

        Assert.Single(eventNameManager.EventFullNameCache);
    }

    [Fact]
    public void EventFullNameMappedWhenEventNamespaceMatchesTest()
    {
        var eventNameManager = BuildEventNameManagerWithEventFullNameMappings(
            new("*", "WildcardEventName"),
            new("MyNamespace", "NewEventName1"),
            new("mynamespace.match.in.full.MyEventName", "NewEventName2"));

        var resolveEventFullName = eventNameManager.ResolveEventFullName("MyNamespace.Match.In.Full", "MyEventName");

        Assert.Equal(Encoding.ASCII.GetBytes("\"NewEventName2\""), resolveEventFullName.EventFullName);
    }

    [Fact]
    public void EventFullNameMappedWhenEventNamespaceStartsWithPrefixTest()
    {
        var eventNameManager = BuildEventNameManagerWithEventFullNameMappings(
            new("*", "WildcardEventName"),
            new("MyNamespace", "NewEventName1"),
            new("MyNamespace.NonMatch", "NewEventName2"),
            new("MyNamespace.MyChild", "NewEventName3"),
            new("mynamespace.mychild.namesp", "NewEventName4"));

        var resolveEventFullName = eventNameManager.ResolveEventFullName("MyNamespace.MyChild.Namespace", "MyEventName");

        Assert.Equal(Encoding.ASCII.GetBytes("\"NewEventName4\""), resolveEventFullName.EventFullName);
    }

    [Fact]
    public void EventFullNameMappedUsingDefaultRuleTest()
    {
        var eventNameManager = BuildEventNameManagerWithEventFullNameMappings(
            new("MyNamespace1", "NewEventName1"),
            new("MyNamespace2", "NewEventName2"),
            new("*", "defaultEventName"));

        var resolveEventFullName = eventNameManager.ResolveEventFullName("MyNamespace", "MyEventName");

        Assert.Equal(Encoding.ASCII.GetBytes("\"DefaultEventName\""), resolveEventFullName.EventFullName);
    }

    [Theory]
    [InlineData("DefaultNamespace")]
    [InlineData("")]
    public void EventFullNameMappedUsingDefaultsWhenNoDefaultRuleDefinedTest(string defaultNamespace)
    {
        var eventNameManager = BuildEventNameManagerWithEventFullNameMappings(
            defaultNamespace,
            [
                new("MyNamespace1", "NewEventName1"),
                new("MyNamespace2", "NewEventName2")
            ]);

        var resolveEventFullName = eventNameManager.ResolveEventFullName("MyNamespace", "MyEventName");

        Assert.Equal(Encoding.ASCII.GetBytes($"\"{(defaultNamespace.Length > 0 ? $"{defaultNamespace}." : string.Empty)}DefaultName\""), resolveEventFullName.EventFullName);
    }

    [Fact]
    public void EventFullNameMappedUsingPassthroughTest()
    {
        var eventNameManager = BuildEventNameManagerWithEventFullNameMappings(
        [
            new("*", "*")
        ]);

        var resolveEventFullName = eventNameManager.ResolveEventFullName("MyNamespace", "MyEventName");

        Assert.Equal(Encoding.ASCII.GetBytes("\"MyNamespace.MyEventName\""), resolveEventFullName.EventFullName);
    }

    private static EventNameManager BuildEventNameManagerWithDefaultOptions()
        => new("defaultNamespace", "defaultName");

    private static EventNameManager BuildEventNameManagerWithEventFullNameMappings(
        params KeyValuePair<string, string>[] mappings) =>
        BuildEventNameManagerWithEventFullNameMappings(
            "defaultNamespace",
            mappings);

    private static EventNameManager BuildEventNameManagerWithEventFullNameMappings(
        string defaultNamespace,
        KeyValuePair<string, string>[] mappings)
    {
        var options = new OneCollectorLogExporterOptions()
        {
            ConnectionString = "InstrumentationKey=token-extrainformation",
            EventFullNameMappings = mappings.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value),
        };

        options.Validate();

        return new EventNameManager(
            defaultNamespace,
            "defaultName",
            eventFullNameMappings: options.ParsedEventFullNameMappings);
    }
}
