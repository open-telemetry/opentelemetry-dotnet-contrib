// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Lambda.Core;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AWSLambda.Tests;

[Collection("TracerProviderDependent")]
public class AWSLambdaWrapperFlushTests : IDisposable
{
    private readonly SampleLambdaContext context = new();

    public AWSLambdaWrapperFlushTests()
    {
        AWSLambdaWrapper.Options = null;
    }

    public void Dispose()
    {
        // Reset the statics AddAWSLambdaConfigurations writes, so tests cannot leak into
        // each other or into the other test classes in this collection.
        Sdk.CreateTracerProviderBuilder().AddAWSLambdaConfigurations();
        AWSLambdaWrapper.Options = null;
    }

    [Fact]
    public void ConfiguredLoggerAndMeterProvidersAreFlushed()
    {
        var logProcessor = new FlushCountingProcessor<LogRecord>();
        var metricReader = new FlushCountingMetricReader();

        using var sdk = CreateSdkWithLogging(logProcessor);
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddReader(metricReader)
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations(options =>
            {
                options.LoggerProvider = sdk.LoggerProvider;
                options.MeterProvider = meterProvider;
            })
            .Build();

        AWSLambdaWrapper.Trace(tracerProvider, (string _, ILambdaContext _) => { }, "input", this.context);

        Assert.Equal(1, logProcessor.FlushCount);
        Assert.Equal(1, metricReader.FlushCount);
    }

    // TraceAsync has its own OnFunctionStop call site, so it is covered separately.
    [Fact]
    public async Task ConfiguredProvidersAreFlushedByTraceAsync()
    {
        var logProcessor = new FlushCountingProcessor<LogRecord>();
        var metricReader = new FlushCountingMetricReader();

        using var sdk = CreateSdkWithLogging(logProcessor);
        using var meterProvider = Sdk.CreateMeterProviderBuilder().AddReader(metricReader).Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations(options =>
            {
                options.LoggerProvider = sdk.LoggerProvider;
                options.MeterProvider = meterProvider;
            })
            .Build();

        await AWSLambdaWrapper.TraceAsync(
            tracerProvider,
            (string _, ILambdaContext _) => Task.CompletedTask,
            "input",
            this.context);

        Assert.Equal(1, logProcessor.FlushCount);
        Assert.Equal(1, metricReader.FlushCount);
    }

    [Fact]
    public void ProvidersNotConfiguredAreNotFlushed()
    {
        var logProcessor = new FlushCountingProcessor<LogRecord>();

        using var sdk = CreateSdkWithLogging(logProcessor);

        // Built, but deliberately not assigned to the options.
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations()
            .Build();

        AWSLambdaWrapper.Trace(tracerProvider, (string _, ILambdaContext _) => { }, "input", this.context);

        Assert.Equal(0, logProcessor.FlushCount);
    }

    // A function that never configures options keeps the unbounded flush it had before.
    [Fact]
    public void WithoutOptionsTheTracerFlushTimeoutIsInfinite()
    {
        var traceProcessor = new FlushCountingProcessor<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(traceProcessor)
            .Build();

        AWSLambdaWrapper.Trace(tracerProvider, (string _, ILambdaContext _) => { }, "input", this.context);

        Assert.Equal(Timeout.Infinite, traceProcessor.LastTimeoutMilliseconds);
    }

    [Fact]
    public void WithOptionsTheTracerFlushTimeoutIsBounded()
    {
        var traceProcessor = new FlushCountingProcessor<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations(options => options.FlushTimeoutMilliseconds = 1234)
            .AddProcessor(traceProcessor)
            .Build();

        AWSLambdaWrapper.Trace(tracerProvider, (string _, ILambdaContext _) => { }, "input", this.context);

        // The configured value reaches the provider, rather than the SDK's own default.
        Assert.Equal(1234, traceProcessor.LastTimeoutMilliseconds);
    }

    // Timeout.Infinite is -1 and must reach the providers as-is, not be treated as no time.
    [Fact]
    public void InfiniteTimeoutDoesNotSkipProviders()
    {
        var metricReader = new FlushCountingMetricReader();

        using var meterProvider = Sdk.CreateMeterProviderBuilder().AddReader(metricReader).Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations(options =>
            {
                options.FlushTimeoutMilliseconds = Timeout.Infinite;
                options.MeterProvider = meterProvider;
            })
            .Build();

        AWSLambdaWrapper.Trace(tracerProvider, (string _, ILambdaContext _) => { }, "input", this.context);

        Assert.Equal(1, metricReader.FlushCount);
    }

    [Fact]
    public void FailingExporterDoesNotFailTheInvocation()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddReader(new ThrowingMetricReader())
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations(options => options.MeterProvider = meterProvider)
            .Build();

        var result = AWSLambdaWrapper.Trace(
            tracerProvider,
            (string _, ILambdaContext _) => "handler result",
            "input",
            this.context);

