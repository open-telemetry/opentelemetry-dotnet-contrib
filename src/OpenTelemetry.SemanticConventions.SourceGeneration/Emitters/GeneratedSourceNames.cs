// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace OpenTelemetry.SemanticConventions.SourceGeneration.Emitters;

internal static class GeneratedSourceNames
{
    public static string ForPartialType(string containingNamespace, string className)
    {
        if (string.IsNullOrEmpty(containingNamespace))
        {
            return Sanitize(className) + ".g.cs";
        }

        return Sanitize(containingNamespace) + "." + Sanitize(className) + ".g.cs";
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }

        return builder.ToString();
    }
}
