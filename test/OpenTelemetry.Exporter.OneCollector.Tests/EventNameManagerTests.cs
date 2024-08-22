// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Text;
using Xunit;

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
    {
        Assert.True(EventNameManager.IsEventNamespaceValid(eventNamespace));
    }

    [Theory]
    [InlineData("9")]
    [InlineData("Company..Product")]
    [InlineData("Company.")]
    [InlineData(".Company")]
    [InlineData("")]
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
    public void InvalidEventNameTest(string eventNamespace)
    {
        Assert.False(EventNameManager.IsEventNameValid(eventNamespace));
    }

    [Fact]
    public void DefaultEventFullNameLengthTest()
    {
        Assert.Throws<ArgumentException>(() => CreateDefaultEventNameManager("N", "N"));
        Assert.Throws<ArgumentException>(() => CreateDefaultEventNameManager(new string('N', 99), "N"));
        Assert.Throws<ArgumentException>(() => CreateDefaultEventNameManager("N", new string('N', 99)));
    }

    [Theory]
    [InlineData(null, null, "DefaultNamespace.DefaultName")]
    [InlineData("myNamespace", null, "MyNamespace.DefaultName")]
    [InlineData(null, "myEvent", "DefaultNamespace.MyEvent")]
    [InlineData("", " ", "DefaultNamespace.DefaultName")]
    [InlineData("9", "[]", "DefaultNamespace.DefaultName")]
    public void DefaultEventNamespaceAndNameUsedToGenerateFullNameTest(string? eventNamespace, string? eventName, string expectedEventFullName)
    {
        var eventNameManager = CreateDefaultEventNameManager("defaultNamespace", "defaultName");

        var resolveEventFullName = eventNameManager.ResolveEventFullName(eventNamespace, eventName);

        Assert.Equal(Encoding.ASCII.GetBytes($"\"{expectedEventFullName}\""), resolveEventFullName.ToArray());
    }

    [Fact]
    public void DefaultEventNamespaceAndNameUsedToGenerateFullNameLengthTest()
    {
        var eventNameManager = CreateDefaultEventNameManager("defaultNamespace", "defaultName");

        var resolveEventFullName = eventNameManager.ResolveEventFullName("N", "N");

        Assert.Equal(Encoding.ASCII.GetBytes("\"DefaultNamespace.DefaultName\""), resolveEventFullName.ToArray());

        resolveEventFullName = eventNameManager.ResolveEventFullName(new string('N', 99), "N");

        Assert.Equal(Encoding.ASCII.GetBytes("\"DefaultNamespace.DefaultName\""), resolveEventFullName.ToArray());

        resolveEventFullName = eventNameManager.ResolveEventFullName("N", new string('N', 99));

        Assert.Equal(Encoding.ASCII.GetBytes("\"DefaultNamespace.DefaultName\""), resolveEventFullName.ToArray());
    }

    [Fact]
    public void EventNameCacheTest()
    {
        var eventNameManager = CreateDefaultEventNameManager("defaultNamespace", "defaultName");

        Assert.Empty(eventNameManager.EventNamespaceCache);

        eventNameManager.ResolveEventFullName("Test", "Test");

        Assert.Single(eventNameManager.EventNamespaceCache);
        Assert.Single((eventNameManager.EventNamespaceCache["Test"] as Hashtable)!);

        eventNameManager.ResolveEventFullName("test", "test");

        Assert.Single(eventNameManager.EventNamespaceCache);
        Assert.Single((eventNameManager.EventNamespaceCache["Test"] as Hashtable)!);
    }

    [Fact]
    public void TableMappingEnabledAndEventNamespaceMatchesTest()
    {
        var exporterOptions = new OneCollectorLogExporterOptions
        {
            DefaultEventNamespace = "defaultNamespace",
            DefaultEventName = "defaultName",
            TableMappingOptions = new OneCollectorLogExporterTableMappingOptions
            {
                UseTableMapping = true,
                TableMappings = new Dictionary<string, string>
                {
                    { "MyNamespace", "MyTable" },
                    { "MyNamespace2", "MyTable2" },
                },
                DefaultTableName = "Log",
            },
        };

        var eventNameManager = new EventNameManager(exporterOptions);

        var resolveEventFullName = eventNameManager.ResolveEventFullName("MyNamespace", "Test");

        Assert.Equal(Encoding.ASCII.GetBytes("\"MyTable\""), resolveEventFullName.ToArray());
    }

    [Fact]
    public void TableMappingEnabledAndEventNamespacePrefixMatchesTest()
    {
        var exporterOptions = new OneCollectorLogExporterOptions
        {
            DefaultEventNamespace = "defaultNamespace",
            DefaultEventName = "defaultName",
            TableMappingOptions = new OneCollectorLogExporterTableMappingOptions
            {
                UseTableMapping = true,
                TableMappings = new Dictionary<string, string>
                {
                    { "MyNamespace", "MyTable" },
                    { "MyNamespace2", "MyTable2" },
                },
                DefaultTableName = "Log",
            },
        };

        var eventNameManager = new EventNameManager(exporterOptions);

        var resolveEventFullName = eventNameManager.ResolveEventFullName("MyNamespace.MyChildNamespace", "Test");

        Assert.Equal(Encoding.ASCII.GetBytes("\"MyTable\""), resolveEventFullName.ToArray());
    }

    [Fact]
    public void TableMappingEnabledAndEventNamespaceLongestMatchWinsTest()
    {
        var exporterOptions = new OneCollectorLogExporterOptions
        {
            DefaultEventNamespace = "defaultNamespace",
            DefaultEventName = "defaultName",
            TableMappingOptions = new OneCollectorLogExporterTableMappingOptions
            {
                UseTableMapping = true,
                TableMappings = new Dictionary<string, string>
                {
                    { "MyNamespace", "MyTable" },
                    { "MyNamespace.MyChildNamespace", "MyChildTable" },
                    { "MyNamespace2", "MyTable2" },
                },
                DefaultTableName = "Log",
            },
        };

        var eventNameManager = new EventNameManager(exporterOptions);

        var resolveEventFullName = eventNameManager.ResolveEventFullName("MyNamespace.MyChildNamespace.MyGrandchildNamespace", "Test");

        Assert.Equal(Encoding.ASCII.GetBytes("\"MyChildTable\""), resolveEventFullName.ToArray());
    }

    [Fact]
    public void TableMappingEnabledAndEventNamespaceDoesNotMatchTest()
    {
        var exporterOptions = new OneCollectorLogExporterOptions
        {
            DefaultEventNamespace = "defaultNamespace",
            DefaultEventName = "defaultName",
            TableMappingOptions = new OneCollectorLogExporterTableMappingOptions
            {
                UseTableMapping = true,
                TableMappings = new Dictionary<string, string>
                {
                    { "MyNamespace2", "MyTable2" },
                    { "MyNamespace3", "MyTable3" },
                },
                DefaultTableName = "Log",
            },
        };

        var eventNameManager = new EventNameManager(exporterOptions);

        var resolveEventFullName = eventNameManager.ResolveEventFullName("MyNamespace", "Test");

        Assert.Equal(Encoding.ASCII.GetBytes("\"Log\""), resolveEventFullName.ToArray());
    }

    [Fact]
    public void TableMappingDisabledAndEventNamespaceMatchesTest()
    {
        var exporterOptions = new OneCollectorLogExporterOptions
        {
            DefaultEventNamespace = "defaultNamespace",
            DefaultEventName = "defaultName",
            TableMappingOptions = new OneCollectorLogExporterTableMappingOptions
            {
                UseTableMapping = false,
                TableMappings = new Dictionary<string, string>
                {
                    { "MyNamespace2", "MyTable" },
                },
                DefaultTableName = "Log",
            },
        };

        var eventNameManager = new EventNameManager(exporterOptions);

        var resolveEventFullName = eventNameManager.ResolveEventFullName("MyNamespace", "Test");

        Assert.Equal(Encoding.ASCII.GetBytes("\"MyNamespace.Test\""), resolveEventFullName.ToArray());
    }

    [Fact]
    public void TableMappingDisabledAndEventNamespaceDoesNotMatchTest()
    {
        var exporterOptions = new OneCollectorLogExporterOptions
        {
            DefaultEventNamespace = "defaultNamespace",
            DefaultEventName = "defaultName",
            TableMappingOptions = new OneCollectorLogExporterTableMappingOptions
            {
                UseTableMapping = false,
                TableMappings = new Dictionary<string, string>
                {
                    { "MyNamespace2", "MyTable" },
                },
                DefaultTableName = "Log",
            },
        };

        var eventNameManager = new EventNameManager(exporterOptions);

        var resolveEventFullName = eventNameManager.ResolveEventFullName("MyNamespace", "Test");

        Assert.Equal(Encoding.ASCII.GetBytes("\"MyNamespace.Test\""), resolveEventFullName.ToArray());
    }

    private static EventNameManager CreateDefaultEventNameManager(string defaultNamespace, string defaultEventName)
    {
        return new EventNameManager(new OneCollectorLogExporterOptions
        {
            DefaultEventNamespace = defaultNamespace,
            DefaultEventName = defaultEventName,
        });
    }
}
