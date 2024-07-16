// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

#if NET6_0_OR_GREATER
[Collection("SqlClient")]
public class SqlClientMetricsTests
{
    private const string TestConnectionString = "Data Source=(localdb)\\MSSQLLocalDB;Database=master";

    [Fact]
    public void SqlClient_BadArgs()
    {
        MeterProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddSqlClientInstrumentation());
    }
}
#endif
