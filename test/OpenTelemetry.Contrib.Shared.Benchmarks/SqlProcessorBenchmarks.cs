// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;

namespace OpenTelemetry.Instrumentation.Benchmarks;

[MemoryDiagnoser]
public class SqlProcessorBenchmarks
{
    [Params("SELECT * FROM Orders o, OrderDetails od", "SELECT order_date\nFROM   (SELECT *\nFROM   orders o\nJOIN customers c\nON o.customer_id = c.customer_id)")]
    public string Sql { get; set; } = string.Empty;

    [Params(1, 20)]
    public int Iterations { get; set; }

    [Benchmark]
    public void Simple()
    {
        for (int i = 0; i < this.Iterations; i++)
        {
            SqlProcessor.GetSanitizedSql(this.Sql);
        }
    }
}
