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

        var succeeded = false;
        foreach (var sanitizedQueryText in test.Expected.SanitizedQueryText)
        {
            if (sqlStatementInfo.SanitizedSql.Equals(sanitizedQueryText))
            {
                succeeded = true;
                break;
            }
        }

        Assert.True(
            succeeded,
            $"Expected one of the sanitized query texts to match: {string.Join(", ", test.Expected.SanitizedQueryText)} but got: {sqlStatementInfo.SanitizedSql}");

        Assert.Equal(test.Expected.Summary, sqlStatementInfo.DbQuerySummary);
    }
}
