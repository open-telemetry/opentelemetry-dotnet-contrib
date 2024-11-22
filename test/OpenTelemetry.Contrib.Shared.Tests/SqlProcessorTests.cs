// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Instrumentation.Tests;

public class SqlProcessorTests
{
    public static IEnumerable<object[]> TestData => SqlProcessorTestCases.GetTestCases();

    [Theory]
    [MemberData(nameof(TestData))]
    public void TestGetSanitizedSql(SqlProcessorTestCases.TestCase test)
    {
        var sanitized = SqlProcessor.GetSanitizedSql(test.Sql);
        Assert.Equal(test.Sanitized, sanitized);
    }
}
