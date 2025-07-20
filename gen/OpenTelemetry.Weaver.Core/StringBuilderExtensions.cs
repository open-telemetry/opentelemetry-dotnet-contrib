// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace OpenTelemetry.Weaver;

internal static class StringBuilderExtensions
{
    internal static void AppendAttributeName(this StringBuilder sb, string[] properties, string name)
    {
        var prefix = name.Equals("namespace") ? "@" : string.Empty;

        sb.AppendLine($@"        /// <summary>
        /// {properties[2].Trim()}
        /// </summary>
        /// <remarks>This is the key for an attribute/tag.</remarks>");
        if (properties[5].Trim().Equals("deprecated"))
        {
            sb.AppendLine($"        [System.Obsolete(\"{properties[6]}\")]");
        }

        sb.AppendLine($"        internal const string {prefix}{name.Trim()} = \"{properties[3].Trim()}\";");
    }

    internal static void AppendAttributeValue(this StringBuilder sb, string[] properties)
    {
        sb.AppendLine(@$"            /// <summary>
            /// {properties[4].Trim()}
            /// </summary>");

        if (properties[5].Trim().Equals("deprecated"))
        {
            sb.AppendLine($"            [System.Obsolete(\"{properties[6]}\")]");
        }

        sb.AppendLine(@$"            {properties[3].Trim().Replace(".", string.Empty)},");
    }

    internal static void AppendAttributeValueType(this StringBuilder sb, string[] properties, string openEnum)
    {
        sb.AppendLine(@$"        /// <summary>
        /// {properties[2].Trim()}
        /// </summary>
        internal enum {openEnum}
        {{");
    }

    internal static void AppendExtensionSwitchOpener(this StringBuilder sb, string name, string openEnum)
    {
        sb.AppendLine(@$"    internal static string ToAttributeValue(this {name} {openEnum})
        => {openEnum} switch
        {{");
    }

    internal static void AppendExtensionSwitchOption(this StringBuilder sb, string[] properties, string targetName)
    {
        sb.Append(@$"          {targetName}.{properties[3].Trim().Replace(".", string.Empty)}")
                        .AppendLine($" => \"{properties[3].Trim()}\",");
    }

    internal static void AppendExtensionSwitchCloser(this StringBuilder sb, string openEnum)
    {
        sb.AppendLine(@$"          _ => throw new System.ArgumentOutOfRangeException(nameof({openEnum}), {openEnum}, null),
        }};");
    }
}
