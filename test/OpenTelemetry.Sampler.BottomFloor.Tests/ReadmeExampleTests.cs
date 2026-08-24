// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Sampler.BottomFloor.Tests;

/// <summary>
/// Guards the usage snippets in the package README. Documented code is not
/// compiled by the build, so a snippet can silently rot into something that does
/// not compile or, worse, compiles and computes the wrong answer. Each snippet
/// below is the real, compiled, executed code; a final test asserts the README
/// still matches it character for character.
/// </summary>
[Collection("LogPipeline")]
public class ReadmeExampleTests
{
    private const string ProcessorHeading = "## Sampling OpenTelemetry logs";

    [Fact]
    public void DocumentedProcessorExample_BuildsAWorkingPipeline()
    {
        // <readme:processor>
        var options = new BottomFloorLogSamplerOptions
        {
            Budget = 100,
        };

        // The sampler decorates an exporter rather than replacing it, forwarding only
        // the records it keeps. Any BaseExporter<LogRecord> works; substitute the OTLP
        // exporter for a real deployment.
        BaseExporter<LogRecord> innerExporter = new ConsoleLogRecordExporter(new ConsoleExporterOptions());

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddOpenTelemetry(loggerOptions =>
            loggerOptions.AddProcessor(new BottomFloorLogRecordProcessor(
                innerExporter,
                options,
                maxExportBatchSize: 2048,
                scheduledDelayMilliseconds: 5000))));

        var logger = loggerFactory.CreateLogger("MyCompany.MyApp");

        // </readme:processor>
        Assert.NotNull(logger);

        // The documented wiring must survive an actual emit and flush. Sampling
        // behaviour itself is asserted in BottomFloorLogRecordProcessorTests.
        logger.LogInformation("message");
    }

    [Theory]
    [InlineData(ProcessorHeading, "processor")]
    public void Readme_MatchesTheCompiledExample(string heading, string marker)
    {
        var documented = ReadDocumentedSnippet(heading);
        var compiled = ReadCompiledSnippet(marker);

        Assert.Equal(compiled, documented);
    }

    private static string ReadDocumentedSnippet(string heading)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "README.md");
        Assert.True(File.Exists(path), $"README.md was not copied next to the tests ({path}).");

        var lines = File.ReadAllLines(path);
        var headingIndex = Array.FindIndex(lines, l => string.Equals(l.Trim(), heading, StringComparison.Ordinal));
        Assert.True(headingIndex >= 0, $"README.md no longer contains the heading '{heading}'.");

        var open = Array.FindIndex(lines, headingIndex, l => l.Trim().StartsWith("```csharp", StringComparison.Ordinal));
        Assert.True(open >= 0, $"No C# fence follows '{heading}' in README.md.");

        var close = Array.FindIndex(lines, open + 1, l => string.Equals(l.Trim(), "```", StringComparison.Ordinal));
        Assert.True(close >= 0, $"The C# fence under '{heading}' in README.md is not closed.");

        // The snippets open with using directives the test file declares elsewhere.
        // A `using var` statement is code, not a directive, so it must be kept.
        var body = lines[(open + 1)..close]
            .Where(l => !(l.TrimStart().StartsWith("using ", StringComparison.Ordinal) && !l.Contains('=', StringComparison.Ordinal)));
        return Normalize(body);
    }

    private static string ReadCompiledSnippet(string marker)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ReadmeExampleTests.cs");
        Assert.True(File.Exists(path), $"The test source was not copied next to the tests ({path}).");

        var begin = $"// <readme:{marker}>";
        var end = $"// </readme:{marker}>";

        var lines = File.ReadAllLines(path);
        var beginIndex = Array.FindIndex(lines, l => string.Equals(l.Trim(), begin, StringComparison.Ordinal));
        var endIndex = Array.FindIndex(lines, l => string.Equals(l.Trim(), end, StringComparison.Ordinal));
        Assert.True(beginIndex >= 0 && endIndex > beginIndex, $"The '{marker}' snippet markers are missing from the test source.");

        return Normalize(lines[(beginIndex + 1)..endIndex]);
    }

    /// <summary>
    /// Removes surrounding blank lines and the common indent, so the comparison
    /// is about the code rather than where it happens to be nested.
    /// </summary>
    /// <param name="lines">The raw lines.</param>
    /// <returns>The normalized text.</returns>
    private static string Normalize(IEnumerable<string> lines)
    {
        var trimmed = lines.Select(l => l.TrimEnd()).ToList();

        while (trimmed.Count > 0 && trimmed[0].Length == 0)
        {
            trimmed.RemoveAt(0);
        }

        while (trimmed.Count > 0 && trimmed[^1].Length == 0)
        {
            trimmed.RemoveAt(trimmed.Count - 1);
        }

        var indent = trimmed
            .Where(l => l.Length > 0)
            .Select(l => l.Length - l.TrimStart().Length)
            .DefaultIfEmpty(0)
            .Min();

        return string.Join("\n", trimmed.Select(l => l.Length == 0 ? l : l[indent..]));
    }
}
