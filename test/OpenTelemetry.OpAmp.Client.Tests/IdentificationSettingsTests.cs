// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Settings;
using Xunit;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class IdentificationSettingsTests
{
    [Fact]
    public void Resources_AddIdentifyingAttribute_NonUniqueOverwrites()
    {
        var resources = new IdentificationSettings();
        resources.AddIdentifyingAttribute("key-test", "value-test");
        resources.AddIdentifyingAttribute("key-test", "value-test-2");

        Assert.Single(resources.IdentifyingResources);
        Assert.Empty(resources.NonIdentifyingResources);
        Assert.Contains("key-test", resources.IdentifyingResources.Keys);
        Assert.Equal("value-test-2", resources.IdentifyingResources["key-test"].StringValue);
    }

    [Fact]
    public void Resources_AddIdentifyingAttribute_String()
    {
        var resources = new IdentificationSettings();
        resources.AddIdentifyingAttribute("key-test", "value-test");

        Assert.Single(resources.IdentifyingResources);
        Assert.Empty(resources.NonIdentifyingResources);
        Assert.Contains("key-test", resources.IdentifyingResources.Keys);
        Assert.Equal("value-test", resources.IdentifyingResources["key-test"].StringValue);
    }

    [Fact]
    public void Resources_AddIdentifyingAttribute_StringList()
    {
        var resources = new IdentificationSettings();
        resources.AddIdentifyingAttribute("key-test", ["value1-test", "value2-test"]);

        Assert.Single(resources.IdentifyingResources);
        Assert.Empty(resources.NonIdentifyingResources);
        Assert.Contains("key-test", resources.IdentifyingResources.Keys);
        Assert.Equal("value1-test", resources.IdentifyingResources["key-test"].ArrayValue!.ElementAt(0).StringValue);
        Assert.Equal("value2-test", resources.IdentifyingResources["key-test"].ArrayValue!.ElementAt(1).StringValue);
    }

    [Fact]
    public void Resources_AddIdentifyingAttribute_Bool()
    {
        var resources = new IdentificationSettings();
        resources.AddIdentifyingAttribute("key-test", true);

        Assert.Single(resources.IdentifyingResources);
        Assert.Empty(resources.NonIdentifyingResources);
        Assert.Contains("key-test", resources.IdentifyingResources.Keys);
        Assert.Equal(true, resources.IdentifyingResources["key-test"].BoolValue);
    }

    [Fact]
    public void Resources_AddIdentifyingAttribute_BoolList()
    {
        var resources = new IdentificationSettings();
        resources.AddIdentifyingAttribute("key-test", [true, false]);

        Assert.Single(resources.IdentifyingResources);
        Assert.Empty(resources.NonIdentifyingResources);
        Assert.Contains("key-test", resources.IdentifyingResources.Keys);
        Assert.Equal(true, resources.IdentifyingResources["key-test"].ArrayValue!.ElementAt(0).BoolValue);
        Assert.Equal(false, resources.IdentifyingResources["key-test"].ArrayValue!.ElementAt(1).BoolValue);
    }

    [Fact]
    public void Resources_AddIdentifyingAttribute_Int()
    {
        var resources = new IdentificationSettings();
        resources.AddIdentifyingAttribute("key-test", 1);

        Assert.Single(resources.IdentifyingResources);
        Assert.Empty(resources.NonIdentifyingResources);
        Assert.Contains("key-test", resources.IdentifyingResources.Keys);
        Assert.Equal(1, resources.IdentifyingResources["key-test"].IntValue);
    }

    [Fact]
    public void Resources_AddIdentifyingAttribute_IntList()
    {
        var resources = new IdentificationSettings();
        resources.AddIdentifyingAttribute("key-test", [1, 2]);

        Assert.Single(resources.IdentifyingResources);
        Assert.Empty(resources.NonIdentifyingResources);
        Assert.Contains("key-test", resources.IdentifyingResources.Keys);
        Assert.Equal(1, resources.IdentifyingResources["key-test"].ArrayValue!.ElementAt(0).IntValue);
        Assert.Equal(2, resources.IdentifyingResources["key-test"].ArrayValue!.ElementAt(1).IntValue);
    }

    [Fact]
    public void Resources_AddIdentifyingAttribute_Double()
    {
        var resources = new IdentificationSettings();
        resources.AddIdentifyingAttribute("key-test", 1.99);

        Assert.Single(resources.IdentifyingResources);
        Assert.Empty(resources.NonIdentifyingResources);
        Assert.Contains("key-test", resources.IdentifyingResources.Keys);
        Assert.Equal(1.99, resources.IdentifyingResources["key-test"].DoubleValue);
    }

    [Fact]
    public void Resources_AddIdentifyingAttribute_DoubleList()
    {
        var resources = new IdentificationSettings();
        resources.AddIdentifyingAttribute("key-test", [1.99, 2.99]);

        Assert.Single(resources.IdentifyingResources);
        Assert.Empty(resources.NonIdentifyingResources);
        Assert.Contains("key-test", resources.IdentifyingResources.Keys);
        Assert.Equal(1.99, resources.IdentifyingResources["key-test"].ArrayValue!.ElementAt(0).DoubleValue);
        Assert.Equal(2.99, resources.IdentifyingResources["key-test"].ArrayValue!.ElementAt(1).DoubleValue);
    }

    [Fact]
    public void Resources_AddNonIdentifyingAttribute_NonUniqueOverwrites()
    {
        var resources = new IdentificationSettings();
        resources.AddNonIdentifyingAttribute("key-test", "value-test");
        resources.AddNonIdentifyingAttribute("key-test", "value-test-2");

        Assert.Single(resources.NonIdentifyingResources);
        Assert.Empty(resources.IdentifyingResources);
        Assert.Contains("key-test", resources.NonIdentifyingResources.Keys);
        Assert.Equal("value-test-2", resources.NonIdentifyingResources["key-test"].StringValue);
    }

    [Fact]
    public void Resources_AddNonIdentifyingAttribute_String()
    {
        var resources = new IdentificationSettings();
        resources.AddNonIdentifyingAttribute("key-test", "value-test");

        Assert.Single(resources.NonIdentifyingResources);
        Assert.Empty(resources.IdentifyingResources);
        Assert.Contains("key-test", resources.NonIdentifyingResources.Keys);
        Assert.Equal("value-test", resources.NonIdentifyingResources["key-test"].StringValue);
    }

    [Fact]
    public void Resources_AddNonIdentifyingAttribute_StringList()
    {
        var resources = new IdentificationSettings();
        resources.AddNonIdentifyingAttribute("key-test", ["value1-test", "value2-test"]);

        Assert.Single(resources.NonIdentifyingResources);
        Assert.Empty(resources.IdentifyingResources);
        Assert.Contains("key-test", resources.NonIdentifyingResources.Keys);
        Assert.Equal("value1-test", resources.NonIdentifyingResources["key-test"].ArrayValue!.ElementAt(0).StringValue);
        Assert.Equal("value2-test", resources.NonIdentifyingResources["key-test"].ArrayValue!.ElementAt(1).StringValue);
    }

    [Fact]
    public void Resources_AddNonIdentifyingAttribute_Bool()
    {
        var resources = new IdentificationSettings();
        resources.AddNonIdentifyingAttribute("key-test", true);

        Assert.Single(resources.NonIdentifyingResources);
        Assert.Empty(resources.IdentifyingResources);
        Assert.Contains("key-test", resources.NonIdentifyingResources.Keys);
        Assert.Equal(true, resources.NonIdentifyingResources["key-test"].BoolValue);
    }

    [Fact]
    public void Resources_AddNonIdentifyingAttribute_BoolList()
    {
        var resources = new IdentificationSettings();
        resources.AddNonIdentifyingAttribute("key-test", [true, false]);

        Assert.Single(resources.NonIdentifyingResources);
        Assert.Empty(resources.IdentifyingResources);
        Assert.Contains("key-test", resources.NonIdentifyingResources.Keys);
        Assert.Equal(true, resources.NonIdentifyingResources["key-test"].ArrayValue!.ElementAt(0).BoolValue);
        Assert.Equal(false, resources.NonIdentifyingResources["key-test"].ArrayValue!.ElementAt(1).BoolValue);
    }

    [Fact]
    public void Resources_AddNonIdentifyingAttribute_Int()
    {
        var resources = new IdentificationSettings();
        resources.AddNonIdentifyingAttribute("key-test", 1);

        Assert.Single(resources.NonIdentifyingResources);
        Assert.Empty(resources.IdentifyingResources);
        Assert.Contains("key-test", resources.NonIdentifyingResources.Keys);
        Assert.Equal(1, resources.NonIdentifyingResources["key-test"].IntValue);
    }

    [Fact]
    public void Resources_AddNonIdentifyingAttribute_IntList()
    {
        var resources = new IdentificationSettings();
        resources.AddNonIdentifyingAttribute("key-test", [1, 2]);

        Assert.Single(resources.NonIdentifyingResources);
        Assert.Empty(resources.IdentifyingResources);
        Assert.Contains("key-test", resources.NonIdentifyingResources.Keys);
        Assert.Equal(1, resources.NonIdentifyingResources["key-test"].ArrayValue!.ElementAt(0).IntValue);
        Assert.Equal(2, resources.NonIdentifyingResources["key-test"].ArrayValue!.ElementAt(1).IntValue);
    }

    [Fact]
    public void Resources_AddNonIdentifyingAttribute_Double()
    {
        var resources = new IdentificationSettings();
        resources.AddNonIdentifyingAttribute("key-test", 1.99);

        Assert.Single(resources.NonIdentifyingResources);
        Assert.Empty(resources.IdentifyingResources);
        Assert.Contains("key-test", resources.NonIdentifyingResources.Keys);
        Assert.Equal(1.99, resources.NonIdentifyingResources["key-test"].DoubleValue);
    }

    [Fact]
    public void Resources_AddNonIdentifyingAttribute_DoubleList()
    {
        var resources = new IdentificationSettings();
        resources.AddNonIdentifyingAttribute("key-test", [1.99, 2.99]);

        Assert.Single(resources.NonIdentifyingResources);
        Assert.Empty(resources.IdentifyingResources);
        Assert.Contains("key-test", resources.NonIdentifyingResources.Keys);
        Assert.Equal(1.99, resources.NonIdentifyingResources["key-test"].ArrayValue!.ElementAt(0).DoubleValue);
        Assert.Equal(2.99, resources.NonIdentifyingResources["key-test"].ArrayValue!.ElementAt(1).DoubleValue);
    }
}
