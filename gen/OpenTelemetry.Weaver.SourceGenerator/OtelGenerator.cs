// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace OpenTelemetry.Weaver.SourceGenerator;

/// <inheritdoc/>
[Generator]
public class OtelGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "OTEL requires attributes to be lowercase.")]
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // If you're targeting the .NET 7 SDK, use this version instead:
        var classDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                 "OpenTelemetry.SemanticConventions.OtelAttributeNamespaceAttribute",
                 predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                 transform: static (ctx, _) => GetStructTransformation(ctx, new List<GenerationMode>() { GenerationMode.AttributeNames, GenerationMode.AttributeValues }))
             .Where(static m => m is not null);

        var combined = context.CompilationProvider.Combine(classDeclarations.Collect());

        // Generate source code
        context.RegisterSourceOutput(
            classDeclarations,
            static (spc, source) => Execute(source, spc));

        static bool IsSyntaxTargetForGeneration(SyntaxNode node)
            => node is StructDeclarationSyntax m;

        static Properties? GetStructTransformation(GeneratorAttributeSyntaxContext context, List<GenerationMode> generationModes)
        {
            BaseTypeDeclarationSyntax structDeclarationSyntax = context.TargetNode is StructDeclarationSyntax syntax ?
               syntax :
               throw new NotSupportedException("Target Node is not supported");

            foreach (AttributeListSyntax attributeListSyntax in structDeclarationSyntax.AttributeLists)
            {
                foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
                {
                    if (attributeSyntax.Name.ToString() + "Attribute" == context.Attributes[0].AttributeClass?.Name && attributeSyntax.ArgumentList != null)
                    {
                        return GetAttributeToGenerate(context, attributeSyntax.ArgumentList, generationModes);
                    }
                }
            }

            // we didn't find the attribute we were looking for
            return null;
        }

        static Properties? GetAttributeToGenerate(GeneratorAttributeSyntaxContext context, AttributeArgumentListSyntax arguments, List<GenerationMode> generationModes)
        {
            string? attrNamespace = null;

            int i = 0;
            foreach (AttributeArgumentSyntax attributeData in arguments.Arguments)
            {
                // This is the right attribute, check the constructor arguments
                if (attributeData.NameEquals?.Name.Identifier.ValueText == "Namespace" ||
                    attributeData.NameColon?.Name.Identifier.ValueText == "Namespace" ||
                    i == 0)
                {
                    attrNamespace = attributeData.Expression.GetFirstToken().ValueText.ToLowerInvariant();
                }

                i++;
            }

            if (attrNamespace is null)
            {
                return null;
            }

            var fileNamespace = context.TargetSymbol.ContainingNamespace.ToDisplayString();
            var properties = new Properties(fileNamespace, context.TargetSymbol.Name);

            var assembly = typeof(SourceGenerationHelper).Assembly;

            foreach (var generationMode in generationModes)
            {
                var file = $"{assembly.GetName().Name!}.Resources.{generationMode}.{attrNamespace}.md";
                var resourceStream = assembly.GetManifestResourceStream(file);

                if (resourceStream is null)
                {
                    continue;
                }

                properties.Streams[generationMode] = resourceStream;
            }

            return properties;
        }

        static void Execute(Properties? properties, SourceProductionContext context)
        {
            if (properties is { } value)
            {
                foreach (var item in value.Streams)
                {
                    var streamReader = new StreamReader(item.Value);
                    var result = item.Key switch
                    {
                        GenerationMode.AttributeNames => SourceGenerationHelper.GenerateNamespaceAttributeNames(value, streamReader),
                        GenerationMode.AttributeValues => SourceGenerationHelper.GenerateNamespaceAttributeValues(value, streamReader),
                        _ => throw new NotSupportedException($"Generation mode {item.Key} is not supported.")
                    };

                    // generate the source code and add it to the output
                    //string result = SourceGenerationHelper.GenerateAttributeClass(value, item);

                    // Create a separate partial class file for each namespace
                    context.AddSource($"OtelAttributes.{value.StructName}.{item.Key}.g.cs", SourceText.From(result, Encoding.UTF8));
                }
            }
        }
    }
}
