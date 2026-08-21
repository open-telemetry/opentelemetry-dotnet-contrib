// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Sampler.BottomFloor;
using OpenTelemetry.Trace;

namespace Examples.BottomFloor;

// This example drives a synthetic single-process workload of correlated logs and
// spans through the Bottom-Floor log sampler and then checks that the sampler's
// Horvitz-Thompson adjusted counts recover the arrival counts of the stationary
// generating distribution.
//
// Each window emits roughly one hundred recording spans and one thousand logs,
// with callsites and span operations drawn from a fixed Zipfian law. The sampler
// keeps only a small budget per window, yet summing the adjusted counts it stamps
// on the survivors recovers the full arrival counts, both for the whole log stream
// and, separately, within spans.
internal static class Program
{
    private const string SourceName = "Examples.BottomFloor";

    // Workload shape: one window is one request-processing tick.
    private const int Windows = 300;
    private const int SpansPerWindow = 100;
    private const double InSpanLogMean = 8.0;
    private const int OutOfSpanLogsPerWindow = 200;

    // The generating distribution: a Zipfian law over callsites and operations is
    // the stationary math the recovered counts are checked against.
    private const int CallsiteCount = 12;
    private const double CallsiteZipfExponent = 1.1;
    private const int OperationCount = 4;
    private const double OperationZipfExponent = 0.7;

    // Sampling budgets, both far below the per-window arrivals so the sampler
    // actually subsamples and the estimators are exercised.
    private const int LogBudget = 100;
    private const int PerSpanBudget = 5;

    public static void Main()
    {
        var random = new Random(20260708);

        var callsites = Enumerable.Range(1, CallsiteCount)
            .Select(i => FormattableString.Invariant($"App.Callsite{i:D2}"))
            .ToArray();
        var operations = Enumerable.Range(1, OperationCount)
            .Select(i => FormattableString.Invariant($"Operation{i}"))
            .ToArray();

        var callsiteWeights = ZipfWeights(CallsiteCount, CallsiteZipfExponent);
        var operationWeights = ZipfWeights(OperationCount, OperationZipfExponent);
        var callsiteCdf = Cdf(callsiteWeights);
        var operationCdf = Cdf(operationWeights);

        // Ground truth, accumulated at emit before any sampling.
        var actualByCallsite = new long[CallsiteCount];
        var actualInSpanByOp = new long[OperationCount];
        long totalLogs = 0;
        long totalSpans = 0;

        var options = new BottomFloorLogSamplerOptions
        {
            Budget = LogBudget,
            MaxLogsPerSpanPerWindow = PerSpanBudget,
        };

        using var stats = new StatisticsExporter(options);

        // Spans are recorded so that in-context logs carry a span id; the sampler
        // groups per-span coverage by that id and needs no span exporter.
        using var source = new ActivitySource(SourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(SourceName)
            .Build();

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
            for (var s = 0; s < SpansPerWindow; s++)
            {
                var op = Sample(operationCdf, random);
                using (source.StartActivity(operations[op]))
                {
                    var count = SamplePoisson(InSpanLogMean, random);
                    for (var i = 0; i < count; i++)
                    {
                        var c = Sample(callsiteCdf, random);
                        Emit(loggers[c], operations[op]);
                        actualByCallsite[c]++;
                        actualInSpanByOp[op]++;
                        totalLogs++;
                    }
                }

                totalSpans++;
            }

            for (var i = 0; i < OutOfSpanLogsPerWindow; i++)
            {
                var c = Sample(callsiteCdf, random);
                Emit(loggers[c]);
                actualByCallsite[c]++;
                totalLogs++;
            }

            // One export batch is one sampling window; flushing closes it.
            processor.ForceFlush();
        }

        Report(
            callsites,
            operations,
            callsiteWeights,
            actualByCallsite,
            actualInSpanByOp,
            stats,
            totalLogs,
            totalSpans);
    }

