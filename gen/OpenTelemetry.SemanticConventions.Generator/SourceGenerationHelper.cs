// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace OpenTelemetry.SemanticConventions.Generator;

public static class SourceGenerationHelper
{
    public static string GenerateAttributeClass(Properties enumToGenerate)
    {
        var sb = new StringBuilder();
        sb.Append(@"
namespace ")
            .Append(enumToGenerate.FileNamespace)
            .Append(@";

internal partial struct ")
            .Append(enumToGenerate.AttributeName)
            .AppendLine(@" 
{
    #pragma warning disable CS8981
    #pragma warning disable SA1629");

        foreach (var attribute in enumToGenerate.Values)
        {
            var identifiers = attribute.Split('.');
            var position = 1;
            foreach (var name in identifiers)
            {
                if (position < identifiers.Length)
                {
                    sb.Append(@"    internal partial struct ")
                        .Append(name)
                        .AppendLine(@"
    {");
                    position++;
                }
                else
                {
                    sb.Append("internal const string ")
                        .Append(name)
                        .Append(" = \"")
                        .Append(attribute)
                        .Append("\";")
                        .AppendLine();
                }
            }

            for (int i = 1; i < position; i++)
            {
                sb.AppendLine(@"    }");
            }
        }

        sb.AppendLine(@"    #pragma warning restore SA1629
    #pragma warning restore CS8981
}");

        return sb.ToString();
    }
}
