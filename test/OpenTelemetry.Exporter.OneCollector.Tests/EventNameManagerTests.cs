// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Text;
using System.Text.Json;

namespace OpenTelemetry.Exporter.OneCollector.Tests;

public class EventNameManagerTests
{
    public static TheoryData<string, string> UnvalidatedNames()
    {
        var longName = "Long name " + new string('a', 300);

        return new()
        {
            { "My Event", "My Event" },
            { "myEvent!", "MyEvent!" },
            { "x\"}}\n{\"injected\":1", "X\"}}\n{\"injected\":1" },
            { "back\\slash", "Back\\slash" },
            { "tab\tand\u0001control", "Tab\tand\u0001control" },
            { "R\u00e9sum\u00e9.Service", "R\u00e9sum\u00e9.Service" },
            { "\u0122quote", "\u0122quote" },
            { "emoji\U0001F600", "Emoji\U0001F600" },
            { "lone\uD800surrogate", "Lone\uFFFDsurrogate" },
            { "Dispatcher<Order>", "Dispatcher<Order>" },
            { longName, longName },
        };
    }

    [Theory]
    [InlineData("Company.Product")]
    [InlineData("Company")]
    [InlineData("company.product")]
    [InlineData("company99.1product")]
    [InlineData("c")]
    public void ValidEventNamespaceTest(string eventNamespace)
    {
        Assert.True(EventNameManager.IsEventNamespaceValid(eventNamespace));
    }

    [Theory]
    [InlineData("9")]
    [InlineData("Company..Product")]
    [InlineData("Company.")]
    [InlineData(".Company")]
    [InlineData("")]
    [InlineData("Company\n")]
    [InlineData("Company.Product\n")]
    public void InvalidEventNamespaceTest(string eventNamespace)
    {
        Assert.False(EventNameManager.IsEventNamespaceValid(eventNamespace));
    }

    [Theory]
    [InlineData("Opened")]
    [InlineData("closed")]
    [InlineData("c")]
    [InlineData("event9")]
    public void ValidEventNameTest(string eventNamespace)
    {
        Assert.True(EventNameManager.IsEventNameValid(eventNamespace));
    }

