// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using BenchmarkDotNet.Attributes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Benchmarks;

/// <summary>
/// Benchmarks for the EFCore instrumentation that can be used to guard against
/// regressions and to guide future optimisations.
/// </summary>
/// <remarks>
/// The benchmarks use an SQLite in-memory database to exercise the full
/// diagnostic listener lifecycle (CommandCreated → CommandExecuting →
/// CommandExecuted) without needing an external database server.
/// </remarks>
[MemoryDiagnoser]
public class EntityFrameworkCoreBenchmarks
{
    private SqliteConnection? connection;
    private DbContextOptions<BenchmarkContext>? contextOptions;
    private TracerProvider? tracerProvider;

    /// <summary>
    /// Controls how the EFCore instrumentation is configured for each benchmark run.
    /// </summary>
    public enum InstrumentationScenario
    {
        /// <summary>
        /// No TracerProvider is configured: the diagnostic events fired by EF Core
        /// are ignored. This is the baseline cost.
        /// </summary>
        None,

        /// <summary>
        /// Tracing is enabled with default options. This covers the core
        /// CommandCreated / CommandExecuting / CommandExecuted path.
        /// </summary>
        Traces,

        /// <summary>
        /// Tracing is enabled with an <see cref="EntityFrameworkInstrumentationOptions.EnrichWithIDbCommand"/>
        /// callback, measuring the additional overhead of user-defined enrichment.
        /// </summary>
        TracesWithEnrichment,

        /// <summary>
        /// Tracing is enabled with a <see cref="EntityFrameworkInstrumentationOptions.Filter"/>
        /// callback that accepts all commands, measuring the overhead of filter evaluation.
        /// </summary>
        TracesWithFilter,
    }

    [Params(
        InstrumentationScenario.None,
        InstrumentationScenario.Traces,
        InstrumentationScenario.TracesWithEnrichment,
        InstrumentationScenario.TracesWithFilter)]
    public InstrumentationScenario Scenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.connection = new SqliteConnection("Filename=:memory:");
        this.connection.Open();

        this.contextOptions = new DbContextOptionsBuilder<BenchmarkContext>()
            .UseSqlite(this.connection)
            .Options;

        using var context = new BenchmarkContext(this.contextOptions);
        context.Database.EnsureCreated();

        context.Items.AddRange(
            new() { Name = "Alpha" },
            new() { Name = "Beta" },
            new() { Name = "Gamma" });

        context.SaveChanges();

        this.tracerProvider = this.Scenario switch
        {
            InstrumentationScenario.Traces => Sdk.CreateTracerProviderBuilder()
                .AddEntityFrameworkCoreInstrumentation()
                .Build(),

            InstrumentationScenario.TracesWithEnrichment => Sdk.CreateTracerProviderBuilder()
                .AddEntityFrameworkCoreInstrumentation(
                    (options) => options.EnrichWithIDbCommand = static (activity, _) => activity.SetTag("benchmark.enriched", true))
                .Build(),

            InstrumentationScenario.TracesWithFilter => Sdk.CreateTracerProviderBuilder()
                .AddEntityFrameworkCoreInstrumentation(
                    (options) => options.Filter = static (_, command) => command.CommandType == CommandType.Text)
                .Build(),

            _ => null,
        };
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.tracerProvider?.Dispose();
        this.connection?.Dispose();
    }

    [Benchmark]
    public int Query()
    {
        using var context = new BenchmarkContext(this.contextOptions!);
        return context.Items.OrderBy((p) => p.Name).ToList().Count;
    }
}
