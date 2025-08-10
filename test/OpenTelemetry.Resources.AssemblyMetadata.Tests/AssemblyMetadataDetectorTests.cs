// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Resources.AssemblyMetadata.Tests;

public sealed class AssemblyMetadataDetectorTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public void PrintAssemblyMetadataAttributes()
    {
        var assembly = typeof(AssemblyMetadataDetectorTests).Assembly;
        var resource = ResourceBuilder.CreateEmpty().AddAssemblyMetadataDetector(assembly).Build();

#if NET
        foreach (var (k, v) in resource.Attributes)
#else
        foreach (var (k, v) in resource.Attributes.Select(static x => (x.Key, x.Value)))
#endif
        {
            outputHelper.WriteLine($"{k} = {v}");
        }
    }

    [Fact]
    public void TestAssemblyMetadataAttributes()
    {
        var assembly = typeof(AssemblyMetadataDetectorTests).Assembly;
        var resource = ResourceBuilder.CreateEmpty().AddAssemblyMetadataDetector(assembly).Build();
        var attributes = resource.Attributes.ToDictionary(static k => k.Key, static v => v.Value);

        var expected = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
#if NET
            .Where(static x => x is { Key: ['o', 't', 'e', 'l', ':', _, ..], Value: not null })
            .ToDictionary(static k => k.Key["otel:".Length..], static v => v.Value);
#else
            .Where(static x => x is { Key: { Length: > 5 } k, Value: not null } && k.StartsWith("otel:", StringComparison.Ordinal))
            .ToDictionary(static k => k.Key.Substring("otel:".Length), static v => v.Value);
#endif

        Assert.Equal(expected.Count, attributes.Count);

#if NET
        foreach (var (k, v) in expected)
#else
        foreach (var (k, v) in expected.Select(static x => (x.Key, x.Value)))
#endif
        {
            Assert.Equal(v, Assert.Contains(k, attributes));
        }

        // Custom resource attributes defined in `OpenTelemetry.Resources.AssemblyMetadata.Tests.csproj`
        Assert.DoesNotContain(string.Empty, attributes);
        Assert.Empty(Assert.IsType<string>(Assert.Contains("some_attribute_with_empty", attributes)));
        Assert.NotEmpty(Assert.IsType<string>(Assert.Contains("some_attribute_with_value", attributes)));
    }

    [Fact]
    public void TestAssemblyMetadataAttributesOnTestEntryAssembly()
    {
        // N.B. the entry assembly for unit tests is the test runner, so use that to confirm that attributes on referenced assemblies are not detected
        var resource = ResourceBuilder.CreateEmpty().AddAssemblyMetadataDetector().Build();

        Assert.Empty(resource.Attributes);
    }
}
