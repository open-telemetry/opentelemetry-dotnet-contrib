// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OpenTelemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace OpenTelemetry.SemanticConventions.SourceGeneration.Tests;

/// <summary>
/// Drives the attribute source generator via CSharpGeneratorDriver and verifies the
/// core Stable/Incubating projection invariants that the upstream proposal relies on:
/// (1) Stable surface is strict — `stability == stable` only.
/// (2) Incubating surface is a superset of Stable.
/// (3) Mixed-stability enum members do not leak into the Stable enum surface.
///     Named regression: `http.request.method = QUERY` is incubating-only.
/// (4) Generated output is deterministic across two driver runs over an unchanged
///     input compilation.
/// </summary>
public class StableIncubatingSeparationTests
{
    private const string StableHttpConsumerSource = @"
namespace ConsumerNs;

[OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionAttributes(""http"")]
public static partial class HttpStable
{
}
";

    private const string IncubatingHttpConsumerSource = @"
namespace ConsumerNs;

[OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionIncubatingAttributes(""http"")]
public static partial class HttpIncubating
{
}
";

    [Fact]
    public void GeneratorBuildsAndProducesOutput()
    {
        var driverResult = RunGenerator(StableHttpConsumerSource);

        Assert.NotNull(driverResult);
        Assert.Empty(driverResult.Diagnostics);
        Assert.NotEmpty(driverResult.GeneratedTrees);
    }

    [Fact]
    public void StableProjection_ContainsRequestMethodAttribute()
    {
        var combined = RunAndCollect(StableHttpConsumerSource);

        Assert.Contains("http.request.method", combined);
    }

    [Fact]
    public void StableProjection_DoesNotLeakDevelopmentEnumMember_Query()
    {
        var combined = RunAndCollect(StableHttpConsumerSource);

        // The QUERY HTTP method is a development-stability enum member on the
        // otherwise-stable http.request.method attribute. Per the spec rule
        // (enum-member stability independent of attribute stability), it must
        // NOT appear in the stable projection as a constant declaration. This
        // is the named regression from open-telemetry/opentelemetry-dotnet-contrib#2090.
        //
        // QUERY may still appear inside doc comments lifted from the registry
        // brief/note text (where it is referenced contextually for users who
        // opt into the incubating surface). We only assert that no const symbol
        // or const value materialises in the stable output.
        Assert.DoesNotContain("= \"QUERY\"", combined);
        Assert.DoesNotContain("const string Query ", combined);
    }

    [Fact]
    public void IncubatingProjection_ContainsBothStableAndDevelopmentMembers()
    {
        var combined = RunAndCollect(IncubatingHttpConsumerSource);

        Assert.Contains("http.request.method", combined);
        // QUERY must appear in incubating output — incubating is a superset.
        Assert.Contains("QUERY", combined);
    }

    [Fact]
    public void GeneratorOutput_IsDeterministicAcrossRuns()
    {
        var first = RunAndCollect(StableHttpConsumerSource);
        var second = RunAndCollect(StableHttpConsumerSource);

        Assert.Equal(first, second);
    }

    private static GeneratorDriverRunResult RunGenerator(string consumerSource)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(consumerSource);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Diagnostics.Activity).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            assemblyName: "ConsumerAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new SemConvAttributesGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        return driver.RunGenerators(compilation).GetRunResult();
    }

    private static string RunAndCollect(string consumerSource)
    {
        var result = RunGenerator(consumerSource);
        return string.Concat(result.GeneratedTrees.Select(t => t.ToString()));
    }
}
