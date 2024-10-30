// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

public class SqlClientTestCase : IEnumerable<object[]>
{
    public string ConnectionString { get; set; } = string.Empty;

    public string ExpectedActivityName { get; set; } = string.Empty;

    public string? ExpectedServerAddress { get; set; }

    public int? ExpectedPort { get; set; }

    public string? ExpectedDbNamespace { get; set; }

    public string? ExpectedInstanceName { get; set; }

    private static SqlClientTestCase[] TestCases =>
    [
        new SqlClientTestCase
        {
            ConnectionString = @"Data Source=SomeServer",
            ExpectedActivityName = "SomeServer",
            ExpectedServerAddress = "SomeServer",
            ExpectedPort = null,
            ExpectedDbNamespace = null,
            ExpectedInstanceName = null,
        },
        new SqlClientTestCase
        {
            ConnectionString = @"Data Source=SomeServer,1434",
            ExpectedActivityName = "SomeServer:1434",
            ExpectedServerAddress = "SomeServer",
            ExpectedPort = 1434,
            ExpectedDbNamespace = null,
            ExpectedInstanceName = null,
        },
    ];

    public IEnumerator<object[]> GetEnumerator()
    {
        foreach (var testCase in TestCases)
        {
            yield return new object[] { testCase };
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
