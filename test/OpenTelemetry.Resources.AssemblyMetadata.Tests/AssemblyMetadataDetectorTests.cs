// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using OpenTelemetry.SemanticConventions;
using OpenTelemetry.Tests;
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

    [Fact]
    public void TestAssemblyMetadataAttributesWithLocalSourceLink()
    {
        // Check if our `.targets` applied SourceLink variables as expected. This test is only expected to run locally.
        if (Environment.GetEnvironmentVariable("CI") is null)
        {
            return;
        }

        var assembly = typeof(AssemblyMetadataDetectorTests).Assembly;
        var resource = ResourceBuilder.CreateEmpty().AddAssemblyMetadataDetector(assembly).Build();
        var attributes = resource.Attributes.ToDictionary();

        Assert.True(attributes.TryGetValue(VcsAttributes.AttributeVcsOwnerName, out var vcsOwnerName));
        Assert.Empty(Assert.IsType<string>(vcsOwnerName));

        Assert.True(attributes.TryGetValue(VcsAttributes.AttributeVcsProviderName, out var vcsProviderName));
        Assert.Empty(Assert.IsType<string>(vcsProviderName));

        Assert.True(attributes.TryGetValue(VcsAttributes.AttributeVcsRefHeadName, out var vcsRefHeadName));
        Assert.Empty(Assert.IsType<string>(vcsRefHeadName));

        Assert.True(attributes.TryGetValue(VcsAttributes.AttributeVcsRefHeadRevision, out var vcsRefHeadRevision));
        Assert.NotEmpty(Assert.IsType<string>(vcsRefHeadRevision));

        Assert.True(attributes.TryGetValue(VcsAttributes.AttributeVcsRefHeadType, out var vcsRefHeadType));
        Assert.True(Assert.IsType<string>(vcsRefHeadType) is VcsAttributes.VcsRefHeadTypeValues.Branch or VcsAttributes.VcsRefHeadTypeValues.Tag);

        Assert.True(attributes.TryGetValue(VcsAttributes.AttributeVcsRefType, out var vcsRefType));
        Assert.True(Assert.IsType<string>(vcsRefType) is VcsAttributes.VcsRefTypeValues.Branch or VcsAttributes.VcsRefTypeValues.Tag);

        Assert.True(attributes.TryGetValue(VcsAttributes.AttributeVcsRepositoryName, out var vcsRepositoryName));
        Assert.Empty(Assert.IsType<string>(vcsRepositoryName));

        Assert.True(attributes.TryGetValue(VcsAttributes.AttributeVcsRepositoryUrlFull, out var vcsRepositoryUrlFull));
        Assert.Empty(Assert.IsType<string>(vcsRepositoryUrlFull));
    }

    [SkipUnlessEnvVarFoundFact("GITHUB_ACTIONS")]
    public void TestAssemblyMetadataAttributesWithGitHubActions()
    {
        var assembly = typeof(AssemblyMetadataDetectorTests).Assembly;
        var resource = ResourceBuilder.CreateEmpty().AddAssemblyMetadataDetector(assembly).Build();
        var attributes = resource.Attributes.ToDictionary();

        if (Environment.GetEnvironmentVariable("GITHUB_EVENT_NAME") is "pull_request")
        {
            Assert.True(attributes.TryGetValue(VcsAttributes.AttributeVcsChangeId, out var vcsChangeId));
            Assert.Equal(Environment.GetEnvironmentVariable("GITHUB_REF"), $"refs/pull/{Assert.IsType<string>(vcsChangeId)}/merge");
        }

        Assert.True(attributes.TryGetValue(VcsAttributes.AttributeVcsOwnerName, out var vcsOwnerName));
        Assert.Equal(Environment.GetEnvironmentVariable("GITHUB_REPOSITORY_OWNER"), Assert.IsType<string>(vcsOwnerName));

        Assert.True(attributes.TryGetValue(VcsAttributes.AttributeVcsProviderName, out var vcsProviderName));
        Assert.Equal(VcsAttributes.VcsProviderNameValues.Github, Assert.IsType<string>(vcsProviderName));

        if (Environment.GetEnvironmentVariable("GITHUB_BASE_REF") is [_, ..] githubBaseRef)
        {
            Assert.True(attributes.TryGetValue(VcsAttributes.AttributeVcsRefBaseName, out var vcsRefBaseName));
            Assert.Equal(githubBaseRef, Assert.IsType<string>(vcsRefBaseName));

            Assert.True(attributes.TryGetValue(VcsAttributes.AttributeVcsRefBaseType, out var vcsRefBaseType));
            Assert.True(Assert.IsType<string>(vcsRefBaseType) is VcsAttributes.VcsRefBaseTypeValues.Branch or VcsAttributes.VcsRefBaseTypeValues.Tag);
        }

        Assert.True(attributes.TryGetValue(VcsAttributes.AttributeVcsRefHeadName, out var vcsRefHeadName));
        Assert.Equal(
            Environment.GetEnvironmentVariable("GITHUB_HEAD_REF") ?? Environment.GetEnvironmentVariable("GITHUB_REF_NAME"),
            Assert.IsType<string>(vcsRefHeadName));

        Assert.True(attributes.TryGetValue(VcsAttributes.AttributeVcsRefHeadRevision, out var vcsRefHeadRevision));
        Assert.Equal(Environment.GetEnvironmentVariable("GITHUB_SHA"), Assert.IsType<string>(vcsRefHeadRevision));

        Assert.True(attributes.TryGetValue(VcsAttributes.AttributeVcsRefHeadType, out var vcsRefHeadType));
        Assert.Equal(Environment.GetEnvironmentVariable("GITHUB_REF_TYPE"), Assert.IsType<string>(vcsRefHeadType));
        Assert.True(Assert.IsType<string>(vcsRefHeadType) is VcsAttributes.VcsRefHeadTypeValues.Branch or VcsAttributes.VcsRefHeadTypeValues.Tag);

        Assert.True(attributes.TryGetValue(VcsAttributes.AttributeVcsRefType, out var vcsRefType));
        Assert.Equal(Environment.GetEnvironmentVariable("GITHUB_REF_TYPE"), Assert.IsType<string>(vcsRefType));
        Assert.True(Assert.IsType<string>(vcsRefType) is VcsAttributes.VcsRefTypeValues.Branch or VcsAttributes.VcsRefTypeValues.Tag);

        Assert.True(attributes.TryGetValue(VcsAttributes.AttributeVcsRepositoryName, out var vcsRepositoryName));
        Assert.Equal(Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")?.Split('/')[1], Assert.IsType<string>(vcsRepositoryName));

        Assert.True(attributes.TryGetValue(VcsAttributes.AttributeVcsRepositoryUrlFull, out var vcsRepositoryUrlFull));
        Assert.Equal(
            $"{Environment.GetEnvironmentVariable("GITHUB_SERVER_URL")}/{Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")}",
            Assert.IsType<string>(vcsRepositoryUrlFull));
    }
}
