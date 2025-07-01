// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text;

namespace OpenTelemetry.SemanticConventions.Generator;

internal static class SourceGenerationHelper
{
    internal static string GenerateAttributeClass(Properties enumToGenerate, KeyValuePair<GenerationMode, List<string>> items)
    {
        if (items.Value is null || items.Value.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.Append(@"
namespace ")
            .Append(enumToGenerate.FileNamespace)
            .Append(@";

#pragma warning disable CS8981
#pragma warning disable IDE1006
#pragma warning disable SA1629
internal partial struct ")
            .Append(enumToGenerate.StructName)
            .AppendLine(@"
{");




        foreach (var attribute in items.Value)
        {
            var properties = attribute.Split('|');
            var identifiers = properties[0].Trim().Split('.');
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
                    sb.Append(@"        /// <summary>
        /// ")
                        .Append(properties[2].Trim())
                        .Append(@"
        /// </summary>
        /// <remarks>This is the key for an attribute/tag.</remarks>
        internal const string ")
                        .Append(name)
                        .Append(" = \"")
                        .Append(properties[3].Trim())
                        .AppendLine("\";");
                }
            }

            for (int i = 1; i < position; i++)
            {
                sb.AppendLine(@"    }");
            }
        sb.AppendLine("}")
            .AppendLine(@"#pragma warning restore SA1629
    #pragma warning restore IDE1006
#pragma warning restore CS8981");

        return sb.ToString();
    }
}
