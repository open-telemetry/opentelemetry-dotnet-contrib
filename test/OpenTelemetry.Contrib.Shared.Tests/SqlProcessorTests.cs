// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;

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
    [InlineData("UPDATE [Orders''/*] SET c = 'secret-name'", "UPDATE [Orders''/*] SET c = ?")]
    [InlineData("UPDATE [Orders''--] SET c = 'secret-name'", "UPDATE [Orders''--] SET c = ?")]
    [InlineData("UPDATE [Orders/*''] SET c = 'secret-name'", "UPDATE [Orders/*''] SET c = ?")]
    [InlineData("SELECT * FROM t WHERE [Orders''/*] = 'secret-name'", "SELECT * FROM t WHERE [Orders''/*] = ?")]
    [InlineData("SELECT * FROM [Orders''--] WHERE c = 'secret-name'", "SELECT * FROM [Orders''--] WHERE c = ?")]
    [InlineData("SELECT * FROM t WHERE [Orders''''--] = 'secret-name'", "SELECT * FROM t WHERE [Orders''''--] = ?")]
    [InlineData("SELECT * FROM t WHERE [Orders/*''''] = 'secret-name'", "SELECT * FROM t WHERE [Orders/*''''] = ?")]
    [InlineData("SELECT fn([a--'']), 'secret-name'", "SELECT fn([a--'']), ?")]
    [InlineData("SELECT * FROM t WHERE fn([a].[b--'']) = $$\nsecret-name$$", "SELECT * FROM t WHERE fn([a].[b--'']) = ?")]
    [InlineData("SELECT * FROM t WHERE [b''--] = 'x\nsecret-name'", "SELECT * FROM t WHERE [b''--] = ?")]
    [InlineData("SELECT * FROM t WHERE [name)''/*] = 'secret-name'", "SELECT * FROM t WHERE [name)''/*] = ?")]
    [InlineData("SELECT * FROM t WHERE [name:''--] = 'x\nsecret-name'", "SELECT * FROM t WHERE [name:''--] = ?")]
    public void GetSanitizedSql_BracketedIdentifierContainingQuotesAndCommentMarker_IsNotParsedAsComment(string sql, string expected)
    {
        // A comment marker in a bracketed identifier is part of the name, and the quotes in it must not make it be
        // parsed as SQL, as then the comment would swallow the end of the name and the rest of the statement,
        // which left a literal on a later line (or a later literal which the swallowed text started) unsanitized.
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT * FROM UNNEST([Email, 'secret-name' /* note */, 'secret-name'])", "SELECT * FROM UNNEST([Email, ? , ?])")]
    [InlineData("SELECT * FROM UNNEST(['secret-name' -- note\n, 'secret-name'])", "SELECT * FROM UNNEST([? \n, ?])")]
    [InlineData("SELECT * FROM UNNEST(['a--b', 'secret-name'])", "SELECT * FROM UNNEST([?, ?])")]
    [InlineData("SELECT * FROM UNNEST(['a/*b', 'secret-name'])", "SELECT * FROM UNNEST([?, ?])")]
    public void GetSanitizedSql_ListOfValuesContainingTerminatedCommentOrCommentMarkerInLiteral_IsParsedAsSql(string sql, string expected)
    {
        // A list of values is still parsed as SQL if a comment in it ends within the list, or if the comment
        // marker is text in one of its literals, as neither of those would contain the closing bracket.
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT * FROM UNNEST(['secret-name' /* ] note */, 'other'])", "SELECT * FROM UNNEST([? , ?])")]
    [InlineData("SELECT * FROM UNNEST(['secret-name' -- ] note\n, 'other'])", "SELECT * FROM UNNEST([? \n, ?])")]
    [InlineData("SELECT * FROM UNNEST([Email, 'secret-name' /* ] note */, 'other'])", "SELECT * FROM UNNEST([Email, ? , ?])")]
    [InlineData("SELECT * FROM UNNEST([N'secret-name' /* ] note */, 'other'])", "SELECT * FROM UNNEST([? , ?])")]
    [InlineData("SELECT * FROM UNNEST(['a', 'secret-name' /* ] note */])", "SELECT * FROM UNNEST([?, ? ])")]
    [InlineData("SELECT * FROM t WHERE c = ['secret-name' /* ] note */, 'other']", "SELECT * FROM t WHERE c = [? , ?]")]
    [InlineData("SELECT * FROM UNNEST([1234 /* ] note */, 5678])", "SELECT * FROM UNNEST([? , ?])")]
    [InlineData("SELECT * FROM UNNEST([1234 -- ] note\n, 5678])", "SELECT * FROM UNNEST([? \n, ?])")]
    [InlineData("SELECT * FROM UNNEST([Email, $$secret-name$$ /* '' ] note */, 'other'])", "SELECT * FROM UNNEST([Email, ? , ?])")]
    [InlineData("SELECT * FROM UNNEST([Email, $$secret-name$$ /* ] note */, $$other$$])", "SELECT * FROM UNNEST([Email, ? , ?])")]
    [InlineData("SELECT * FROM UNNEST([Email, $$secret-name$$])", "SELECT * FROM UNNEST([Email, ?])")]
    [InlineData("SELECT * FROM UNNEST([Email -- note\r, 'secret-name' /* ] note */, 'other'])", "SELECT * FROM UNNEST([Email \r, ? , ?])")]
    [InlineData("SELECT * FROM UNNEST([Email -- note\r, 'secret-name' -- ] note\r, 'other'])", "SELECT * FROM UNNEST([Email \r, ? \r, ?])")]
    public void GetSanitizedSql_ListOfValuesWithCommentContainingClosingBracket_SanitizesEveryValue(string sql, string expected)
    {
        // The first closing bracket of a list is in a comment, which ends later in the list, so the content up to that
        // bracket has a comment which is not ended. That is not an identifier with a comment marker in its name, as
        // the content has values, which must not be copied as an identifier would be.
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.DoesNotContain("1234", sqlStatementInfo.SanitizedSql);
        Assert.DoesNotContain("other", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT * FROM [N'Souza] WHERE c = 'secret-name'", "SELECT * FROM [N'Souza] WHERE c = ?")]
    [InlineData("SELECT * FROM [2024's Reports] WHERE c = 'secret-name'", "SELECT * FROM [2024's Reports] WHERE c = ?")]
    [InlineData("SELECT * FROM [2024, O'Brien] WHERE c = 'secret-name'", "SELECT * FROM [2024, O'Brien] WHERE c = ?")]
    [InlineData("SELECT * FROM [--Orders] WHERE c = 'x\nsecret-name'", "SELECT * FROM [--Orders] WHERE c = ?")]
    [InlineData("SELECT * FROM [/*Orders] WHERE c = 'secret-name'", "SELECT * FROM [/*Orders] WHERE c = ?")]
    [InlineData("SELECT * FROM [/*Orders] WHERE c = 'secret-name' /* x */", "SELECT * FROM [/*Orders] WHERE c = ? ")]
    [InlineData("SELECT * FROM [E'Souza] WHERE c = 'secret-name'", "SELECT * FROM [E'Souza] WHERE c = ?")]
    public void GetSanitizedSql_BracketedIdentifierWhichStartsLikeValueOrComment_IsCopiedAndLaterLiteralsAreSanitized(string sql, string expected)
    {
        // A name can start with a comment marker, a number or a string prefix, which a list of values can also do, but
        // a list ends with a closing bracket which is not in a string or comment, which these do not have. Treating
        // them as lists would make the quote in the name start a string, which would take in the next literal's quote.
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT * FROM [N'Souza] WHERE c = 'secret-name]suffix'", "SELECT * FROM [N'Souza] WHERE c = ?")]
    [InlineData("SELECT * FROM [--Orders] WHERE c = 'a]\nsecret-name'", "SELECT * FROM [--Orders] WHERE c = ?")]
    [InlineData("SELECT * FROM [/*Orders] WHERE c = 'a]\nsecret-name'", "SELECT * FROM [/*Orders] WHERE c = ?")]
    [InlineData("SELECT * FROM [Price $$x$$ O'Brien] WHERE c = 'secret-name'", "SELECT * FROM [Price $$x$$ O'Brien] WHERE c = ?")]
    [InlineData("SELECT * FROM [dbo].[N'Souza] WHERE c = 'secret-name]suffix'", "SELECT * FROM [dbo].[N'Souza] WHERE c = ?")]
    [InlineData("SELECT * FROM t, [N'Souza] WHERE c = 'secret-name]suffix'", "SELECT * FROM t, [N'Souza] WHERE c = ?")]
    [InlineData("SELECT * FROM t JOIN [2024's Reports] ON 1 = 1 WHERE c = 'secret-name]suffix'", "SELECT * FROM t JOIN [2024's Reports] ON ? = ? WHERE c = ?")]
    [InlineData("DELETE FROM [N'Souza] WHERE c = 'secret-name]suffix'", "DELETE FROM [N'Souza] WHERE c = ?")]
    [InlineData("UPDATE [N'Souza] SET c = 'secret-name]suffix'", "UPDATE [N'Souza] SET c = ?")]
    [InlineData("INSERT INTO [N'Souza] (c) VALUES ('secret-name]suffix')", "INSERT INTO [N'Souza] (c) VALUES (?)")]
    public void GetSanitizedSql_BracketedNameWhereANameIsExpected_IsCopiedWithoutUsingTheSqlAfterIt(string sql, string expected)
    {
        // A bracket after FROM, UPDATE, INTO and the like is a name, never a list of values, so it ends at its first
        // closing bracket. Looking at the SQL after it to guess would mistake a bracket in a later literal for its end.
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT * FROM t WHERE [Price $$x$$ O'Brien] = 'secret-name'", "SELECT * FROM t WHERE [Price $$x$$ O'Brien] = ?")]
    [InlineData("SELECT [Price $$x$$ O'Brien] FROM t WHERE c = 'secret-name'", "SELECT [Price $$x$$ O'Brien] FROM t WHERE c = ?")]
    [InlineData("SELECT * FROM t WHERE [Orders'$$] = 'secret-name'", "SELECT * FROM t WHERE [Orders'$$] = ?")]
    [InlineData("SELECT * FROM t WHERE [Orders--$$] = 'x\nsecret-name'", "SELECT * FROM t WHERE [Orders--$$] = ?")]
    [InlineData("SELECT * FROM t WHERE [Price$USD$] = 'secret-name'", "SELECT * FROM t WHERE [Price$USD$] = ?")]
    [InlineData("SELECT * FROM UNNEST([Email, -- note\n $$secret-name$$ /* ] note */, $$other$$])", "SELECT * FROM UNNEST([Email, \n ? , ?])")]
    [InlineData("SELECT * FROM UNNEST([Email, /* note */ $$secret-name$$ /* ] note */, $$other$$])", "SELECT * FROM UNNEST([Email,  ? , ?])")]
    public void GetSanitizedSql_DollarSignsInBracketedNameWhichIsNotAfterKeyword_AreNotParsedAsString(string sql, string expected)
    {
        // A dollar-quoted string is a value only when it starts a list or follows a comma, as elsewhere
        // the dollar signs are part of a name, which must not make its quotes or comment markers be parsed.
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.DoesNotContain("other", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT * FROM t WHERE c = [N'Souza] AND d = '", "secret-name'", "SELECT * FROM t WHERE c = [?")]
    [InlineData("SELECT * FROM t WHERE c = [E'head]", "secret-name']", "SELECT * FROM t WHERE c = [?")]
    [InlineData("SELECT * FROM t WHERE c = [1, 'head]", "secret-name', 'other']", "SELECT * FROM t WHERE c = [?")]
    public void GetSanitizedSql_BracketWhichCouldStartListAndLimitOnLookaheadIsReached_DoesNotExposeAnythingWhichFollows(string prefix, string suffix, string expected)
    {
        // The bracket could be a name or a list of values, and where the closing bracket of a list would be is further
        // away than is read, so it is not known where it ends, as that could be in a string. Redacting only the bracket
        // would parse what follows from inside that string, so the rest of the statement is redacted instead.
        var sql = prefix + new string('x', 17_000) + suffix;

        var stopwatch = Stopwatch.StartNew();
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);
        stopwatch.Stop();

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.DoesNotContain("other", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Sanitizing {sql.Length:N0} characters took {stopwatch.Elapsed}.");
    }

    [Theory]
    [InlineData("SELECT * FROM t WHERE c = -- from\n ['secret-name']")]
    [InlineData("SELECT * FROM t WHERE c = -- from\r\n ['secret-name']")]
    [InlineData("SELECT * FROM t WHERE c = -- from\r ['secret-name']")]
    [InlineData("SELECT * FROM t WHERE c = /* from */ ['secret-name']")]
    [InlineData("SELECT * FROM t WHERE c IN -- update\n ['secret-name']")]
    [InlineData("SELECT * FROM t WHERE c = 'from' ['secret-name']")]
    [InlineData("SELECT * FROM t WHERE c = -- from\n [Email, 'secret-name']")]
    public void GetSanitizedSql_ListOfValuesAfterKeywordInCommentOrLiteral_IsNotParsedAsName(string sql)
    {
        // A word before the bracket in a comment or string is not a keyword, so the bracket is not where a name is expected.
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT * FROM t WHERE c = [Email, fn($$secret-name$$) /* '' ] note */, 'other']")]
    [InlineData("SELECT * FROM t WHERE c = [Email, fn(a, $$secret-name$$) /* ] note */, 'other']")]
    [InlineData("SELECT * FROM t WHERE c = [Email, 1 + $$secret-name$$ /* '' ] note */, 'other']")]
    [InlineData("SELECT * FROM t WHERE c = [Email, ($$secret-name$$) /* ] note */, 'other']")]
    public void GetSanitizedSql_DollarQuotedValueInItemExpression_IsSanitized(string sql)
    {
        // A string in dollar quotes can follow an opening parenthesis or an operator in an item of a list, as well as a comma.
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.DoesNotContain("other", sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT * FROM t WHERE c = [$$secret-name$$]", "SELECT * FROM t WHERE c = [?]")]
    [InlineData("SELECT * FROM t WHERE c = [$$secret-name$$, $$other$$]", "SELECT * FROM t WHERE c = [?, ?]")]
    [InlineData("SELECT * FROM t WHERE c = [$tag$secret-name$tag$]", "SELECT * FROM t WHERE c = [?]")]
    [InlineData("SELECT * FROM t WHERE c = [E'secret-name]x', 'other']", "SELECT * FROM t WHERE c = [?, ?]")]
    [InlineData("SELECT * FROM t WHERE c = [N'secret-name]x', 'other']", "SELECT * FROM t WHERE c = [?, ?]")]
    [InlineData("SELECT * FROM t WHERE c = [1234, 'secret-name]x', 'other']", "SELECT * FROM t WHERE c = [?, ?, ?]")]
    [InlineData("SELECT * FROM t WHERE c = [ /* ] note */ 'secret-name', 'other']", "SELECT * FROM t WHERE c = [  ?, ?]")]
    [InlineData("SELECT * FROM t WHERE c = [ -- ] note\n 'secret-name', 'other']", "SELECT * FROM t WHERE c = [ \n ?, ?]")]
    public void GetSanitizedSql_ListOfValuesWhichStartsWithStringOrCommentAndHasClosingBracketInIt_SanitizesEveryValue(string sql, string expected)
    {
        // The first closing bracket of a list can be in its first string (which is not delimited by single quotes
        // or has a prefix), or in a comment before its first value, so the content up to it is not a name.
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.DoesNotContain("1234", sqlStatementInfo.SanitizedSql);
        Assert.DoesNotContain("other", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT * FROM UNNEST(['a-b', 'secret-name'])", "SELECT * FROM UNNEST([?, ?])")]
    [InlineData("SELECT * FROM UNNEST(['a/b', 'secret-name'])", "SELECT * FROM UNNEST([?, ?])")]
    [InlineData("SELECT * FROM UNNEST([Email/Name, 'secret-name'])", "SELECT * FROM UNNEST([Email/Name, ?])")]
    [InlineData("SELECT * FROM t WHERE [a-b] = 'secret-name'", "SELECT * FROM t WHERE [a-b] = ?")]
    [InlineData("SELECT * FROM t WHERE [a/b] = 'secret-name'", "SELECT * FROM t WHERE [a/b] = ?")]
    public void GetSanitizedSql_BracketedContentWithDashOrSlashWhichIsNotComment_IsNotTreatedAsComment(string sql, string expected)
    {
        // A dash or a slash on its own does not start a comment, so it does not make the content of a
        // bracket be an identifier, or a list of values be parsed as anything other than SQL.
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData(
        "SELECT * FROM t WHERE a IN ('secret-name', 'secret-name' AND b IN ('secret-name', 'secret-name'",
        "SELECT * FROM t WHERE a IN (?, ? AND b IN (?, ?")]
    [InlineData(
        "SELECT * FROM t WHERE a IN ('secret-name' IN ('secret-name' IN ('secret-name'",
        "SELECT * FROM t WHERE a IN (? IN (? IN (?")]
    [InlineData(
        "SELECT * FROM t WHERE a IN ('secret-name', b IN (1, 2 AND c IN ('secret-name') AND d IN ('secret-name'",
        "SELECT * FROM t WHERE a IN (?) AND d IN (?")]
    [InlineData(
        "SELECT * FROM t WHERE a IN ('secret-name' AND b IN ('secret-name', 'secret-name')",
        "SELECT * FROM t WHERE a IN (?)")]
    public void GetSanitizedSql_InClauseWhichIsNotTerminated_SanitizesEachValue(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("[--a]\n")]
    [InlineData("[/*a]\n*/")]
    [InlineData("[N'a]")]
    [InlineData("[2024's]")]
    public void GetSanitizedSql_ManyBracketsWhichCouldStartAListOfValues_DoesNotTakeTimeWhichGrowsWithTheSquareOfTheLength(string bracketed)
    {
        // Looking for the bracket which would end a list of values reads on from each bracket which could start one, and
        // for a comment which ends at the end of a line it does not find one, so without a limit on the total read it would
        // read to the end of the statement each time, which for a statement of this length is billions of steps.
        var sql = "SELECT * FROM t WHERE x = " + string.Concat(Enumerable.Repeat(bracketed, 40_000)) + " AND y = 'secret-name'";

        var stopwatch = Stopwatch.StartNew();
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);
        stopwatch.Stop();

        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Sanitizing {sql.Length:N0} characters took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void GetSanitizedSql_ManyInClausesWhichAreNotTerminated_DoesNotTakeTimeWhichGrowsWithTheSquareOfTheLength()
    {
        // Looking for the end of each list of values would read to the end of the statement each time when there is none,
        // which for a statement of this length is tens of billions of steps, rather than the few milliseconds it takes
        // when it is only looked for once.
        const string Value = "secret-name";
        var sql = "SELECT * FROM t WHERE " + string.Concat(Enumerable.Repeat($"IN ('{Value}', ", 40_000));

        var stopwatch = Stopwatch.StartNew();
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);
        stopwatch.Stop();

        Assert.DoesNotContain(Value, sqlStatementInfo.SanitizedSql);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Sanitizing {sql.Length:N0} characters took {stopwatch.Elapsed}.");
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
    [InlineData("[Server]...['Orders]", "INSERT [Server]...['Orders]")]
    [InlineData("[Server] . . . ['Orders]", "INSERT [Server]...['Orders]")]
    [InlineData("Server...['Orders]", "INSERT Server...['Orders]")]
    [InlineData("Server . . . ['Orders]", "INSERT Server...['Orders]")]
    [InlineData("[Server]..['Orders]", "INSERT [Server]..['Orders]")]
    [InlineData("[Server].[Database]..['Orders]", "INSERT [Server].[Database]..['Orders]")]
    [InlineData("[Server]....['Orders]", "INSERT [Server]....['Orders]")]
    [InlineData("Server . . . . ['Orders]", "INSERT Server....['Orders]")]
    public void GetSanitizedSql_OmittedNamePartsAndQuoteInIdentifier_SanitizesFollowingLiteral(string target, string expectedSummary)
    {
        var sql = $"INSERT INTO {target} VALUES ('customer@example.com')";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        Assert.Equal($"INSERT INTO {target} VALUES (?)", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expectedSummary, sqlStatementInfo.DbQuerySummary);
    }

    [Theory]
    [InlineData("[Server]...['Orders]")]
    [InlineData("Server...['Orders]")]
    [InlineData("[Server].dbo.['Orders]")]
    [InlineData("Server . dbo . ['Orders]")]
    [InlineData("[Server].[Database].[dbo].['Orders]")]
    public void GetSanitizedSql_MultiPartNameAfterSummaryLengthLimit_SanitizesFollowingLiteral(string target)
    {
        var firstName = new string('A', 128);
        var secondName = new string('B', 128);
        var prefix = $"INSERT INTO dbo.[{firstName}] VALUES (1); INSERT INTO dbo.[{secondName}] VALUES (2); ";
        var expectedPrefix = $"INSERT INTO dbo.[{firstName}] VALUES (?); INSERT INTO dbo.[{secondName}] VALUES (?); ";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql($"{prefix}INSERT INTO {target} VALUES ('customer@example.com')");

        Assert.Equal($"{expectedPrefix}INSERT INTO {target} VALUES (?)", sqlStatementInfo.SanitizedSql);
        Assert.Equal($"INSERT dbo.[{firstName}]", sqlStatementInfo.DbQuerySummary);
    }

    [Fact]
    public void GetSanitizedSql_LongPeriodRun_SanitizesFollowingLiteralAndLimitsSummary()
    {
        var target = $"[Server]{new string('.', 10_000)}['Orders]";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql($"INSERT INTO {target} VALUES ('customer@example.com')");

        Assert.Equal($"INSERT INTO {target} VALUES (?)", sqlStatementInfo.SanitizedSql);
        Assert.Equal("INSERT", sqlStatementInfo.DbQuerySummary);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetSanitizedSql_DelimitedNameExceedingSummaryLengthLimit_OmitsPartialName(bool firstContainsSpace)
    {
        // Each part is a valid (<= 128 character) delimited identifier, but the whole name does not fit in the summary.
        var first = firstContainsSpace ? $"{new string('A', 50)} {new string('A', 49)}" : new string('A', 100);
        var sql = $"INSERT INTO [{first}].[{new string('B', 100)}].[{new string('C', 100)}] VALUES (@p0)";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        Assert.Equal(sql, sqlStatementInfo.SanitizedSql);
        Assert.Equal("INSERT", sqlStatementInfo.DbQuerySummary);
    }

    [Theory]
    [InlineData("[dbo].", "INSERT [dbo]")]
    [InlineData("[Archive]..", "INSERT [Archive]")]
    public void GetSanitizedSql_DelimitedNamePartTooLong_OmitsTrailingPeriod(string qualifier, string expectedSummary)
    {
        // A delimited identifier is limited to 128 characters, so the last part of the name is not captured.
        var sql = $"INSERT INTO {qualifier}[{new string('A', 200)}] VALUES (@p0)";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        Assert.Equal(sql, sqlStatementInfo.SanitizedSql);
        Assert.Equal(expectedSummary, sqlStatementInfo.DbQuerySummary);
    }

    [Theory]
    [InlineData(true, "INSERT [dbo].[Orders]")]
    [InlineData(false, "INSERT [dbo]")]
    public void GetSanitizedSql_HashCommentAfterPeriod_IsOnlySkippedForBackslashEscapeDialects(bool useBackslashEscapes, string expectedSummary)
    {
        // A # starts a comment in MySQL and MariaDB, but is not part of a name in other dialects.
        var sql = "INSERT INTO [dbo]. # comment\n[Orders] VALUES (@p0)";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes);

        Assert.Equal(expectedSummary, sqlStatementInfo.DbQuerySummary);
    }

    [Theory]
    [InlineData(false, 120, 126, false)]
    [InlineData(true, 120, 122, false)]
    [InlineData(true, 120, 122, true)]
    public void GetSanitizedSql_MultiPartNameEndingAtSummaryLengthLimit_OmitsPartialName(bool delimited, int firstLength, int secondLength, bool firstContainsSpace)
    {
        // The summary is exactly 255 characters once the period after the second part has been added,
        // so the last part is not captured and the incomplete name must not be left in the summary.
        // A delimited name can contain whitespace, which must not be mistaken for the end of the name.
        var first = firstContainsSpace
            ? $"{new string('A', firstLength / 2)} {new string('A', (firstLength / 2) - 1)}"
            : new string('A', firstLength);
        var second = new string('B', secondLength);
        var name = delimited ? $"[{first}].[{second}].[Orders]" : $"{first}.{second}.[Orders]";
        var sql = $"INSERT INTO {name} VALUES (@p0)";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        Assert.Equal(sql, sqlStatementInfo.SanitizedSql);
        Assert.Equal("INSERT", sqlStatementInfo.DbQuerySummary);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetSanitizedSql_StatementLongerThanCacheLimit_IsNotCached(bool useBackslashEscapes)
    {
        var sql = $"INSERT INTO Notes (Body) VALUES ('{new string('x', 16 * 1024)}')";
        var copy = new string(sql.ToCharArray());

        var first = SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes);
        var second = SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes);
        var fromCopy = SqlProcessor.GetSanitizedSql(copy, useBackslashEscapes);

        Assert.Equal("INSERT INTO Notes (Body) VALUES (?)", first.SanitizedSql);
        Assert.Equal("INSERT Notes", first.DbQuerySummary);

        Assert.NotSame(first.SanitizedSql, second.SanitizedSql);
        Assert.NotSame(first.SanitizedSql, fromCopy.SanitizedSql);
        Assert.Equal(first.SanitizedSql, second.SanitizedSql);
        Assert.Equal(first.SanitizedSql, fromCopy.SanitizedSql);
        Assert.Equal(first.DbQuerySummary, second.DbQuerySummary);
        Assert.Equal(first.DbQuerySummary, fromCopy.DbQuerySummary);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetSanitizedSql_StatementLongerThanCacheLimit_IsNotRetained(bool useBackslashEscapes)
    {
        var reference = SanitizeLongStatement(useBackslashEscapes);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(reference.TryGetTarget(out _));

        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference<string> SanitizeLongStatement(bool useBackslashEscapes)
        {
            var sql = $"INSERT INTO Notes (Body) VALUES ('{new string('x', 16 * 1024)}')";
            SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes);
            return new WeakReference<string>(sql);
        }
    }

    [Theory]
    [InlineData(8192, true)]
    [InlineData(8193, false)]
    public void GetSanitizedSql_StatementAtCacheLimit_IsCachedOnlyIfNotLonger(int length, bool expectedCached)
    {
        var prefix = $"SELECT * FROM Orders WHERE Token = '{Guid.NewGuid()}' AND Note = '";
        var sql = prefix + new string('x', length - prefix.Length - 1) + "'";
        var copy = new string(sql.ToCharArray());

        Assert.Equal(length, sql.Length);

        var first = SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes: true);
        var fromCopy = SqlProcessor.GetSanitizedSql(copy, useBackslashEscapes: true);

        Assert.Equal("SELECT * FROM Orders WHERE Token = ? AND Note = ?", first.SanitizedSql);
        Assert.Equal(expectedCached, ReferenceEquals(first.SanitizedSql, fromCopy.SanitizedSql));
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
