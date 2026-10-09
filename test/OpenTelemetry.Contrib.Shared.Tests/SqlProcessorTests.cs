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
    [InlineData("SELECT * FROM Users WHERE Name IN ('a', $$b)c$$, $$secret-name$$, 'd')")]
    [InlineData("SELECT * FROM Users WHERE Name IN ('a', $tag$b)c$tag$, 'secret-name')")]
    [InlineData("SELECT * FROM Users WHERE Name IN ('a', E'b\\')c', 'secret-name')")]
    [InlineData("SELECT * FROM Users WHERE Name IN ('a', q'[it's)]', 'secret-name')")]
    [InlineData("SELECT * FROM Users WHERE Name IN ('a', /* /* ) */ ) */ 'secret-name')")]
    [InlineData("SELECT * FROM Users WHERE Name IN ('a', [b)c], 'secret-name')")]
    [InlineData("SELECT * FROM Users WHERE Name IN ('a', \"b)c\", 'secret-name')")]
    [InlineData("SELECT * FROM Users WHERE Name IN ('a', `b)c`, 'secret-name')")]
    public void GetSanitizedSql_InClauseLiteralOrCommentContainingCloseParen_SanitizesAllLiterals(string sql)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Name IN (?)", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT Users", sqlStatementInfo.DbQuerySummary);
    }

    [Theory]
    [InlineData("SELECT * FROM Users WHERE Name IN ('carol@example.com', \"Fabrikam (UK)\", \"alice@example.com\", \"x\", \"secret-name\")")]
    [InlineData("SELECT * FROM Users WHERE Name IN ('a', \"b\\\")c\", 'secret-name')")]
    [InlineData("SELECT * FROM Users WHERE Name IN (1, 2 # don't )\n, 'secret-name')")]
    public void GetSanitizedSql_InClauseLiteralOrCommentContainingCloseParenWithBackslashDialect_SanitizesAllLiterals(string sql)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes: true);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Name IN (?)", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT Users", sqlStatementInfo.DbQuerySummary);

        // Control: the same values quoted with single quotes are also sanitized.
        var control = SqlProcessor.GetSanitizedSql(sql.Replace('"', '\''), useBackslashEscapes: true);
        Assert.Equal("SELECT * FROM Users WHERE Name IN (?)", control.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT * FROM Users WHERE Id IN (1, [Manager's Approval] = 'secret-name' AND Name = [", "[Manager's Approval]")]
    [InlineData("SELECT * FROM Users WHERE Id IN (1, \"Manager's Approval\" = 'secret-name' AND Name = \"", "\"Manager's Approval\"")]
    [InlineData("SELECT * FROM Users WHERE Id IN (1, `Manager's Approval` = 'secret-name' AND Name = `", "`Manager's Approval`")]
    public void GetSanitizedSql_UnterminatedInClause_QuotedIdentifiersInItAreStillParsedAsIdentifiers(string sql, string identifier)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Contains($"{identifier} = ? AND Name = ", sqlStatementInfo.SanitizedSql);
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
    [InlineData(
        "SELECT [a].[Id], [a].[Nom de l'enterprise], [a].[Token] FROM [Accounts] AS [a] WHERE [a].[Token] = N'secret-name'",
        false,
        "SELECT [a].[Id], [a].[Nom de l'enterprise], [a].[Token] FROM [Accounts] AS [a] WHERE [a].[Token] = ?")]
    [InlineData(
        "SELECT \"a\".\"Id\", \"a\".\"Nom de l'enterprise\", \"a\".\"Token\" FROM \"Accounts\" AS \"a\" WHERE \"a\".\"Token\" = 'secret-name'",
        false,
        "SELECT \"a\".\"Id\", \"a\".\"Nom de l'enterprise\", \"a\".\"Token\" FROM \"Accounts\" AS \"a\" WHERE \"a\".\"Token\" = ?")]
    [InlineData(
        "SELECT `a`.`Id`, `a`.`Nom de l'enterprise`, `a`.`Token` FROM `Accounts` AS `a` WHERE `a`.`Token` = 'secret-name'",
        true,
        "SELECT `a`.`Id`, `a`.`Nom de l'enterprise`, `a`.`Token` FROM `Accounts` AS `a` WHERE `a`.`Token` = ?")]
    [InlineData(
        "SELECT [a].[Nom de l'enterprise] FROM [Accounts] AS [a] WHERE [a].[Token] = N'secret-name' AND [a].[Nom de l'enterprise] = N'Contoso'",
        false,
        "SELECT [a].[Nom de l'enterprise] FROM [Accounts] AS [a] WHERE [a].[Token] = ? AND [a].[Nom de l'enterprise] = ?")]
    public void GetSanitizedSql_QuoteInQuotedIdentifier_SanitizesLiterals(string sql, bool useBackslashEscapes, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        // The quoted identifier is copied verbatim, and its quote does not affect the literals.
        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);

        // Control: the same statement without a quote in the identifier.
        var control = SqlProcessor.GetSanitizedSql(WithoutApostrophe(sql), useBackslashEscapes);
        Assert.Equal(WithoutApostrophe(expected), control.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_QuoteInQuotedIdentifier_DoesNotChangeQuerySummary()
    {
        var sql = "SELECT [a].[Nom de l'enterprise] FROM [Accounts] AS [a] WHERE [a].[Note] = N'Transfer from Contoso4471 approved'";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");
        this.output.WriteLine($"Summary: {sqlStatementInfo.DbQuerySummary}");

        Assert.Equal("SELECT [a].[Nom de l'enterprise] FROM [Accounts] AS [a] WHERE [a].[Note] = ?", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT [Accounts]", sqlStatementInfo.DbQuerySummary);

        // Control: the same statement without a quote in the identifier has the same summary.
        var control = SqlProcessor.GetSanitizedSql(WithoutApostrophe(sql));
        Assert.Equal("SELECT [Accounts]", control.DbQuerySummary);
    }

    [Theory]
    [InlineData(
        "INSERT INTO audit_recipients (email) SELECT e FROM unnest(ARRAY['alice@example.com', 'bob@example.com', 'carol@example.com']) AS t(e)",
        "INSERT INTO audit_recipients (email) SELECT e FROM unnest(ARRAY[?, ?, ?]) AS t(e)",
        "INSERT audit_recipients SELECT unnest")]
    [InlineData(
        "SELECT * FROM unnest(ARRAY ['alice@example.com', 'bob@example.com'])",
        "SELECT * FROM unnest(ARRAY [?, ?])",
        "SELECT unnest")]
    [InlineData(
        "SELECT * FROM UNNEST(['alice@example.com', 'bob@example.com'])",
        "SELECT * FROM UNNEST([?, ?])",
        "SELECT UNNEST")]
    [InlineData(
        "SELECT * FROM UNNEST([12345, 67890])",
        "SELECT * FROM UNNEST([?, ?])",
        "SELECT UNNEST")]
    [InlineData(
        "SELECT * FROM UNNEST([Email, 'alice@example.com'])",
        "SELECT * FROM UNNEST([Email, ?])",
        "SELECT UNNEST")]
    [InlineData(
        "SELECT * FROM UNNEST(['alice]@example.com', 'bob@example.com'])",
        "SELECT * FROM UNNEST([?, ?])",
        "SELECT UNNEST")]
    public void GetSanitizedSql_ArrayInFromClause_SanitizesLiteralsInSqlAndSummary(string sql, string expectedSql, string expectedSummary)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");
        this.output.WriteLine($"Summary: {sqlStatementInfo.DbQuerySummary}");

        Assert.DoesNotContain("example.com", sqlStatementInfo.SanitizedSql);
        Assert.DoesNotContain("example.com", sqlStatementInfo.DbQuerySummary);
        Assert.Equal(expectedSql, sqlStatementInfo.SanitizedSql);
        Assert.Equal(expectedSummary, sqlStatementInfo.DbQuerySummary);
    }

    [Theory]
    [InlineData(
        "SELECT [Manager's Approval] FROM [Requests] WHERE [Tags] = ARRAY['secret-name'] AND [Owner] = 'secret-name'",
        "SELECT [Manager's Approval] FROM [Requests] WHERE [Tags] = ARRAY[?] AND [Owner] = ?")]
    [InlineData(
        "SELECT * FROM Requests WHERE [Owner] >[Manager's Approval] AND [Owner] = 'secret-name'",
        "SELECT * FROM Requests WHERE [Owner] >[Manager's Approval] AND [Owner] = ?")]
    [InlineData(
        "SELECT * FROM [1st Manager's Requests] WHERE [Owner] = 'secret-name'",
        "SELECT * FROM [1st Manager's Requests] WHERE [Owner] = ?")]
    public void GetSanitizedSql_BracketedIdentifierAndArray_SanitizesOnlyLiterals(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT * FROM t WHERE Tags = ARRAY[E'it\\'s', 'secret-name']", "SELECT * FROM t WHERE Tags = ARRAY[?, ?]")]
    [InlineData("SELECT * FROM t WHERE Tags = ARRAY [E'it\\'s', 'secret-name']", "SELECT * FROM t WHERE Tags = ARRAY [?, ?]")]
    [InlineData("SELECT Tags[E'secret-name\\''] FROM t", "SELECT Tags[?] FROM t")]
    [InlineData("SELECT (Tags)[E'secret-name\\''] FROM t", "SELECT (Tags)[?] FROM t")]
    [InlineData("SELECT Tags[1][E'secret-name\\''] FROM t", "SELECT Tags[?][?] FROM t")]
    [InlineData("SELECT \"Tags\"[E'secret-name\\''] FROM t", "SELECT \"Tags\"[?] FROM t")]
    [InlineData("SELECT * FROM[O'Brien] WHERE [Owner] = 'secret-name'", "SELECT * FROM[O'Brien] WHERE [Owner] = ?")]
    public void GetSanitizedSql_BracketAfterExpressionOrKeyword_IsParsedAccordingToItsContext(string sql, string expected)
    {
        // A bracket after an expression is an array subscript, and one after the ARRAY keyword is an
        // array constructor, so neither is copied as a bracketed identifier even if their content
        // could be one. A bracket directly after a keyword is still a bracketed identifier.
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT * FROM t WHERE c = \"12345\"", "SELECT * FROM t WHERE c = \"?\"")]
    [InlineData("SELECT * FROM t WHERE c = \"078-05-1120\" AND d = 'secret-name'", "SELECT * FROM t WHERE c = \"?\" AND d = ?")]
    [InlineData("SELECT * FROM t WHERE c = \" +44 20 7946 0958\"", "SELECT * FROM t WHERE c = \"?\"")]
    public void GetSanitizedSql_DoubleQuotedValueStartingWithNumber_IsSanitized(string sql, string expected)
    {
        // A double-quoted value is a string literal in some dialects (e.g. GoogleSQL), so one which
        // starts with a number is redacted rather than copied as a quoted identifier.
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT * FROM users WHERE nickname = E'it\\'s me' AND api_key = 'secret-name'", false, "SELECT * FROM users WHERE nickname = ? AND api_key = ?")]
    [InlineData("SELECT * FROM users WHERE nickname = e'it\\\\\\'s me' AND api_key = 'secret-name'", false, "SELECT * FROM users WHERE nickname = ? AND api_key = ?")]
    [InlineData("SELECT * FROM users WHERE nickname = q'[it's me]' AND api_key = 'secret-name'", false, "SELECT * FROM users WHERE nickname = ? AND api_key = ?")]
    [InlineData("SELECT * FROM users WHERE nickname = Q'{it's me}' AND api_key = 'secret-name'", false, "SELECT * FROM users WHERE nickname = ? AND api_key = ?")]
    [InlineData("SELECT * FROM users WHERE nickname = q'(it's me)' AND api_key = 'secret-name'", false, "SELECT * FROM users WHERE nickname = ? AND api_key = ?")]
    [InlineData("SELECT * FROM users WHERE nickname = q'<it's me>' AND api_key = 'secret-name'", false, "SELECT * FROM users WHERE nickname = ? AND api_key = ?")]
    [InlineData("SELECT * FROM users WHERE nickname = q'!it's me!' AND api_key = 'secret-name'", false, "SELECT * FROM users WHERE nickname = ? AND api_key = ?")]
    [InlineData("SELECT * FROM users WHERE nickname = nq'[it's me]' AND api_key = 'secret-name'", false, "SELECT * FROM users WHERE nickname = ? AND api_key = ?")]
    [InlineData("SELECT * FROM users WHERE nickname = q'[it's me' AND api_key = 'secret-name'", false, "SELECT * FROM users WHERE nickname = q?")]
    [InlineData("SELECT * FROM users /* disabled: /* old */ don't use */ WHERE api_key = 'secret-name'", false, "SELECT * FROM users  WHERE api_key = ?")]
    [InlineData("SELECT * FROM users /* disabled: /* old */ don't use WHERE api_key = 'secret-name'", false, "SELECT * FROM users ")]
    [InlineData("SELECT * FROM users # don't cache\nWHERE api_key = 'secret-name'", true, "SELECT * FROM users \nWHERE api_key = ?")]
    public void GetSanitizedSql_DialectSpecificLiteralOrComment_SanitizesFollowingLiteral(string sql, bool useBackslashEscapes, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT users", sqlStatementInfo.DbQuerySummary);
    }

    [Theory]
    [InlineData("SELECT * FROM #users WHERE api_key = 'secret-name'", false, "SELECT * FROM #users WHERE api_key = ?")]
    [InlineData("SELECT * FROM users /* a /* b */ WHERE api_key = 'secret-name'", true, "SELECT * FROM users  WHERE api_key = ?")]
    [InlineData("SELECT CASE WHEN x = 1 THEN 'a' ELSE'b\\' END FROM users WHERE api_key = 'secret-name'", false, "SELECT CASE WHEN x = ? THEN ? ELSE? END FROM users WHERE api_key = ?")]
    [InlineData("SELECT e, q FROM users WHERE e = 'a\\' AND q = 'secret-name'", false, "SELECT e, q FROM users WHERE e = ? AND q = ?")]
    public void GetSanitizedSql_DialectSpecificLiteralOrComment_IsOnlyRecognizedWhereValid(string sql, bool useBackslashEscapes, string expected)
    {
        // '#' only starts a comment for MySQL/MariaDB, whose block comments do not nest, and a
        // backslash only escapes a quote in an escape string when the E is a separate token.
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("CREATE LOGIN [CONTOSO\\alice.smith] FROM WINDOWS", "CREATE LOGIN [?] FROM WINDOWS", "CREATE LOGIN")]
    [InlineData("CREATE USER [Alice Smith] WITH PASSWORD = 'Pa55w0rd!'", "CREATE USER [?] WITH PASSWORD = ?", "CREATE USER")]
    [InlineData("CREATE USER [svc-payments] WITH PASSWORD = 'Pa55w0rd!'", "CREATE USER [?] WITH PASSWORD = ?", "CREATE USER")]
    [InlineData("CREATE USER [alice@contoso.com] FROM EXTERNAL PROVIDER", "CREATE USER [?] FROM EXTERNAL PROVIDER", "CREATE USER")]
    [InlineData("CREATE USER [alice]]smith] WITHOUT LOGIN", "CREATE USER [?] WITHOUT LOGIN", "CREATE USER")]
    [InlineData("DROP USER IF EXISTS [Alice Smith]", "DROP USER IF EXISTS [?]", "DROP USER")]
    [InlineData("ALTER LOGIN \"Alice Smith\" DISABLE", "ALTER LOGIN \"?\" DISABLE", "ALTER LOGIN")]
    [InlineData("CREATE LOGIN alice WITH PASSWORD = 'Pa55w0rd!'", "CREATE LOGIN ? WITH PASSWORD = ?", "CREATE LOGIN")]
    [InlineData("CREATE USER \"\" X", "CREATE USER \"\" X", "CREATE USER")]
    public void GetSanitizedSql_LoginOrUserName_IsSanitized(string sql, string expectedSql, string expectedSummary)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.Equal(expectedSql, sqlStatementInfo.SanitizedSql);
        Assert.Equal(expectedSummary, sqlStatementInfo.DbQuerySummary);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(64)]
    [InlineData(128)]
    public void GetSanitizedSql_LoginOrUserNameWithEscapedBracketsAtMaximumLength_IsSanitized(int escapedBrackets)
    {
        // Each escaped bracket (]]) is one character of the 128 character maximum length of an identifier.
        var name = new string('a', 128 - escapedBrackets) + string.Concat(Enumerable.Repeat("]]", escapedBrackets));
        var sql = $"CREATE USER [{name}] WITHOUT LOGIN";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.Equal("CREATE USER [?] WITHOUT LOGIN", sqlStatementInfo.SanitizedSql);
        Assert.Equal("CREATE USER", sqlStatementInfo.DbQuerySummary);
    }

    [Theory]
    [InlineData("SELECT a Xrom b", "SELECT")]
    [InlineData("SELECT Prom FROM Events", "SELECT Events")]
    [InlineData("SELECT * FROM Orders Xoin Customers", "SELECT Orders")]
    [InlineData("Xelect * FROM Orders", "")]
    public void GetSanitizedSql_WordWhichOnlyDiffersFromKeywordByFirstCharacter_IsNotKeyword(string sql, string expectedSummary)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Summary: {sqlStatementInfo.DbQuerySummary}");

        Assert.Equal(sql, sqlStatementInfo.SanitizedSql);
        Assert.Equal(expectedSummary, sqlStatementInfo.DbQuerySummary);
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

    private static string WithoutApostrophe(string sql)
#if NET
        => sql.Replace("Nom de l'enterprise", "NomEnterprise", StringComparison.Ordinal);
#else
        => sql.Replace("Nom de l'enterprise", "NomEnterprise");
#endif

    /// <summary>
    /// Gets a column list which makes a statement long enough for the most recently sanitized
    /// statement to be remembered when <paramref name="extraColumns"/> is not zero.
    /// </summary>
    private static string GetColumnList(int extraColumns) =>
        string.Join(", ", Enumerable.Range(0, extraColumns).Select(i => $"Column{i}").Prepend("Id"));
}
