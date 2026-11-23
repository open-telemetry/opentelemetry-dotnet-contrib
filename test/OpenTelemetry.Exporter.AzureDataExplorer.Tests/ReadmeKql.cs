// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace OpenTelemetry.Exporter.AzureDataExplorer.Tests;

/// <summary>
/// Extracts the KQL management commands documented in the component README so
/// integration tests create tables exactly as users are told to.
/// </summary>
internal static class ReadmeKql
{
    public static IReadOnlyList<string> GetCommands()
    {
        using var stream = typeof(ReadmeKql).Assembly.GetManifestResourceStream("README.md")
            ?? throw new InvalidOperationException("README.md embedded resource was not found.");
        using var reader = new StreamReader(stream);

        var commands = new List<string>();
        var current = new StringBuilder();
        var inKqlBlock = false;

        void Flush()
        {
            if (current.Length > 0)
            {
                commands.Add(current.ToString().Trim());
                current.Clear();
            }
        }

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                Flush();
                inKqlBlock = !inKqlBlock && string.Equals(trimmed, "```kql", StringComparison.Ordinal);
                continue;
            }

            if (!inKqlBlock)
            {
                continue;
            }

            if (trimmed.Length == 0)
            {
                Flush();
            }
            else
            {
                current.Append(line).Append('\n');
            }
        }

        return commands;
    }
}