        Assert.Equal("handler result", result);
    }

    // The flush runs in a finally block, so a failing export must not surface as the
    // invocation's exception.
    [Fact]
    public void FailingExporterDoesNotMaskTheHandlerException()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddReader(new ThrowingMetricReader())
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations(options => options.MeterProvider = meterProvider)
            .Build();

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            AWSLambdaWrapper.Trace(
                tracerProvider,
                (string _, ILambdaContext _) => throw new InvalidOperationException("from the handler"),
                "input",
                this.context));

        Assert.Equal("from the handler", thrown.Message);
    }

    // A negative timeout other than Timeout.Infinite is not a meaningful wait.
    [Fact]
    public void NegativeFlushTimeoutIsRejected()
    {
        var options = new AWSLambdaInstrumentationOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.FlushTimeoutMilliseconds = -500);

        // Timeout.Infinite is the one negative value that must be accepted.
        options.FlushTimeoutMilliseconds = Timeout.Infinite;
        Assert.Equal(Timeout.Infinite, options.FlushTimeoutMilliseconds);
    }

    // Providers are flushed concurrently, so the invocation is held for roughly the slowest
    // provider rather than the sum of all three. Serial flushing would take at least 3x block.
    [Fact]
    public void ProvidersAreFlushedConcurrently()
    {
        const int block = 300;

        var logProcessor = new FlushCountingProcessor<LogRecord>(blockForMilliseconds: block);
        var traceProcessor = new FlushCountingProcessor<Activity>(blockForMilliseconds: block);
        var metricReader = new FlushCountingMetricReader(blockForMilliseconds: block);

        using var sdk = CreateSdkWithLogging(logProcessor);
        using var meterProvider = Sdk.CreateMeterProviderBuilder().AddReader(metricReader).Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations(options =>
            {
                options.LoggerProvider = sdk.LoggerProvider;
                options.MeterProvider = meterProvider;
            })
            .AddProcessor(traceProcessor)
            .Build();

        var stopwatch = Stopwatch.StartNew();
        AWSLambdaWrapper.Trace(tracerProvider, (string _, ILambdaContext _) => { }, "input", this.context);
        stopwatch.Stop();

        // All three ran.
        Assert.Equal(1, logProcessor.FlushCount);
        Assert.Equal(1, traceProcessor.FlushCount);
        Assert.Equal(1, metricReader.FlushCount);

        // Concurrent, so well under the 900ms a serial flush would need. The bound is generous
        // because thread-pool scheduling is not deterministic.
        Assert.InRange(stopwatch.ElapsedMilliseconds, block, block * 2.5);
    }

    // A provider is free to ignore the timeout it is handed, so waiting on the tasks is what
    // actually limits how long the invocation is held.
    [Fact]
    public void AProviderIgnoringItsTimeoutDoesNotHoldTheInvocation()
    {
        const int flushTimeout = 200;

        // Blocks far longer than the timeout it is given, and ignores it entirely.
        var metricReader = new FlushCountingMetricReader(blockForMilliseconds: 3000);

        using var meterProvider = Sdk.CreateMeterProviderBuilder().AddReader(metricReader).Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations(options =>
            {
                options.FlushTimeoutMilliseconds = flushTimeout;
                options.MeterProvider = meterProvider;
            })
            .Build();

        var stopwatch = Stopwatch.StartNew();
        AWSLambdaWrapper.Trace(tracerProvider, (string _, ILambdaContext _) => { }, "input", this.context);
        stopwatch.Stop();

        // Returned on the timeout rather than waiting out the provider's 3000ms.
        Assert.InRange(stopwatch.ElapsedMilliseconds, flushTimeout, 1500);
    }

    // Zero would abandon every flush rather than waiting briefly, so it must not be accepted.
    [Fact]
    public void ZeroFlushTimeoutIsRejected()
    {
        var options = new AWSLambdaInstrumentationOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.FlushTimeoutMilliseconds = 0);
        Assert.Equal(10000, options.FlushTimeoutMilliseconds);
    }

    // OpenTelemetrySdk.Create is the only public route to a standalone LoggerProvider.
    private static OpenTelemetrySdk CreateSdkWithLogging(BaseProcessor<LogRecord> processor) =>
        OpenTelemetrySdk.Create(builder => builder.WithLogging(logging => logging.AddProcessor(processor)));

    private sealed class FlushCountingProcessor<T> : BaseProcessor<T>
        where T : class
    {
        private readonly int blockForMilliseconds;

        public FlushCountingProcessor(int blockForMilliseconds = 0)
        {
            this.blockForMilliseconds = blockForMilliseconds;
        }

        public int FlushCount { get; private set; }

        public int LastTimeoutMilliseconds { get; private set; } = int.MinValue;

        protected override bool OnForceFlush(int timeoutMilliseconds)
        {
            this.FlushCount++;
            this.LastTimeoutMilliseconds = timeoutMilliseconds;

            if (this.blockForMilliseconds > 0)
            {
                Thread.Sleep(this.blockForMilliseconds);
            }

            return true;
        }
    }

    private sealed class FlushCountingMetricReader : MetricReader
    {
        private readonly int blockForMilliseconds;

        public FlushCountingMetricReader(int blockForMilliseconds = 0)
        {
            this.blockForMilliseconds = blockForMilliseconds;
        }

        public int FlushCount { get; private set; }

        protected override bool OnCollect(int timeoutMilliseconds)
        {
            this.FlushCount++;

            // Deliberately ignores the timeout, which a provider is free to do.
            if (this.blockForMilliseconds > 0)
            {
                Thread.Sleep(this.blockForMilliseconds);
            }

            return true;
        }
    }

    private sealed class ThrowingMetricReader : MetricReader
    {
        protected override bool OnCollect(int timeoutMilliseconds) =>
            throw new InvalidOperationException("flush failed");
    }
}
