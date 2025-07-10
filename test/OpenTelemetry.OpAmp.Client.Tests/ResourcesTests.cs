// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Data;
using Xunit;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class ResourcesTests
{
    [Fact]
    public void Resources_AddIdentifyingAttribute_NonUniqueOverwrites()
    {
        var resources = new OpAmpClientResources();
        resources.AddIdentifyingAttribute("key-test", "value-test");
        resources.AddIdentifyingAttribute("key-test", "value-test-2");

        Assert.Single(resources.IdentifingResources);
        Assert.Empty(resources.NonIdentifingResources);
        Assert.Contains("key-test", resources.IdentifingResources.Keys);
        Assert.Equal("value-test-2", resources.IdentifingResources["key-test"].StringValue);
    }

    [Fact]
    public void Resources_AddIdentifyingAttribute_String()
    {
        var resources = new OpAmpClientResources();
        resources.AddIdentifyingAttribute("key-test", "value-test");

        Assert.Single(resources.IdentifingResources);
        Assert.Empty(resources.NonIdentifingResources);
        Assert.Contains("key-test", resources.IdentifingResources.Keys);
        Assert.Equal("value-test", resources.IdentifingResources["key-test"].StringValue);
    }

    [Fact]
    public void Resources_AddIdentifyingAttribute_Bool()
    {
        var resources = new OpAmpClientResources();
        resources.AddIdentifyingAttribute("key-test", true);

        Assert.Single(resources.IdentifingResources);
        Assert.Empty(resources.NonIdentifingResources);
        Assert.Contains("key-test", resources.IdentifingResources.Keys);
        Assert.Equal(true, resources.IdentifingResources["key-test"].BoolValue);
    }

    [Fact]
    public void Resources_AddIdentifyingAttribute_Int()
    {
        var resources = new OpAmpClientResources();
        resources.AddIdentifyingAttribute("key-test", 1);

        Assert.Single(resources.IdentifingResources);
        Assert.Empty(resources.NonIdentifingResources);
        Assert.Contains("key-test", resources.IdentifingResources.Keys);
        Assert.Equal(1, resources.IdentifingResources["key-test"].IntValue);
    }

    [Fact]
    public void Resources_AddIdentifyingAttribute_Double()
    {
        var resources = new OpAmpClientResources();
        resources.AddIdentifyingAttribute("key-test", 1.99);

        Assert.Single(resources.IdentifingResources);
        Assert.Empty(resources.NonIdentifingResources);
        Assert.Contains("key-test", resources.IdentifingResources.Keys);
        Assert.Equal(1.99, resources.IdentifingResources["key-test"].DoubleValue);
    }

    [Fact]
    public void Resources_AddNonIdentifyingAttribute_NonUniqueOverwrites()
    {
        var resources = new OpAmpClientResources();
        resources.AddNonIdentifyingAttribute("key-test", "value-test");
        resources.AddNonIdentifyingAttribute("key-test", "value-test-2");

        Assert.Single(resources.NonIdentifingResources);
        Assert.Empty(resources.IdentifingResources);
        Assert.Contains("key-test", resources.NonIdentifingResources.Keys);
        Assert.Equal("value-test-2", resources.NonIdentifingResources["key-test"].StringValue);
    }

    [Fact]
    public void Resources_AddNonIdentifyingAttribute_String()
    {
        var resources = new OpAmpClientResources();
        resources.AddNonIdentifyingAttribute("key-test", "value-test");

        Assert.Single(resources.NonIdentifingResources);
        Assert.Empty(resources.IdentifingResources);
        Assert.Contains("key-test", resources.NonIdentifingResources.Keys);
        Assert.Equal("value-test", resources.NonIdentifingResources["key-test"].StringValue);
    }

    [Fact]
    public void Resources_AddNonIdentifyingAttribute_Bool()
    {
        var resources = new OpAmpClientResources();
        resources.AddNonIdentifyingAttribute("key-test", true);

        Assert.Single(resources.NonIdentifingResources);
        Assert.Empty(resources.IdentifingResources);
        Assert.Contains("key-test", resources.NonIdentifingResources.Keys);
        Assert.Equal(true, resources.NonIdentifingResources["key-test"].BoolValue);
    }

    [Fact]
    public void Resources_AddNonIdentifyingAttribute_Int()
    {
        var resources = new OpAmpClientResources();
        resources.AddNonIdentifyingAttribute("key-test", 1);

        Assert.Single(resources.NonIdentifingResources);
        Assert.Empty(resources.IdentifingResources);
        Assert.Contains("key-test", resources.NonIdentifingResources.Keys);
        Assert.Equal(1, resources.NonIdentifingResources["key-test"].IntValue);
    }

    [Fact]
    public void Resources_AddNonIdentifyingAttribute_Double()
    {
        var resources = new OpAmpClientResources();
        resources.AddNonIdentifyingAttribute("key-test", 1.99);

        Assert.Single(resources.NonIdentifingResources);
        Assert.Empty(resources.IdentifingResources);
        Assert.Contains("key-test", resources.NonIdentifingResources.Keys);
        Assert.Equal(1.99, resources.NonIdentifingResources["key-test"].DoubleValue);
    }
}
