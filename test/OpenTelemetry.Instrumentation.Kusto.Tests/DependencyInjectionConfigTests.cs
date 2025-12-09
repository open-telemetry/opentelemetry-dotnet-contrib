// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Instrumentation.Kusto.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

public class DependencyInjectionConfigTests : IDisposable
{
    public DependencyInjectionConfigTests()
    {
        KustoInstrumentation.TraceOptions = new KustoTraceInstrumentationOptions();
        KustoInstrumentation.MeterOptions = new KustoMeterInstrumentationOptions();
    }

    public void Dispose()
    {
        KustoInstrumentation.TraceOptions = new KustoTraceInstrumentationOptions();
        KustoInstrumentation.MeterOptions = new KustoMeterInstrumentationOptions();
    }

    [Fact]
    public void TestTracingOptionsDiConfig()
    {
        var enrichCalled = false;
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<KustoTraceInstrumentationOptions>(options =>
                {
                    options.Enrich = (activity, record) => { enrichCalled = true; };
                });
            })
            .AddKustoInstrumentation(configureKustoTraceInstrumentationOptions: null)
            .Build();

        // Assert that the options were picked up from DI and set on the static property
        Assert.NotNull(KustoInstrumentation.TraceOptions);
        Assert.NotNull(KustoInstrumentation.TraceOptions.Enrich);

        // Verify the Enrich callback works
        KustoInstrumentation.TraceOptions.Enrich(null!, default!);
        Assert.True(enrichCalled);
    }

    [Fact]
    public void TestMeterOptionsDiConfig()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<KustoMeterInstrumentationOptions>(options =>
                {
                    options.RecordQueryText = true;
                    options.RecordQuerySummary = false;
                });
            })
            .AddKustoInstrumentation(configureKustoMeterInstrumentationOptions: null)
            .Build();

        // Assert that the options were picked up from DI and set on the static property
        Assert.NotNull(KustoInstrumentation.MeterOptions);
        Assert.True(KustoInstrumentation.MeterOptions.RecordQueryText);
        Assert.False(KustoInstrumentation.MeterOptions.RecordQuerySummary);
    }

    [Fact]
    public void TestTraceAndMeterOptionsDiConfigTogether()
    {
        var enrichCalled = false;
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<KustoTraceInstrumentationOptions>(options =>
                {
                    options.Enrich = (activity, record) => { enrichCalled = true; };
                });
            })
            .AddKustoInstrumentation(configureKustoTraceInstrumentationOptions: null)
            .Build();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<KustoMeterInstrumentationOptions>(options =>
                {
                    options.RecordQueryText = true;
                    options.RecordQuerySummary = false;
                });
            })
            .AddKustoInstrumentation(configureKustoMeterInstrumentationOptions: null)
            .Build();

        // Assert that both options were picked up from DI and set on the static properties
        Assert.NotNull(KustoInstrumentation.TraceOptions);
        Assert.NotNull(KustoInstrumentation.TraceOptions.Enrich);

        Assert.NotNull(KustoInstrumentation.MeterOptions);
        Assert.True(KustoInstrumentation.MeterOptions.RecordQueryText);
        Assert.False(KustoInstrumentation.MeterOptions.RecordQuerySummary);

        // Verify the Enrich callback works
        KustoInstrumentation.TraceOptions.Enrich(null!, default!);
        Assert.True(enrichCalled);
    }

    [Fact]
    public void TestTraceOptionsWithCallbackOverridesDi()
    {
        var enrichFromDiCalled = false;
        var enrichFromCallbackCalled = false;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<KustoTraceInstrumentationOptions>(options =>
                {
                    // This should be overridden by the callback
                    options.Enrich = (activity, record) => { enrichFromDiCalled = true; };
                });
            })
            .AddKustoInstrumentation(configureKustoTraceInstrumentationOptions: options =>
            {
                // Callback should override DI configuration
                options.Enrich = (activity, record) => { enrichFromCallbackCalled = true; };
            })
            .Build();

        // Assert that the callback options were used, not the DI options
        Assert.NotNull(KustoInstrumentation.TraceOptions);
        Assert.NotNull(KustoInstrumentation.TraceOptions.Enrich);

        // Verify the callback version is used
        KustoInstrumentation.TraceOptions.Enrich(null!, default!);
        Assert.False(enrichFromDiCalled);
        Assert.True(enrichFromCallbackCalled);
    }

    [Fact]
    public void TestMeterOptionsWithCallbackOverridesDi()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<KustoMeterInstrumentationOptions>(options =>
                {
                    // This should be overridden by the callback
                    options.RecordQueryText = false;
                    options.RecordQuerySummary = true;
                });
            })
            .AddKustoInstrumentation(configureKustoMeterInstrumentationOptions: options =>
            {
                // Callback should override DI configuration
                options.RecordQueryText = true;
                options.RecordQuerySummary = false;
            })
            .Build();

        // Assert that the callback options were used, not the DI options
        Assert.NotNull(KustoInstrumentation.MeterOptions);
        Assert.True(KustoInstrumentation.MeterOptions.RecordQueryText);
        Assert.False(KustoInstrumentation.MeterOptions.RecordQuerySummary);
    }
}