    [Theory]
    [InlineData("9")]
    [InlineData("Some.Event")]
    [InlineData("Event.")]
    [InlineData(".Event")]
    [InlineData("")]
    [InlineData("Event\n")]
    public void InvalidEventNameTest(string eventNamespace)
    {
        Assert.False(EventNameManager.IsEventNameValid(eventNamespace));
    }

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
                new("MyNamespace2", "NewEventName2"),
            ]);

        var resolveEventFullName = eventNameManager.ResolveEventFullName("MyNamespace", "MyEventName");

        Assert.Equal(Encoding.ASCII.GetBytes($"\"{(defaultNamespace.Length > 0 ? $"{defaultNamespace}." : string.Empty)}DefaultName\""), resolveEventFullName.EventFullName);
    }

    [Fact]
    public void EventFullNameMappedUsingPassthroughTest()
    {
        var eventNameManager = BuildEventNameManagerWithEventFullNameMappings(
        [
            new("*", "*"),
        ]);

        var resolveEventFullName = eventNameManager.ResolveEventFullName("MyNamespace", "MyEventName");

        Assert.Equal(Encoding.ASCII.GetBytes("\"MyNamespace.MyEventName\""), resolveEventFullName.EventFullName);
    }

    [Theory]
    [MemberData(nameof(UnvalidatedNames))]
    public void OriginalEventNameIsJsonEncodedTest(string eventName, string expectedValue)
    {
        var eventNameManager = BuildEventNameManagerWithDefaultOptions();

        var resolveEventFullName = eventNameManager.ResolveEventFullName("MyNamespace", eventName);

        Assert.Equal(Encoding.ASCII.GetBytes("\"MyNamespace.DefaultName\""), resolveEventFullName.EventFullName);
        Assert.Null(resolveEventFullName.OriginalEventNamespace);
        Assert.Equal(expectedValue, ReadJsonString(resolveEventFullName.OriginalEventName));
    }

    [Theory]
    [MemberData(nameof(UnvalidatedNames))]
    public void OriginalEventNamespaceIsJsonEncodedTest(string eventNamespace, string expectedValue)
    {
        var eventNameManager = BuildEventNameManagerWithDefaultOptions();

        var resolveEventFullName = eventNameManager.ResolveEventFullName(eventNamespace, "MyEvent");

        Assert.Equal(Encoding.ASCII.GetBytes("\"DefaultNamespace.MyEvent\""), resolveEventFullName.EventFullName);
        Assert.Equal(expectedValue, ReadJsonString(resolveEventFullName.OriginalEventNamespace));
        Assert.Null(resolveEventFullName.OriginalEventName);
    }

    [Theory]
    [MemberData(nameof(UnvalidatedNames))]
    public void MappedOriginalEventNamespaceIsJsonEncodedTest(string eventNamespace, string expectedValue)
    {
        var eventNameManager = BuildEventNameManagerWithEventFullNameMappings(
        [
            new("*", "DefaultLogs"),
        ]);

        var resolveEventFullName = eventNameManager.ResolveEventFullName(eventNamespace, null);

        Assert.Equal(Encoding.ASCII.GetBytes("\"DefaultLogs\""), resolveEventFullName.EventFullName);
        Assert.Equal(expectedValue, ReadJsonString(resolveEventFullName.OriginalEventNamespace));
    }

    [Theory]
    [MemberData(nameof(UnvalidatedNames))]
    public void EventFullNameAttributeIsJsonEncodedTest(string eventFullName, string expectedValue)
    {
        var eventNameManager = BuildEventNameManagerWithDefaultOptions();

        var resolveEventFullName = eventNameManager.ResolveEventFullName(eventFullName);

        Assert.Equal(expectedValue, ReadJsonString(resolveEventFullName.EventFullName));
    }

    [Fact]
    public void OriginalEventNameNotReusedForOtherEventsTest()
    {
        var eventNameManager = BuildEventNameManagerWithDefaultOptions();

        var invalid = eventNameManager.ResolveEventFullName("MyNamespace", "Invalid event name");

        Assert.Equal("Invalid event name", ReadJsonString(invalid.OriginalEventName));

        var noEventName = eventNameManager.ResolveEventFullName("MyNamespace", null);

        Assert.Equal(Encoding.ASCII.GetBytes("\"MyNamespace.DefaultName\""), noEventName.EventFullName);
        Assert.Null(noEventName.OriginalEventName);

        var invalidAgain = eventNameManager.ResolveEventFullName("MyNamespace", "Invalid event name");

        Assert.Same(invalid, invalidAgain);
    }

    [Fact]
    public void OriginalEventNameCaseVariantsPreservedTest()
    {
        var eventNameManager = BuildEventNameManagerWithDefaultOptions();

        var first = eventNameManager.ResolveEventFullName("MyNamespace", "Invalid Event");
        var second = eventNameManager.ResolveEventFullName("MyNamespace", "Invalid event");
        var firstAgain = eventNameManager.ResolveEventFullName("MyNamespace", "Invalid Event");
        var secondAgain = eventNameManager.ResolveEventFullName("MyNamespace", "Invalid event");

        Assert.Equal("Invalid Event", ReadJsonString(first.OriginalEventName));
        Assert.Equal("Invalid event", ReadJsonString(second.OriginalEventName));
        Assert.Same(first, firstAgain);
        Assert.Equal("Invalid event", ReadJsonString(secondAgain.OriginalEventName));
    }

    [Fact]
    public void OriginalEventNamespaceCaseVariantsPreservedTest()
    {
        var eventNameManager = BuildEventNameManagerWithDefaultOptions();

        var first = eventNameManager.ResolveEventFullName("R\u00e9sum\u00e9.Service", "MyEvent");
        var second = eventNameManager.ResolveEventFullName("R\u00e9sum\u00e9.SERVICE", "MyEvent");

        Assert.Equal("R\u00e9sum\u00e9.Service", ReadJsonString(first.OriginalEventNamespace));
        Assert.Equal("R\u00e9sum\u00e9.SERVICE", ReadJsonString(second.OriginalEventNamespace));
        Assert.Same(first, eventNameManager.ResolveEventFullName("R\u00e9sum\u00e9.Service", "MyEvent"));
    }

    [Fact]
    public void MappedOriginalEventNamespaceCaseVariantsPreservedTest()
    {
        var eventNameManager = BuildEventNameManagerWithEventFullNameMappings(
        [
            new("*", "DefaultLogs"),
        ]);

        var first = eventNameManager.ResolveEventFullName("MyCompany.Orders", null);
        var second = eventNameManager.ResolveEventFullName("MyCompany.ORDERS", null);

        Assert.Equal("MyCompany.Orders", ReadJsonString(first.OriginalEventNamespace));
        Assert.Equal("MyCompany.ORDERS", ReadJsonString(second.OriginalEventNamespace));
        Assert.Same(first, eventNameManager.ResolveEventFullName("MyCompany.Orders", null));
    }

    [Fact]
    public void ValidEventNameCaseVariantsStillShareCacheEntryTest()
    {
        var eventNameManager = BuildEventNameManagerWithDefaultOptions();

        var first = eventNameManager.ResolveEventFullName("MyNamespace", "MyEvent");
        var second = eventNameManager.ResolveEventFullName("mynamespace", "myevent");

        Assert.Same(first, second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("MyCompany.Orders.Mapped")]
    [InlineData("DefaultLogs")]
    [InlineData("*")]
    [InlineData("MyCompany.Orders.Event")]
    public void WarmCacheMatchesColdCacheForPreservedOriginalsTest(string? defaultMapping)
    {
        (string? EventNamespace, string? EventName)[] inputs =
        [
            ("MyCompany.Orders", "Mapped"),
            ("MyCompany.ORDERS", "MAPPED"),
            ("MyCompany.Orders", "Event"),
            ("MyCompany.ORDERS", "Event"),
            ("MyCompany.Orders", "EVENT"),
            ("MyCompany.Orders", "Mapped"),
            ("MyNamespace", "Invalid Event"),
            ("MyNamespace", "Invalid event"),
            ("R\u00e9sum\u00e9.Service", "MyEvent"),
            ("R\u00e9sum\u00e9.SERVICE", "MyEvent"),
            ("MyCompany.Orders", "Event"),
            ("MyNamespace", "Invalid Event"),
        ];

        var warmEventNameManager = Build();

        foreach (var (eventNamespace, eventName) in inputs)
        {
            AssertWarmMatchesCold(warmEventNameManager, eventNamespace, eventName);
        }

        // Every ordered pair: warm the cache with the first input, then resolve the second.
        (string? EventNamespace, string? EventName)[] pairInputs =
        [
            .. inputs,
            ("MyCompany.Orders", "mapped"),
            ("mycompany.orders", "Event"),
            ("Default.Logs", "Event"),
            ("DefaultLogs", "Event"),
            (" ", "Event"),
            ("MyCompany.Orders", " "),
            (null, "Event"),
            ("MyCompany.Orders", null),
            ("defaultNamespace", "Event"),
            ("DefaultNamespace", "Event"),
            ("MyCompany.Orders", "defaultName"),
            ("MyCompany.Orders", "DefaultName"),
            (" ", "Bad Event"),
            ("defaultNamespace", "Bad Event"),
        ];

        foreach (var first in pairInputs)
        {
            foreach (var second in pairInputs)
            {
                var pairEventNameManager = Build();

                pairEventNameManager.ResolveEventFullName(first.EventNamespace, first.EventName);

                AssertWarmMatchesCold(pairEventNameManager, second.EventNamespace, second.EventName);
            }
        }

        void AssertWarmMatchesCold(EventNameManager warmManager, string? eventNamespace, string? eventName)
        {
            var warm = warmManager.ResolveEventFullName(eventNamespace, eventName);
            var cold = Build().ResolveEventFullName(eventNamespace, eventName);

            Assert.Equal(cold.OriginalEventNamespace, warm.OriginalEventNamespace);
            Assert.Equal(cold.OriginalEventName, warm.OriginalEventName);

            if (warm.OriginalEventNamespace != null || warm.OriginalEventName != null)
            {
                Assert.Equal(cold.EventFullName, warm.EventFullName);
            }
        }

        EventNameManager Build() => defaultMapping == null
            ? BuildEventNameManagerWithDefaultOptions()
            : BuildEventNameManagerWithEventFullNameMappings([new("*", defaultMapping!)]);
    }

    private static string? ReadJsonString(byte[]? jsonValue)
    {
        Assert.NotNull(jsonValue);

        var reader = new Utf8JsonReader(jsonValue);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.String, reader.TokenType);

        var value = reader.GetString();

        Assert.False(reader.Read());

        return value;
    }

    private static EventNameManager BuildEventNameManagerWithDefaultOptions()
    {
        return new EventNameManager("defaultNamespace", "defaultName");
    }

    private static EventNameManager BuildEventNameManagerWithEventFullNameMappings(
        params KeyValuePair<string, string>[] mappings)
    {
        return BuildEventNameManagerWithEventFullNameMappings(
            "defaultNamespace",
            mappings);
    }

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
