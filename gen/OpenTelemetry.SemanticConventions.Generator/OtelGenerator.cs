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

namespace OpenTelemetry.SemanticConventions.Generator;

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
                 transform: static (ctx, _) => GetStructTransformation(ctx, GenerationMode.AttributeNamesAndValues))
             .Where(static m => m is not null);

        var combined = context.CompilationProvider.Combine(classDeclarations.Collect());

        // Generate source code
        context.RegisterSourceOutput(
            classDeclarations,
            static (spc, source) => Execute(source, spc));

        static bool IsSyntaxTargetForGeneration(SyntaxNode node)
            => node is StructDeclarationSyntax m;

        static Properties? GetStructTransformation(GeneratorAttributeSyntaxContext context, GenerationMode generationMode)
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
                        return GetAttributeToGenerate(context, attributeSyntax.ArgumentList, generationMode);
                    }
                }
            }

            // we didn't find the attribute we were looking for
            return null;
        }

        static Properties? GetAttributeToGenerate(GeneratorAttributeSyntaxContext context, AttributeArgumentListSyntax arguments, GenerationMode generationMode)
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
            var properties = new Properties(fileNamespace, context.TargetSymbol.Name, generationMode);

            var assembly = typeof(SourceGenerationHelper).Assembly;
            var filesToLoad = new Dictionary<string, GenerationMode>();
            if (generationMode is GenerationMode.AttributeNames or GenerationMode.AttributeNamesAndValues)
            {
                filesToLoad.Add($"{assembly.GetName().Name!}.Resources.AttributeNames.{attrNamespace}.md", GenerationMode.AttributeNames);
            }

            foreach (var file in filesToLoad)
            {
                var resourceStream = assembly.GetManifestResourceStream(file.Key);

                if (resourceStream is null)
                {
                    continue;
                }

                var streamReader = new StreamReader(resourceStream);
                streamReader.ReadLine();
                streamReader.ReadLine();
                streamReader.ReadLine();
                while (!streamReader.EndOfStream)
                {
                    if (file.Value == GenerationMode.AttributeNames)
                    {
                        properties.AttributeNames.Add(streamReader.ReadLine()!.Trim('|').Trim());
                    }
                }
            }

            return properties;
        }

        static void Execute(Properties? properties, SourceProductionContext context)
        {
            if (properties is { } value)
            {
                if (value.GenerationMode == GenerationMode.AttributeNames ||
                    value.GenerationMode == GenerationMode.AttributeNamesAndValues)
                {
                    // generate the source code and add it to the output
                    string result = SourceGenerationHelper.GenerateAttributeClass(value, value.AttributeNames);

                    // Create a separate partial class file for each enum
                    context.AddSource($"OtelAttributes.{value.StructName}.AttributeNames.g.cs", SourceText.From(result, Encoding.UTF8));
                }

                if (value.GenerationMode == GenerationMode.AttributeValues ||
                    value.GenerationMode == GenerationMode.AttributeNamesAndValues)
                {
                    // generate the source code and add it to the output
                    string result = SourceGenerationHelper.GenerateAttributeClass(value, null);

                    // Create a separate partial class file for each enum
                    context.AddSource($"OtelAttributes.{value.StructName}.Attributevalues.g.cs", SourceText.From(result, Encoding.UTF8));
                }
            }
        }
    }
}