    // The operation is emitted as an ordinary structured log property. The
    // sampler preserves the attributes of every record it keeps, so the report
    // below can group survivors by operation without the sampler knowing
    // anything about span names.
    private static void Emit(ILogger logger, string? operation = null)
    {
#pragma warning disable CA1848, CA1873
        if (operation == null)
        {
            logger.LogInformation("synthetic event");
        }
        else
        {
            logger.LogInformation("synthetic event in {Operation}", operation);
        }
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

    private static int SamplePoisson(double mean, Random random)
    {
        // Knuth's algorithm; the mean is small, so the loop is short.
        var threshold = Math.Exp(-mean);
        var k = 0;
        var product = 1.0;
        do
        {
            k++;
            product *= random.NextDouble();
        }
        while (product > threshold);

        return k - 1;
    }

    private static void Report(
        string[] callsites,
        string[] operations,
        double[] callsiteProbabilities,
        long[] actualByCallsite,
        long[] actualInSpanByOp,
        StatisticsExporter stats,
        long totalLogs,
        long totalSpans)
    {
        Console.WriteLine(FormattableString.Invariant(
            $"Windows={Windows}  spans={totalSpans}  logs={totalLogs}  log budget={LogBudget}/window  per-span budget={PerSpanBudget}"));
        Console.WriteLine(FormattableString.Invariant(
            $"Forwarded={stats.Forwarded} (stream-kept {stats.StreamKept}, span-only {stats.SpanOnly})  whole-stream compression={(double)totalLogs / Math.Max(1, stats.StreamKept):F1}x  max logs kept in any span={stats.MaxSpanKept} (budget {PerSpanBudget})"));
        Console.WriteLine();

        Console.WriteLine("Whole-stream recovery (sum of otel.logs.adjusted_count vs actual arrivals)");
        Console.WriteLine("  callsite         p(theory)   share   actual    estimate   rel.err");
        var streamEstTotal = 0.0;
        for (var i = 0; i < callsites.Length; i++)
        {
            var actual = actualByCallsite[i];
            var estimate = stats.StreamEstimateFor(callsites[i]);
            streamEstTotal += estimate;
            var share = (double)actual / Math.Max(1, totalLogs);
            Console.WriteLine(FormattableString.Invariant(
                $"  {callsites[i],-14}  {callsiteProbabilities[i],8:F4}  {share,6:F3}  {actual,8}  {estimate,10:F1}  {RelErr(actual, estimate),7:P2}"));
        }

        Console.WriteLine(FormattableString.Invariant(
            $"  {"TOTAL",-14}  {1.0,8:F4}  {1.0,6:F3}  {totalLogs,8}  {streamEstTotal,10:F1}  {RelErr(totalLogs, streamEstTotal),7:P2}"));
        Console.WriteLine();

        Console.WriteLine("Per-span recovery (sum of otel.span_logs.adjusted_count vs in-span arrivals)");
        Console.WriteLine("  operation     actual    estimate   rel.err");
        var spanActualTotal = 0L;
        var spanEstTotal = 0.0;
        for (var i = 0; i < operations.Length; i++)
        {
            var actual = actualInSpanByOp[i];
            var estimate = stats.SpanEstimateForOperation(operations[i]);
            spanActualTotal += actual;
            spanEstTotal += estimate;
            Console.WriteLine(FormattableString.Invariant(
                $"  {operations[i],-10}  {actual,8}  {estimate,10:F1}  {RelErr(actual, estimate),7:P2}"));
        }

        Console.WriteLine(FormattableString.Invariant(
            $"  {"TOTAL",-10}  {spanActualTotal,8}  {spanEstTotal,10:F1}  {RelErr(spanActualTotal, spanEstTotal),7:P2}"));
    }

    private static double RelErr(double actual, double estimate)
    {
        return actual == 0.0 ? 0.0 : (estimate - actual) / actual;
    }

    // Accumulates the sampler's stamped adjusted counts to reconstruct the
    // arrival counts of each estimator. Missing count means fully included (one);
    // a stream count of zero marks a span-only record.
    private sealed class StatisticsExporter : BaseExporter<LogRecord>
    {
        private const string OperationAttribute = "Operation";

        private readonly string streamAttribute;
        private readonly string spanAttribute;
        private readonly Dictionary<string, double> streamByCallsite = new(StringComparer.Ordinal);
        private readonly Dictionary<string, double> spanByOperation = new(StringComparer.Ordinal);

        public StatisticsExporter(BottomFloorLogSamplerOptions options)
        {
            this.streamAttribute = options.AdjustedCountAttribute;
            this.spanAttribute = options.SpanAdjustedCountAttribute;
        }

        public long Forwarded { get; private set; }

        public long StreamKept { get; private set; }

        public long SpanOnly { get; private set; }

        public int MaxSpanKept { get; private set; }

        public double StreamEstimateFor(string callsite)
        {
            return this.streamByCallsite.TryGetValue(callsite, out var value) ? value : 0.0;
        }

        public double SpanEstimateForOperation(string operation)
        {
            return this.spanByOperation.TryGetValue(operation, out var value) ? value : 0.0;
        }

        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            var keptPerSpan = new Dictionary<ActivitySpanId, int>();

            foreach (var record in batch)
            {
                this.Forwarded++;

                var stream = 1.0;
                var span = double.NaN;
                string? operation = null;
                if (record.Attributes != null)
                {
                    foreach (var attribute in record.Attributes)
                    {
                        if (string.Equals(attribute.Key, this.streamAttribute, StringComparison.Ordinal) && attribute.Value is double streamValue)
                        {
                            stream = streamValue;
                        }
                        else if (string.Equals(attribute.Key, this.spanAttribute, StringComparison.Ordinal) && attribute.Value is double spanValue)
                        {
                            span = spanValue;
                        }
                        else if (string.Equals(attribute.Key, OperationAttribute, StringComparison.Ordinal) && attribute.Value is string name)
                        {
                            operation = name;
                        }
                    }
                }

                var callsite = record.CategoryName ?? "(none)";
                this.streamByCallsite[callsite] = (this.streamByCallsite.TryGetValue(callsite, out var streamSum) ? streamSum : 0.0) + stream;

                if (stream > 0.0)
                {
                    this.StreamKept++;
                }
                else
                {
                    this.SpanOnly++;
                }

                if (operation != null)
                {
                    var spanContribution = double.IsNaN(span) ? 1.0 : span;
                    this.spanByOperation[operation] = (this.spanByOperation.TryGetValue(operation, out var spanSum) ? spanSum : 0.0) + spanContribution;

                    if (spanContribution > 0.0 && record.SpanId != default)
                    {
                        keptPerSpan[record.SpanId] = (keptPerSpan.TryGetValue(record.SpanId, out var kept) ? kept : 0) + 1;
                    }
                }
            }

            foreach (var kept in keptPerSpan.Values)
            {
                if (kept > this.MaxSpanKept)
                {
                    this.MaxSpanKept = kept;
                }
            }

            return ExportResult.Success;
        }
    }
}
