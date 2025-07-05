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
        var extb = new StringBuilder();
        sb.AppendLine(@$"
namespace {enumToGenerate.FileNamespace}

#pragma warning disable CS8981
#pragma warning disable IDE1006
#pragma warning disable SA1629
internal partial struct {enumToGenerate.StructName}
{{");
        extb.AppendLine(@$"internal static partial class {enumToGenerate.StructName}Extensions
{{");

        string openEnumNamespace = string.Empty;
        string openEnum = string.Empty;
        foreach (var attribute in items.Value)
        {
            var properties = attribute.Split('|');
            var identifiers = properties[0].Trim().Split('.');
            var position = 1;

            // Close previous enum if open when new namespace
            if (!string.IsNullOrEmpty(openEnumNamespace) && openEnumNamespace != properties[0])
            {
                for (int i = 1; i <= openEnumNamespace.Split('.').Length; i++)
                {
                    sb.AppendLine(@"    }");
                }

                extb.AppendLine(@"          _ => throw new System.ArgumentOutOfRangeException(nameof(directions), directions, null),
        };
");

                openEnumNamespace = string.Empty;
                openEnum = string.Empty;
            }

            foreach (var name in identifiers)
            {
                var attrNamespace = properties[0];

                // create a struct for the namespace
                if (position < identifiers.Length && openEnumNamespace != attrNamespace)
                {
                    sb.AppendLine(@$"    internal partial struct {name}
    {{");
                }

                // process the final identifier when not member of open enum
                else if (openEnumNamespace != attrNamespace)
                {
                    switch (items.Key)
                    {
                        case GenerationMode.AttributeNames:
                            sb.Append($@"        /// <summary>
        /// {properties[2].Trim()}
        /// </summary>
        /// <remarks>This is the key for an attribute/tag.</remarks>
        internal const string {name} = ")
                                .AppendLine($"\"{properties[3].Trim()}\";");
                            break;
                        case GenerationMode.AttributeValues:
                            openEnumNamespace = properties[0];
                            openEnum = name + "s";
                            sb.AppendLine(@$"        /// <summary>
        /// {properties[2].Trim()}
        /// </summary>
        internal enum {openEnum}
        {{");
                            extb.AppendLine(@$"    internal static string ToAttributeValue(this {enumToGenerate.StructName}.{attrNamespace.Trim()}s {openEnum})
        => {openEnum} switch
        {{");
                            break;
                    }
                }

                // process the final identifier
                if (openEnumNamespace == attrNamespace && position == identifiers.Length)
                {
                    sb.AppendLine(@$"            /// <summary>
            /// {properties[4].Trim()}
            /// </summary>
            {properties[3].Trim().Replace(".", "")},");
                    extb.Append(@$"          {enumToGenerate.StructName}.{attrNamespace.Trim()}s.{properties[3].Trim().Replace(".","")}")
                        .AppendLine($" => \"{properties[3].Trim()}\",");
                }

                position++;
            }

            position--;
            if (items.Key != GenerationMode.AttributeValues)
            {
                for (int i = 1; i < position; i++)
                {
                    sb.AppendLine(@"    }");
                }
            }
        }

        if (!string.IsNullOrEmpty(openEnumNamespace))
        {
            for (int i = 1; i <= openEnumNamespace.Split('.').Length; i++)
            {
                sb.AppendLine(@"    }");
            }

            extb.Append(@$"          _ => throw new System.ArgumentOutOfRangeException(nameof({openEnum}), {openEnum}, null),
        }};");
        }

        extb.AppendLine("}");
        sb.AppendLine("}")
            .AppendLine()
            .Append(extb)
            .AppendLine(@"#pragma warning restore SA1629
#pragma warning restore IDE1006
#pragma warning restore CS8981");

        return sb.ToString();
    }
}
