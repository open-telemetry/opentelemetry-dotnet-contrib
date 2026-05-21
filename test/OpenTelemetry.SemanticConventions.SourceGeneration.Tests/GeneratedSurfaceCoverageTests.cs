// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OpenTelemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace OpenTelemetry.SemanticConventions.SourceGeneration.Tests;

public class GeneratedSurfaceCoverageTests
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    public static IEnumerable<object[]> SurfaceCases()
    {
        yield return TestCase(Surface(
            name: "Stable attributes",
            generator: new SemConvAttributesGenerator(),
            source: """
                    namespace ConsumerNs;

                    [OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionAttributes("http")]
                    public static partial class HttpStableAttributes
                    {
                    }
                    """,
            expectedGeneratedText: "http.request.method"));
        yield return TestCase(Surface(
            name: "Incubating attributes",
            generator: new SemConvAttributesGenerator(),
            source: """
                    namespace ConsumerNs;

                    [OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionIncubatingAttributes("http")]
                    public static partial class HttpIncubatingAttributes
                    {
                    }
                    """,
            expectedGeneratedText: "QUERY"));
        yield return TestCase(Surface(
            name: "Stable metrics",
            generator: new SemConvMetricsGenerator(),
            source: """
                    namespace ConsumerNs;

                    [OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionMetrics("http")]
                    public static partial class HttpStableMetrics
                    {
                    }
                    """,
            expectedGeneratedText: "http.client.request.duration"));
        yield return TestCase(Surface(
            name: "Incubating metrics",
            generator: new SemConvMetricsGenerator(),
            source: """
                    namespace ConsumerNs;

                    [OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionIncubatingMetrics("http")]
                    public static partial class HttpIncubatingMetrics
                    {
                    }
                    """,
            expectedGeneratedText: "http.client.active_requests"));
        yield return TestCase(Surface(
            name: "Stable meters",
            generator: new SemConvMetersGenerator(),
            source: """
                    namespace ConsumerNs;

                    [OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionMeters("http")]
                    public static partial class HttpStableMeters
                    {
                    }
                    """,
            expectedGeneratedText: "CreateHttpClientRequestDurationHistogram"));
        yield return TestCase(Surface(
            name: "Incubating meters",
            generator: new SemConvMetersGenerator(),
            source: """
                    namespace ConsumerNs;

                    [OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionIncubatingMeters("http")]
                    public static partial class HttpIncubatingMeters
                    {
                    }
                    """,
            expectedGeneratedText: "CreateHttpClientActiveRequestsUpdowncounter"));
        yield return TestCase(Surface(
            name: "Stable events",
            generator: new SemConvEventsGenerator(),
            source: """
                    namespace ConsumerNs;

                    [OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionEvents("exception")]
                    public static partial class ExceptionStableEvents
                    {
                    }
                    """,
            expectedGeneratedText: "ExceptionPayload"));
        yield return TestCase(Surface(
            name: "Incubating events",
            generator: new SemConvEventsGenerator(),
            source: """
                    namespace ConsumerNs;

                    [OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionIncubatingEvents("http")]
                    public static partial class HttpIncubatingEvents
                    {
                    }
                    """,
            expectedGeneratedText: "http.client.request.exception"));
        yield return TestCase(Surface(
            name: "Stable activities",
            generator: new SemConvActivitiesGenerator(),
            source: """
                    namespace ConsumerNs;

                    [OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionActivities("http")]
                    public static partial class HttpStableActivities
                    {
                    }
                    """,
            expectedGeneratedText: "SetHttpRequestMethod"));
        yield return TestCase(Surface(
            name: "Incubating activities",
            generator: new SemConvActivitiesGenerator(),
            source: """
                    namespace ConsumerNs;

                    [OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionIncubatingActivities("http")]
                    public static partial class HttpIncubatingActivities
                    {
                    }
                    """,
            expectedGeneratedText: "QUERY"));
    }

    public static IEnumerable<object[]> SameClassNameCases()
    {
        yield return TestCase(SameClassName(
            name: "Attributes",
            generator: new SemConvAttributesGenerator(),
            attributeName: "SemanticConventionAttributes",
            prefix: "http"));
        yield return TestCase(SameClassName(
            name: "Metrics",
            generator: new SemConvMetricsGenerator(),
            attributeName: "SemanticConventionMetrics",
            prefix: "http"));
        yield return TestCase(SameClassName(
            name: "Meters",
            generator: new SemConvMetersGenerator(),
            attributeName: "SemanticConventionMeters",
            prefix: "http"));
        yield return TestCase(SameClassName(
            name: "Events",
            generator: new SemConvEventsGenerator(),
            attributeName: "SemanticConventionEvents",
            prefix: "exception"));
        yield return TestCase(SameClassName(
            name: "Activities",
            generator: new SemConvActivitiesGenerator(),
            attributeName: "SemanticConventionActivities",
            prefix: "http"));
    }

    [Theory]
    [MemberData(nameof(SurfaceCases))]
    public void GeneratedSurface_CompilesWithoutDiagnostics(object data)
    {
        var testCase = Assert.IsType<GeneratorSurfaceCase>(data);
        var result = RunGenerator(testCase.Generator, testCase.Source);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(GetErrors(result.OutputCompilation));
        Assert.Contains(testCase.ExpectedGeneratedText, result.GeneratedText);
    }

    [Theory]
    [MemberData(nameof(SurfaceCases))]
    public void GeneratedSurface_DoesNotEmitDuplicateMembers(object data)
    {
        var testCase = Assert.IsType<GeneratorSurfaceCase>(data);
        var result = RunGenerator(testCase.Generator, testCase.Source);

        var duplicates = result.RunResult.GeneratedTrees
            .SelectMany(FindDuplicateGeneratedMembers)
            .ToArray();

        Assert.Empty(duplicates);
    }

    [Theory]
    [MemberData(nameof(SameClassNameCases))]
    public void GeneratedSurface_AllowsSameClassNameInDifferentNamespaces(object data)
    {
        var testCase = Assert.IsType<SameClassNameCase>(data);
        var source = $$"""
            namespace FirstConsumerNs
            {
                [OpenTelemetry.SemanticConventions.SourceGeneration.{{testCase.AttributeName}}("{{testCase.Prefix}}")]
                public static partial class SharedSemConvSurface
                {
                }
            }

            namespace SecondConsumerNs
            {
                [OpenTelemetry.SemanticConventions.SourceGeneration.{{testCase.AttributeName}}("{{testCase.Prefix}}")]
                public static partial class SharedSemConvSurface
                {
                }
            }
            """;

        var result = RunGenerator(testCase.Generator, source);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(GetErrors(result.OutputCompilation));
        Assert.Contains("namespace FirstConsumerNs;", result.GeneratedText);
        Assert.Contains("namespace SecondConsumerNs;", result.GeneratedText);
    }

    private static GeneratorSurfaceCase Surface(
        string name,
        IIncrementalGenerator generator,
        string source,
        string expectedGeneratedText)
    {
        return new GeneratorSurfaceCase(name, generator, source, expectedGeneratedText);
    }

    private static SameClassNameCase SameClassName(
        string name,
        IIncrementalGenerator generator,
        string attributeName,
        string prefix)
    {
        return new SameClassNameCase(name, generator, attributeName, prefix);
    }

    private static object[] TestCase(object testCase) => new[] { testCase };

    private static GeneratorRun RunGenerator(IIncrementalGenerator generator, string consumerSource)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(consumerSource, ParseOptions);
        var compilation = CSharpCompilation.Create(
            assemblyName: "ConsumerAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: CreateMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { generator.AsSourceGenerator() }, parseOptions: ParseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);

        var runResult = driver.GetRunResult();
        var generatedText = string.Concat(runResult.GeneratedTrees.Select(static t => t.ToString()));
        return new GeneratorRun(runResult, outputCompilation, generatorDiagnostics, generatedText);
    }

    private static ImmutableArray<Diagnostic> GetErrors(Compilation compilation)
    {
        return compilation.GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
    }

    private static IEnumerable<string> FindDuplicateGeneratedMembers(SyntaxTree generatedTree)
    {
        var root = generatedTree.GetRoot();
        foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var seen = new Dictionary<string, MemberDeclarationSyntax>(StringComparer.Ordinal);
            foreach (var member in type.Members)
            {
                foreach (var key in MemberKeys(member))
                {
                    if (!seen.TryAdd(key, member))
                    {
                        yield return $"{generatedTree.FilePath}: {type.Identifier.ValueText} emits duplicate {key}";
                    }
                }
            }
        }
    }

    private static IEnumerable<string> MemberKeys(MemberDeclarationSyntax member)
    {
        switch (member)
        {
            case BaseTypeDeclarationSyntax type:
                yield return "type " + type.Identifier.ValueText;
                yield break;
            case FieldDeclarationSyntax field:
                foreach (var variable in field.Declaration.Variables)
                {
                    yield return "field " + variable.Identifier.ValueText;
                }

                yield break;
            case PropertyDeclarationSyntax property:
                yield return "property " + property.Identifier.ValueText;
                yield break;
            case MethodDeclarationSyntax method:
                yield return "method " + method.Identifier.ValueText + "(" +
                    string.Join(", ", method.ParameterList.Parameters.Select(static p => p.Type?.ToString() ?? string.Empty)) +
                    ")";
                yield break;
        }
    }

    private static MetadataReference[] CreateMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        if (!string.IsNullOrEmpty(trustedPlatformAssemblies))
        {
            return trustedPlatformAssemblies
                .Split(Path.PathSeparator)
                .Select(static path => MetadataReference.CreateFromFile(path))
                .ToArray();
        }

        return new[]
            {
                typeof(object).Assembly,
                typeof(Enumerable).Assembly,
                typeof(System.Diagnostics.Activity).Assembly,
                typeof(System.Diagnostics.Metrics.Meter).Assembly,
            }
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location))
            .ToArray();
    }

    private sealed record GeneratorSurfaceCase(
        string Name,
        IIncrementalGenerator Generator,
        string Source,
        string ExpectedGeneratedText)
    {
        public override string ToString() => Name;
    }

    private sealed record SameClassNameCase(
        string Name,
        IIncrementalGenerator Generator,
        string AttributeName,
        string Prefix)
    {
        public override string ToString() => Name;
    }

    private sealed record GeneratorRun(
        GeneratorDriverRunResult RunResult,
        Compilation OutputCompilation,
        ImmutableArray<Diagnostic> GeneratorDiagnostics,
        string GeneratedText);
}
