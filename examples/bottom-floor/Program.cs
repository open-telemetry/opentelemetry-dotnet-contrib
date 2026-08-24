// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Sampler.BottomFloor;

namespace Examples.BottomFloor;

// This example drives a synthetic log workload through the Bottom-Floor log
// sampler and then checks that the sampler's Horvitz-Thompson adjusted counts
// recover the arrival counts of the stationary generating distribution.
//
// Each window emits one thousand logs whose callsites are drawn from a fixed
// Zipfian law. The sampler keeps only one hundred of them, yet summing the
// adjusted counts it stamps on the survivors recovers the full arrival count of
// every callsite, including the rare ones a head-based sampler would lose.
internal static class Program
{
    // Workload shape: one window is one request-processing tick.
    private const int Windows = 300;
    private const int LogsPerWindow = 1000;

    // The generating distribution: a Zipfian law over callsites is the
    // stationary math the recovered counts are checked against.
    private const int CallsiteCount = 12;
    private const double CallsiteZipfExponent = 1.1;

    // Far below the per-window arrivals, so the sampler actually subsamples and
    // the estimator is exercised.
    private const int LogBudget = 100;

    public static void Main()
    {
        var random = new Random(20260708);

        var callsites = Enumerable.Range(1, CallsiteCount)
            .Select(i => FormattableString.Invariant($"App.Callsite{i:D2}"))
            .ToArray();

        var callsiteWeights = ZipfWeights(CallsiteCount, CallsiteZipfExponent);
        var callsiteCdf = Cdf(callsiteWeights);

        // Ground truth, accumulated at emit before any sampling.
        var actualByCallsite = new long[CallsiteCount];
        long totalLogs = 0;

        var options = new BottomFloorLogSamplerOptions
        {
            Budget = LogBudget,
        };

        using var stats = new StatisticsExporter(options);

        using var processor = new BottomFloorLogRecordProcessor(
            stats,
            options,
            maxExportBatchSize: 4096,
            scheduledDelayMilliseconds: 600000,
            maxQueueSize: 8192);

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddOpenTelemetry(logging => logging.AddProcessor(processor));
        });

        var loggers = callsites.Select(loggerFactory.CreateLogger).ToArray();

        for (var window = 0; window < Windows; window++)
        {
            for (var i = 0; i < LogsPerWindow; i++)
            {
                var c = Sample(callsiteCdf, random);
                Emit(loggers[c]);
                actualByCallsite[c]++;
                totalLogs++;
            }

            // One export batch is one sampling window; flushing closes it.
            processor.ForceFlush();
        }

        Report(callsites, callsiteWeights, actualByCallsite, stats, totalLogs);
    }

    private static void Emit(ILogger logger)
    {
#pragma warning disable CA1848, CA1873
        logger.LogInformation("synthetic event");
#pragma warning restore CA1848, CA1873
    }

    private static double[] ZipfWeights(int n, double exponent)
    {
        var weights = new double[n];
        var sum = 0.0;
        for (var i = 0; i < n; i++)
        {
            weights[i] = 1.0 / Math.Pow(i + 1, exponent);
            sum += weights[i];
        }

        for (var i = 0; i < n; i++)
        {
            weights[i] /= sum;
        }

        return weights;
    }

    private static double[] Cdf(double[] probabilities)
    {
        var cdf = new double[probabilities.Length];
        var acc = 0.0;
        for (var i = 0; i < probabilities.Length; i++)
        {
            acc += probabilities[i];
            cdf[i] = acc;
        }

        return cdf;
    }

    private static int Sample(double[] cdf, Random random)
    {
        var u = random.NextDouble();
        for (var i = 0; i < cdf.Length; i++)
        {
            if (u <= cdf[i])
            {
                return i;
            }
        }

        return cdf.Length - 1;
    }

    private static void Report(
        string[] callsites,
        double[] callsiteProbabilities,
        long[] actualByCallsite,
        StatisticsExporter stats,
        long totalLogs)
    {
        Console.WriteLine(FormattableString.Invariant(
            $"Windows={Windows}  logs={totalLogs}  budget={LogBudget}/window"));
        Console.WriteLine(FormattableString.Invariant(
            $"Forwarded={stats.Forwarded}  compression={(double)totalLogs / Math.Max(1, stats.Forwarded):F1}x"));
        Console.WriteLine();

        Console.WriteLine("Recovery (sum of otel.logs.adjusted_count vs actual arrivals)");
        Console.WriteLine("  callsite         p(theory)   share   actual    estimate   rel.err");
        var estimateTotal = 0.0;
        for (var i = 0; i < callsites.Length; i++)
        {
            var actual = actualByCallsite[i];
            var estimate = stats.EstimateFor(callsites[i]);
            estimateTotal += estimate;
            var share = (double)actual / Math.Max(1, totalLogs);
            Console.WriteLine(FormattableString.Invariant(
                $"  {callsites[i],-14}  {callsiteProbabilities[i],8:F4}  {share,6:F3}  {actual,8}  {estimate,10:F1}  {RelErr(actual, estimate),7:P2}"));
        }

        Console.WriteLine(FormattableString.Invariant(
            $"  {"TOTAL",-14}  {1.0,8:F4}  {1.0,6:F3}  {totalLogs,8}  {estimateTotal,10:F1}  {RelErr(totalLogs, estimateTotal),7:P2}"));
    }

    private static double RelErr(double actual, double estimate)
    {
        return actual == 0.0 ? 0.0 : (estimate - actual) / actual;
    }

    // Accumulates the sampler's stamped adjusted counts to reconstruct the
    // arrival count of each callsite. A missing attribute means the record was
    // fully included, which is an adjusted count of one.
    private sealed class StatisticsExporter : BaseExporter<LogRecord>
    {
        private readonly string adjustedCountAttribute;
        private readonly Dictionary<string, double> estimateByCallsite = new(StringComparer.Ordinal);

        public StatisticsExporter(BottomFloorLogSamplerOptions options)
        {
            this.adjustedCountAttribute = options.AdjustedCountAttribute;
        }

        public long Forwarded { get; private set; }

        public double EstimateFor(string callsite)
        {
            return this.estimateByCallsite.TryGetValue(callsite, out var value) ? value : 0.0;
        }

        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            foreach (var record in batch)
            {
                this.Forwarded++;

                var adjustedCount = 1.0;
                if (record.Attributes != null)
                {
                    foreach (var attribute in record.Attributes)
                    {
                        if (string.Equals(attribute.Key, this.adjustedCountAttribute, StringComparison.Ordinal) && attribute.Value is double value)
                        {
                            adjustedCount = value;
                            break;
                        }
                    }
                }

                var callsite = record.CategoryName ?? "(none)";
                this.estimateByCallsite[callsite] =
                    (this.estimateByCallsite.TryGetValue(callsite, out var sum) ? sum : 0.0) + adjustedCount;
            }

            return ExportResult.Success;
        }
    }
}
