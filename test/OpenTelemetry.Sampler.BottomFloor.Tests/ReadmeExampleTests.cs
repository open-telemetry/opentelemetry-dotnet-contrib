// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using CapturedRecord = OpenTelemetry.Sampler.BottomFloor.Tests.BottomFloorLogExporterTests.CapturedRecord;
using CapturingExporter = OpenTelemetry.Sampler.BottomFloor.Tests.BottomFloorLogExporterTests.CapturingExporter;

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
    private const string SpanCoverageHeading = "## Per-span log coverage";
    private const string SamplerHeading = "## Using the sampling algorithm directly";

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

    [Fact]
    public void DocumentedSpanCoverageExample_EnablesPerSpanCoverage()
    {
        // <readme:span-coverage>
        var options = new BottomFloorLogSamplerOptions
        {
            Budget = 100,
            MaxLogsPerSpanPerWindow = 5,
        };

        // </readme:span-coverage>
        Assert.Equal(100, options.Budget);
        Assert.Equal(5, options.MaxLogsPerSpanPerWindow);

        // The snippet's purpose is to turn on a feature that is off by default.
        Assert.NotEqual(new BottomFloorLogSamplerOptions().MaxLogsPerSpanPerWindow, options.MaxLogsPerSpanPerWindow);

        // And the documented values must be a configuration the pipeline accepts.
        var innerExporter = new CapturingExporter(new List<CapturedRecord>());
        using var processor = new BottomFloorLogRecordProcessor(innerExporter, options, maxExportBatchSize: 2048);
    }

    [Fact]
    public void DocumentedSamplerExample_RecoversTheArrivalCount()
    {
        // Scaffolding the snippet refers to but does not define.
        var rng = new Random(17);
        var callsites = Enumerable.Range(1, 12).Select(i => ($"App.Callsite{i:00}", i)).ToArray();
        var cdf = BuildZipfCdf(callsites.Length);

        var windows = new List<List<MyEvent>>();
        var arrivals = 0L;
        for (var w = 0; w < 200; w++)
        {
            var window = new List<MyEvent>();
            for (var n = 0; n < 1000; n++)
            {
                var u = rng.NextDouble();
                var index = Array.FindIndex(cdf, x => u <= x);
                var (category, eventId) = callsites[index < 0 ? cdf.Length - 1 : index];
                window.Add(new MyEvent(category, eventId));
                arrivals++;
            }

            windows.Add(window);
        }

        var estimated = 0.0;
        var exported = 0L;

        void Export(MyEvent item, double adjustedCount)
        {
            estimated += adjustedCount;
            exported++;
        }

        // <readme:sampler>
        var sampler = new BottomFloorSampler<(string Category, int EventId)>(budget: 100);
        var buffered = new Dictionary<long, MyEvent>();

        foreach (var window in windows)
        {
            foreach (var item in window)
            {
                var outcome = sampler.Offer((item.Category, item.EventId));
                if (!outcome.Admitted)
                {
                    continue;
                }

                // Honour the eviction, so the buffer holds exactly the reservoir.
                if (outcome.Evicted)
                {
                    buffered.Remove(outcome.EvictedToken);
                }

                buffered[outcome.Token] = item;
            }

            var summary = sampler.CloseWindow();
            foreach (var kept in summary.KeptItems)
            {
                var estimate = summary.Estimates[kept.Callsite];

                // The per-record adjusted count is 1 / inclusion probability. Summed
                // over a callsite's kept records it reproduces estimate.EstimatedCount,
                // that callsite's estimated arrival count for the window.
                Export(buffered[kept.Token], 1.0 / estimate.InclusionProbability);
            }

            // The next window starts from an empty reservoir, so nothing carries over.
            buffered.Clear();
        }

        // </readme:sampler>

        // The snippet must actually subsample, or it proves nothing.
        Assert.True(exported < arrivals / 5, $"expected heavy subsampling, exported {exported} of {arrivals}");

        // And its adjusted counts must recover what was thrown away. Stamping
        // EstimatedCount per record instead would overshoot by roughly ninefold
        // here, so this tolerance is far tighter than that failure mode.
        var relativeError = Math.Abs(estimated - arrivals) / arrivals;
        Assert.True(relativeError < 0.05, $"relative error {relativeError:P2} exceeded 5% (estimated {estimated:F0}, arrivals {arrivals})");

        // The buffer is drained every window, so nothing accumulates across them.
        Assert.Empty(buffered);
    }

    [Theory]
    [InlineData(ProcessorHeading, "processor")]
    [InlineData(SpanCoverageHeading, "span-coverage")]
    [InlineData(SamplerHeading, "sampler")]
    public void Readme_MatchesTheCompiledExample(string heading, string marker)
    {
        var documented = ReadDocumentedSnippet(heading);
        var compiled = ReadCompiledSnippet(marker);

        Assert.Equal(compiled, documented);
    }

    private static double[] BuildZipfCdf(int count)
    {
        var cdf = new double[count];
        var total = 0.0;
        for (var i = 0; i < count; i++)
        {
            total += 1.0 / (i + 1);
            cdf[i] = total;
        }

        for (var i = 0; i < count; i++)
        {
            cdf[i] /= total;
        }

        return cdf;
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

    private sealed class MyEvent
    {
        public MyEvent(string category, int eventId)
        {
            this.Category = category;
            this.EventId = eventId;
        }

        public string Category { get; }

        public int EventId { get; }
    }
}
