// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace OpenTelemetry.Instrumentation.Benchmarks;

/// <summary>
/// Measures sanitizing two different statements, each either once or twice in a row, as happens when
/// more than one instrumentation (for example EF Core and SqlClient) sanitizes the same command text.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
public class SqlProcessorRepeatedSanitizationBenchmarks
{
    private string first = string.Empty;
    private string second = string.Empty;

    /// <summary>
    /// Gets or sets the approximate length of each SQL statement.
    /// </summary>
    [Params(100, 1000, 4000)]
    public int Length { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the statement cache is already full of other statements,
    /// in which case the benchmarked statements are sanitized on every call.
    /// </summary>
    [Params(false, true)]
    public bool CacheFull { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (this.CacheFull)
        {
            for (var i = 0; i < 1000; i++)
            {
                SqlProcessor.GetSanitizedSql("SELECT * FROM Table" + i.ToString(CultureInfo.InvariantCulture));
            }
        }

        this.first = CreateStatement(this.Length, "Orders");
        this.second = CreateStatement(this.Length, "Customers");

        SqlProcessor.GetSanitizedSql(this.first);
        SqlProcessor.GetSanitizedSql(this.second);
    }

    [Benchmark(Baseline = true)]
    public string EachStatementOnce()
    {
        _ = SqlProcessor.GetSanitizedSql(this.first);
        return SqlProcessor.GetSanitizedSql(this.second).DbQuerySummary;
    }

    [Benchmark]
    public string EachStatementTwice()
    {
        _ = SqlProcessor.GetSanitizedSql(this.first);
        _ = SqlProcessor.GetSanitizedSql(this.first);
        _ = SqlProcessor.GetSanitizedSql(this.second);
        return SqlProcessor.GetSanitizedSql(this.second).DbQuerySummary;
    }

    private static string CreateStatement(int length, string table)
    {
        var builder = new StringBuilder("SELECT ");
        var column = 0;

        while (builder.Length < length - 50)
        {
            builder.Append("[t].[Column").Append(column++).Append("], ");
        }

        return builder.Append("[t].[Id] FROM [").Append(table).Append("] AS [t] WHERE [t].[Id] = 42").ToString();
    }
}
