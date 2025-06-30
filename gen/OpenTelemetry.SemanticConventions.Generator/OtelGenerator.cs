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
                 transform: static (ctx, _) => GetStructTransformation(ctx))
             .Where(static m => m is not null);

        var combined = context.CompilationProvider.Combine(classDeclarations.Collect());

        // Generate source code for each enum found
        context.RegisterSourceOutput(
            classDeclarations,
            static (spc, source) => Execute(source, spc));

        static bool IsSyntaxTargetForGeneration(SyntaxNode node)
            => node is StructDeclarationSyntax m;

        static Properties? GetStructTransformation(GeneratorAttributeSyntaxContext context)
        {
            BaseTypeDeclarationSyntax structDeclarationSyntax = context.TargetNode is StructDeclarationSyntax syntax ?
               syntax :
               throw new NotSupportedException("Target Node is not supported");

            foreach (AttributeListSyntax attributeListSyntax in structDeclarationSyntax.AttributeLists)
            {
                foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
                {
                    if (attributeSyntax.Name.ToString() == "OtelAttributeNamespace" && attributeSyntax.ArgumentList != null)
                    {
                        return GetAttributeToGenerate(context, attributeSyntax.ArgumentList);
                    }
                }
            }

            // we didn't find the attribute we were looking for
            return null;
        }

        static Properties? GetAttributeToGenerate(GeneratorAttributeSyntaxContext context, AttributeArgumentListSyntax arguments)
        {
            string? attrNamespace = null;

            int i = 0;
            foreach (AttributeArgumentSyntax attributeData in arguments.Arguments)
            {
                // This is the right attribute, check the constructor arguments
                if (attributeData.NameEquals?.Name.Identifier.ValueText == "AttributeNamespace" ||
                    attributeData.NameColon?.Name.Identifier.ValueText == "AttributeNamespace" ||
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

            var assembly = typeof(SourceGenerationHelper).Assembly;
            var fileName = $"{assembly.GetName().Name!}.Resources.{attrNamespace}.md";
            var resourceStream = assembly.GetManifestResourceStream(fileName);

            if (resourceStream is null)
            {
                return null;
            }

            var streamReader = new StreamReader(resourceStream);
            streamReader.ReadLine();
            streamReader.ReadLine();
            var values = new List<string>();
            while (!streamReader.EndOfStream)
            {
                values.Add(streamReader.ReadLine()!.Trim('|'));
            }

            var fileNamespace = context.TargetSymbol.ContainingNamespace.ToDisplayString();
            return new Properties(fileNamespace, context.TargetSymbol.Name, values);
        }

        static void Execute(Properties? enumToGenerate, SourceProductionContext context)
        {
            if (enumToGenerate is { } value)
            {
                // generate the source code and add it to the output
                string result = SourceGenerationHelper.GenerateAttributeClass(value);

                // Create a separate partial class file for each enum
                context.AddSource($"OtelAttributes.{value.AttributeName}.g.cs", SourceText.From(result, Encoding.UTF8));
            }
        }
    }
}
