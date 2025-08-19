// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Instrumentation.Tests;

public class SqlProcessorTests
{
    public static IEnumerable<object[]> TestData => SqlProcessorTestCases.GetSemanticConventionsTestCases();

    [Theory]
    [MemberData(nameof(TestData))]
    public void TestGetSanitizedSql(SqlProcessorTestCases.TestCase test)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(test.Input.Query);

        foreach (var sanitizedQueryText in test.Expected.SanitizedQueryText)
        {
            Assert.Equal(sanitizedQueryText, sqlStatementInfo.SanitizedSql);
        }

        Assert.Equal(test.Expected.Summary, sqlStatementInfo.DbQuerySummary);
    }
}
