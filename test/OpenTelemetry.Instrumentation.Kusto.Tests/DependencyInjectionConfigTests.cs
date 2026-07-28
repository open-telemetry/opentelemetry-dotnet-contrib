// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Instrumentation.Kusto.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

public class DependencyInjectionConfigTests : IDisposable
{
    public DependencyInjectionConfigTests()
    {
        KustoInstrumentation.Listener.TraceOptions = new KustoTraceInstrumentationOptions();
        KustoInstrumentation.Listener.MeterOptions = new KustoMeterInstrumentationOptions();
    }

    public void Dispose()
    {
        KustoInstrumentation.Listener.TraceOptions = new KustoTraceInstrumentationOptions();
        KustoInstrumentation.Listener.MeterOptions = new KustoMeterInstrumentationOptions();
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
                    options.Enrich = (_, _) => { enrichCalled = true; };
                });
            })
            .AddKustoInstrumentation(configure: null)
            .Build();

        // Assert that the options were picked up from DI and set on the listener instance
        Assert.NotNull(KustoInstrumentation.Listener.TraceOptions);
        Assert.NotNull(KustoInstrumentation.Listener.TraceOptions.Enrich);

        // Verify the Enrich callback works
        KustoInstrumentation.Listener.TraceOptions.Enrich(null!, default!);
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
            .AddKustoInstrumentation(configure: null)
            .Build();

        // Assert that the options were picked up from DI and set on the listener instance
        Assert.NotNull(KustoInstrumentation.Listener.MeterOptions);
        Assert.True(KustoInstrumentation.Listener.MeterOptions.RecordQueryText);
        Assert.False(KustoInstrumentation.Listener.MeterOptions.RecordQuerySummary);
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
                    options.Enrich = (_, _) => { enrichCalled = true; };
                });
            })
            .AddKustoInstrumentation(configure: null)
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
            .AddKustoInstrumentation(configure: null)
            .Build();

        // Assert that both options were picked up from DI and set on the listener instance
        Assert.NotNull(KustoInstrumentation.Listener.TraceOptions);
        Assert.NotNull(KustoInstrumentation.Listener.TraceOptions.Enrich);

        Assert.NotNull(KustoInstrumentation.Listener.MeterOptions);
        Assert.True(KustoInstrumentation.Listener.MeterOptions.RecordQueryText);
        Assert.False(KustoInstrumentation.Listener.MeterOptions.RecordQuerySummary);

        // Verify the Enrich callback works
        KustoInstrumentation.Listener.TraceOptions.Enrich(null!, default!);
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
                    options.Enrich = (_, _) => { enrichFromDiCalled = true; };
                });
            })
            .AddKustoInstrumentation(configure: options =>
            {
                // Callback should override DI configuration
                options.Enrich = (_, _) => { enrichFromCallbackCalled = true; };
            })
            .Build();

        // Assert that the callback options were used, not the DI options
        Assert.NotNull(KustoInstrumentation.Listener.TraceOptions);
        Assert.NotNull(KustoInstrumentation.Listener.TraceOptions.Enrich);

        // Verify the callback version is used
        KustoInstrumentation.Listener.TraceOptions.Enrich(null!, default!);
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
            .AddKustoInstrumentation(configure: options =>
            {
                // Callback should override DI configuration
                options.RecordQueryText = true;
                options.RecordQuerySummary = false;
            })
            .Build();

        // Assert that the callback options were used, not the DI options
        Assert.NotNull(KustoInstrumentation.Listener.MeterOptions);
        Assert.True(KustoInstrumentation.Listener.MeterOptions.RecordQueryText);
        Assert.False(KustoInstrumentation.Listener.MeterOptions.RecordQuerySummary);
    }
}
