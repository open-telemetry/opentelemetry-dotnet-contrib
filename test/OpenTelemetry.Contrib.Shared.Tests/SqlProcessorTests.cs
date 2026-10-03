// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Tests;

public class SqlProcessorTests(ITestOutputHelper output)
{
    /// <summary>
    /// A table name long enough that the query summary reaches its maximum length of 255
    /// characters once it has been captured.
    /// </summary>
    private static readonly string LongCapturedIdentifier = new('T', 260);

    private readonly ITestOutputHelper output = output;

    public static TheoryData<SqlProcessorTestCases.TestCase> TestData => SqlProcessorTestCases.GetSemanticConventionsTestCases();

    [Fact]
    public void GetSanitizedSql_CreateTableWithTrailingIdentifier_DoesNotThrow()
    {
        var sql = "CREATE TABLE XXX";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        Assert.Equal(sql, sqlStatementInfo.SanitizedSql);
        Assert.Equal(sql, sqlStatementInfo.DbQuerySummary);
    }

    [Fact]
    public void GetSanitizedSql_SingleLineCommentWithCarriageReturnLineFeed_PreservesLineBreak()
    {
        var sql = "SELECT * FROM table -- comment\r\nWHERE id = 42";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        Assert.Equal("SELECT * FROM table \r\nWHERE id = ?", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT table", sqlStatementInfo.DbQuerySummary);
    }

    [Fact]
    public void GetSanitizedSql_UnterminatedEscapedIdentifierInFromClause_SanitizesLiterals()
    {
        var sql = "SELECT * FROM [Orders WHERE CustomerName = 'secret-name' AND Id = 123 AND Token = 0xDEADBEEF";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        Assert.Equal("SELECT * FROM [Orders WHERE CustomerName = ? AND Id = ? AND Token = ?", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_RepeatedUnterminatedEscapedIdentifiersInFromClause_SanitizesLiterals()
    {
        var sql = $"SELECT * FROM {new string('[', 4096)} WHERE CustomerName = 'secret-name' AND Id = 123 AND Token = 0xDEADBEEF";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        Assert.Contains("?", sqlStatementInfo.SanitizedSql);
        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.DoesNotContain("123", sqlStatementInfo.SanitizedSql);
        Assert.DoesNotContain("DEADBEEF", sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT * FROM Users WHERE Name IN ('a)b', 'secret-name')")]
    [InlineData("SELECT * FROM Users WHERE Name IN ('a)b', 'secret-name', 'another)one')")]
    [InlineData("SELECT * FROM Users WHERE Name IN ('O''Brien)', 'secret-name')")]
    [InlineData("SELECT * FROM Users WHERE Name IN ('))', 'secret-name')")]
    [InlineData("SELECT * FROM Users WHERE Name IN (1 /* don't */, 'a)b', 'secret-name')")]
    [InlineData("SELECT * FROM Users WHERE Name IN (1 /* ) */, 'secret-name')")]
    [InlineData("SELECT * FROM Users WHERE Name IN (1, -- don't )\n'secret-name')")]
    public void GetSanitizedSql_InClauseLiteralOrCommentContainingCloseParen_SanitizesAllLiterals(string sql)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Name IN (?)", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT Users", sqlStatementInfo.DbQuerySummary);
    }

    [Fact]
    public void GetSanitizedSql_InClauseLiteralContainingCloseParen_DoesNotLeakPersonalData()
    {
        var sql = "SELECT Id FROM Users WHERE Email IN ('x)', 'user@example.com')";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        Assert.DoesNotContain("user@example.com", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT Id FROM Users WHERE Email IN (?)", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_UnterminatedInClauseLiteralContainingCloseParen_SanitizesAllLiterals()
    {
        // Without a closing parenthesis outside of the literals there is no clause to collapse,
        // so each value is sanitized individually instead.
        var sql = "SELECT * FROM Users WHERE Name IN ('a)b', 'secret-name'";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Name IN (?, ?", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_BackslashEscapedQuoteWithBackslashDialect_SanitizesLiteral()
    {
        var sql = "SELECT * FROM Users WHERE Password = 'a\\'secret-name'";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes: true);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Password = ?", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_BackslashEscapedQuoteInInClauseWithBackslashDialect_SanitizesLiterals()
    {
        var sql = "SELECT * FROM Users WHERE Name IN ('a\\'secret-name', 'b')";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes: true);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Name IN (?)", sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetSanitizedSql_DoubledQuoteEscape_SanitizesLiteralInEitherDialect(bool useBackslashEscapes)
    {
        var sql = "SELECT * FROM Users WHERE Password = 'a''secret-name'";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes);

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Password = ?", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_BackslashBeforeDoubledQuoteWithoutBackslashDialect_DoesNotLeak()
    {
        var sql = "SELECT * FROM Users WHERE Password = 'a\\''secret-name'";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes: false);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Password = ?", sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT * FROM t WHERE c = $$secret-name$$")]
    [InlineData("SELECT * FROM t WHERE c = $tag$se'cret-'name$tag$")] // Body may contain quotes/dollars.
    public void GetSanitizedSql_DollarQuotedString_SanitizesLiteral(string sql)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM t WHERE c = ?", sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT $IDENTITY FROM t", "SELECT $IDENTITY FROM t")] // SQL Server pseudo-column.
    [InlineData("SELECT a WHERE b = $1", "SELECT a WHERE b = $?")] // PostgreSQL positional parameter.
    public void GetSanitizedSql_LoneDollarSign_IsNotTreatedAsDollarQuote(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_DollarQuoteTagStartingWithDigit_IsNotTreatedAsDollarQuote()
    {
        var sql = "SELECT a WHERE b = $1$not-a-secret$1$";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.Contains("not-a-secret", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_UnterminatedDollarQuotedString_SanitizesLiteral()
    {
        var sql = "SELECT * FROM t WHERE c = $$secret-name";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM t WHERE c = ?", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_DoubleQuotedString_IsPreservedAsIdentifier()
    {
        var sql = "SELECT * FROM t WHERE c = \"identifier_or_mysql_string\"";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        Assert.Contains("identifier_or_mysql_string", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_DoubleQuotedStringWithBackslashDialect_SanitizesLiteral()
    {
        var sql = "SELECT * FROM Users WHERE Name = \"secret-value\" AND Id = 1";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes: true);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-value", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Name = ? AND Id = ?", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_DoubledDoubleQuoteEscapeWithBackslashDialect_SanitizesLiteral()
    {
        var sql = "SELECT * FROM Users WHERE Name = \"a\"\"secret-value\"";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes: true);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-value", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Name = ?", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_BackslashEscapedDoubleQuoteWithBackslashDialect_SanitizesLiteral()
    {
        var sql = "SELECT * FROM Users WHERE Name = \"a\\\"secret-value\"";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes: true);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-value", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Name = ?", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_UnterminatedDoubleQuotedStringWithBackslashDialect_SanitizesLiteral()
    {
        var sql = "SELECT * FROM Users WHERE Name = \"secret-value";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes: true);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-value", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Name = ?", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_UnterminatedInClauseStringLiteral_SanitizesLiteral()
    {
        var sql = "SELECT * FROM Users WHERE Name IN ('secret-name";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Name IN (?", sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("WHERE Password='secret-name'")]
    [InlineData("WHERE Password=N'secret-name'")]
    [InlineData("WHERE Password = 'secret-name'")]
    [InlineData("WHERE Email IN ('secret-name','other')")]
    [InlineData("WHERE Email LIKE'%secret-name%'")]
    [InlineData("INSERT INTO Credentials (User, Password) VALUES ('admin','secret-name')")]
    public void GetSanitizedSql_StringLiteralAfterSummaryLengthLimitReached_SanitizesLiteral(string clause)
    {
        var sql = $"SELECT * FROM {LongCapturedIdentifier} {clause}";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("WHERE SocialSecurityNumber=123456789", "123456789")]
    [InlineData("WHERE SocialSecurityNumber = 123456789", "123456789")]
    [InlineData("WHERE ApiToken=0xDEADBEEF", "DEADBEEF")]
    [InlineData("WHERE ApiToken = 0xDEADBEEF", "DEADBEEF")]
    public void GetSanitizedSql_NumericOrHexLiteralAfterSummaryLengthLimitReached_SanitizesLiteral(string clause, string literal)
    {
        var sql = $"SELECT * FROM {LongCapturedIdentifier} {clause}";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain(literal, sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_ManyJoinsExceedingSummaryLengthLimit_SanitizesLiteral()
    {
        var joins = string.Join(
            " ",
            Enumerable.Range(0, 12).Select(i =>
                $"INNER JOIN CustomerOrderDetails{i} AS d{i} ON d{i}.OrderId = o.OrderId"));

        var sql = $"SELECT o.OrderId, c.Email FROM Orders AS o {joins} WHERE c.Email='user@example.com'";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("user@example.com", sqlStatementInfo.SanitizedSql);
        Assert.True(sqlStatementInfo.DbQuerySummary.Length <= 255);
    }

    [Fact]
    public void GetSanitizedSql_ManyTablesExceedingSummaryLengthLimit_SanitizesLiteral()
    {
        var tables = string.Join(",", Enumerable.Range(0, 12).Select(i => $"CustomerOrderDetails{i}"));

        var sql = $"SELECT * FROM {tables} WHERE Email='user@example.com'";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("user@example.com", sqlStatementInfo.SanitizedSql);
        Assert.True(sqlStatementInfo.DbQuerySummary.Length <= 255);
    }

    [Fact]
    public void GetSanitizedSql_SummaryLengthLimitReached_DoesNotChangeSanitizedSql()
    {
        const string Clause = "WHERE Password='secret-name' AND Id=123 AND Token=0xDEADBEEF";

        var shortSummary = SqlProcessor.GetSanitizedSql($"SELECT * FROM Orders {Clause}");
        var fullSummary = SqlProcessor.GetSanitizedSql($"SELECT * FROM {LongCapturedIdentifier} {Clause}");

        var expected = "WHERE Password=? AND Id=? AND Token=?";

        Assert.EndsWith(expected, shortSummary.SanitizedSql, StringComparison.Ordinal);
        Assert.EndsWith(expected, fullSummary.SanitizedSql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    public void GetSanitizedSql_SameInstanceSanitizedRepeatedly_ReturnsSameResult(int extraColumns)
    {
        var columns = GetColumnList(extraColumns);
        var sql = $"SELECT {columns} FROM Orders WHERE CustomerName = 'secret-name' AND Id = 42";
        var copy = new string(sql.ToCharArray());

        var first = SqlProcessor.GetSanitizedSql(sql);
        var second = SqlProcessor.GetSanitizedSql(sql);
        var fromCopy = SqlProcessor.GetSanitizedSql(copy);

        Assert.Equal($"SELECT {columns} FROM Orders WHERE CustomerName = ? AND Id = ?", first.SanitizedSql);
        Assert.Equal("SELECT Orders", first.DbQuerySummary);
        Assert.Equal(first.SanitizedSql, second.SanitizedSql);
        Assert.Equal(first.DbQuerySummary, second.DbQuerySummary);
        Assert.Equal(first.SanitizedSql, fromCopy.SanitizedSql);
        Assert.Equal(first.DbQuerySummary, fromCopy.DbQuerySummary);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    public void GetSanitizedSql_SameInstanceWithDifferentDialects_RespectsDialect(int extraColumns)
    {
        var columns = GetColumnList(extraColumns);
        var sql = $"SELECT {columns} FROM Users WHERE Name = \"secret-value\" AND Id = 1";

        var defaultDialect = SqlProcessor.GetSanitizedSql(sql);
        var backslashDialect = SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes: true);
        var defaultDialectAgain = SqlProcessor.GetSanitizedSql(sql);
        var backslashDialectAgain = SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes: true);

        Assert.Equal($"SELECT {columns} FROM Users WHERE Name = \"secret-value\" AND Id = ?", defaultDialect.SanitizedSql);
        Assert.Equal($"SELECT {columns} FROM Users WHERE Name = ? AND Id = ?", backslashDialect.SanitizedSql);
        Assert.Equal(defaultDialect.SanitizedSql, defaultDialectAgain.SanitizedSql);
        Assert.Equal(backslashDialect.SanitizedSql, backslashDialectAgain.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_CacheFull_SameInstanceIsNotSanitizedAgain()
    {
        // Fill the statement cache so that statements not already in it are sanitized on every lookup.
        for (var i = 0; i < 1000; i++)
        {
            SqlProcessor.GetSanitizedSql($"SELECT * FROM {nameof(this.GetSanitizedSql_CacheFull_SameInstanceIsNotSanitizedAgain)}{i}");
        }

        var sql = $"SELECT * FROM Orders WHERE Id = 42 AND Token = '{Guid.NewGuid()}'";
        var copy = new string(sql.ToCharArray());

        var first = SqlProcessor.GetSanitizedSql(sql);
        var second = SqlProcessor.GetSanitizedSql(sql);
        var fromCopy = SqlProcessor.GetSanitizedSql(copy);

        Assert.Equal("SELECT * FROM Orders WHERE Id = ? AND Token = ?", first.SanitizedSql);
        Assert.Equal("SELECT Orders", first.DbQuerySummary);

        // The second lookup for the same instance on the same thread returns the previous result.
        Assert.Same(first.SanitizedSql, second.SanitizedSql);
        Assert.Same(first.DbQuerySummary, second.DbQuerySummary);

        // A different instance with equal content is sanitized again and produces an equal result.
        Assert.NotSame(first.SanitizedSql, fromCopy.SanitizedSql);
        Assert.Equal(first.SanitizedSql, fromCopy.SanitizedSql);
        Assert.Equal(first.DbQuerySummary, fromCopy.DbQuerySummary);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public void TestGetSanitizedSql(SqlProcessorTestCases.TestCase testCase)
    {
        Assert.SkipUnless(string.IsNullOrWhiteSpace(testCase.Skip), testCase.Skip ?? string.Empty);

        this.output.WriteLine($"Input: {testCase.Input.Query}");

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(testCase.Input.Query);

        var succeeded = false;
        foreach (var sanitizedQueryText in testCase.Expected.SanitizedQueryText)
        {
            if (sqlStatementInfo.SanitizedSql.Equals(sanitizedQueryText))
            {
                succeeded = true;
                break;
            }
        }

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");
        this.output.WriteLine($"Summary: {sqlStatementInfo.DbQuerySummary}");

        Assert.True(
            succeeded,
            $"Expected one of the sanitized query texts to match: {string.Join(", ", testCase.Expected.SanitizedQueryText)} but got: {sqlStatementInfo.SanitizedSql}");

        Assert.Equal(testCase.Expected.Summary, sqlStatementInfo.DbQuerySummary);
    }

    /// <summary>
    /// Gets a column list which makes a statement long enough for the most recently sanitized
    /// statement to be remembered when <paramref name="extraColumns"/> is not zero.
    /// </summary>
    private static string GetColumnList(int extraColumns) =>
        string.Join(", ", Enumerable.Range(0, extraColumns).Select(i => $"Column{i}").Prepend("Id"));
}
