// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using static OpenTelemetry.Instrumentation.SqlProcessor;

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
    [InlineData("b$$tag$", "$tag$")]
    [InlineData("b$prefix$$tag$", "$tag$")]
    [InlineData("b$$$", "$$")]
    [InlineData("b$$$$", "$$")]
    [InlineData("b$$\u00e9$", "$\u00e9$")]
    [InlineData("b\u0301$$tag$", "$tag$")]
    public void GetSanitizedSql_InClauseIdentifierWithRejectedDollarQuote_DoesNotChangeLiteralBoundaries(string identifier, string delimiter)
    {
        var sql = $"SELECT * FROM t WHERE a IN ('x', {identifier}) AND d = '{delimiter}) secret-name'";

        var result = GetSanitizedSql(sql);

        Assert.Equal("SELECT * FROM t WHERE a IN (?) AND d = ?", result.SanitizedSql);
        Assert.Equal("SELECT t", result.DbQuerySummary);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0x1F")]
    [InlineData("0b101")]
    [InlineData("0o173")]
    public void GetSanitizedSql_InClauseNumericFirstValue_DoesNotChangeFailedDollarQuoteBoundaries(string firstValue)
    {
        var sql = $"SELECT * FROM t WHERE a IN ({firstValue}, b$$tag$) AND d = '$tag$) secret-name'";

        var result = GetSanitizedSql(sql);

        Assert.Equal("SELECT * FROM t WHERE a IN (?) AND d = ?", result.SanitizedSql);
        Assert.Equal("SELECT t", result.DbQuerySummary);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0x1F")]
    [InlineData("0b101")]
    [InlineData("0o173")]
    [InlineData("1.5")]
    [InlineData("1.5e3")]
    [InlineData("1_000")]
    public void GetSanitizedSql_InClauseDollarQuoteDirectlyAfterNumber_IsNotPartOfAnIdentifier(string number)
    {
        var sql = $"SELECT * FROM t WHERE a IN ({number}$$) secret-name$$)";

        var result = GetSanitizedSql(sql);

        Assert.Equal("SELECT * FROM t WHERE a IN (?)", result.SanitizedSql);
        Assert.Equal("SELECT t", result.DbQuerySummary);
    }

    [Fact]
    public void GetSanitizedSql_RejectedDollarQuoteInUnterminatedInClause_DoesNotFindParenthesisInLiteral()
    {
        const string Sql = "SELECT * FROM t WHERE a IN ('x', b$$tag$ AND d = '$tag$) secret-name'";

        var result = GetSanitizedSql(Sql);

        Assert.Equal("SELECT * FROM t WHERE a IN (?, b$$tag$ AND d = ?", result.SanitizedSql);
        Assert.Equal("SELECT t", result.DbQuerySummary);
    }

    [Theory]
    [InlineData("SELECT * FROM t WHERE a IN ('x', $$a$$$$secret-name$$)")]
    [InlineData("SELECT * FROM t WHERE a IN ('x', 'a'$$secret-name$$)")]
    [InlineData("SELECT * FROM t WHERE a IN ('x', flags#$$) secret-name$$)")]
    [InlineData("SELECT * FROM t WHERE a IN ('x', b$$9$tag$) AND d = '$tag$) secret-name'")]
    public void GetSanitizedSql_InClauseGenuineLiteralsAndInvalidTags_KeepTheirBoundaries(string sql)
    {
        var result = GetSanitizedSql(sql);

        Assert.DoesNotContain("secret-name", result.SanitizedSql, StringComparison.Ordinal);
        Assert.Equal("SELECT t", result.DbQuerySummary);
    }

    [Theory]
    [InlineData("SELECT * FROM Users WHERE Name IN ('carol@example.com', \"Fabrikam (UK)\", \"alice@example.com\", \"x\", \"secret-name\")")]
    [InlineData("SELECT * FROM Users WHERE Name IN ('a', \"b\\\")c\", 'secret-name')")]
    [InlineData("SELECT * FROM Users WHERE Name IN (1, 2 # don't )\n, 'secret-name')")]
    public void GetSanitizedSql_InClauseLiteralOrCommentContainingCloseParenWithBackslashDialect_SanitizesAllLiterals(string sql)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.MySql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Name IN (?)", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT Users", sqlStatementInfo.DbQuerySummary);

        // Control: the same values quoted with single quotes are also sanitized.
        var control = SqlProcessor.GetSanitizedSql(sql.Replace('"', '\''), SqlDialect.MySql);
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

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.MySql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Password = ?", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_BackslashEscapedQuoteInInClauseWithBackslashDialect_SanitizesLiterals()
    {
        var sql = "SELECT * FROM Users WHERE Name IN ('a\\'secret-name', 'b')";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.MySql);

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

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, ToDialect(useBackslashEscapes));

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Password = ?", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_BackslashBeforeDoubledQuoteWithoutBackslashDialect_DoesNotLeak()
    {
        var sql = "SELECT * FROM Users WHERE Password = 'a\\''secret-name'";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.Default);

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

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.MySql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-value", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Name = ? AND Id = ?", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_DoubledDoubleQuoteEscapeWithBackslashDialect_SanitizesLiteral()
    {
        var sql = "SELECT * FROM Users WHERE Name = \"a\"\"secret-value\"";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.MySql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-value", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Name = ?", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_BackslashEscapedDoubleQuoteWithBackslashDialect_SanitizesLiteral()
    {
        var sql = "SELECT * FROM Users WHERE Name = \"a\\\"secret-value\"";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.MySql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-value", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Name = ?", sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("UPDATE Cases SET Note = '''The customer's code is secret-name''' WHERE CaseId = 4711", "UPDATE Cases SET Note = ? WHERE CaseId = ?")]
    [InlineData("UPDATE Cases SET Note = '''He said 'my code is secret-name' today''' WHERE CaseId = 4711", "UPDATE Cases SET Note = ? WHERE CaseId = ?")]
    [InlineData("UPDATE Cases SET Note = '''The customer's call''', Owner = 'secret-name' WHERE CaseId = 4711", "UPDATE Cases SET Note = ?, Owner = ? WHERE CaseId = ?")]
    [InlineData("UPDATE Cases SET Note = \"\"\"{\"name\": \"secret-name\"}\"\"\" WHERE CaseId = 4711", "UPDATE Cases SET Note = ? WHERE CaseId = ?")]
    [InlineData("UPDATE Cases SET Note = '''it\\'''s secret-name''' WHERE CaseId = 4711", "UPDATE Cases SET Note = ? WHERE CaseId = ?")]
    [InlineData("UPDATE Cases SET Note = '''a\nsecret-name''' WHERE CaseId = 4711", "UPDATE Cases SET Note = ? WHERE CaseId = ?")]
    [InlineData("UPDATE Cases SET Note = '''''' WHERE Note IN ('''it's)''', 'secret-name')", "UPDATE Cases SET Note = ? WHERE Note IN (?)")]
    [InlineData("UPDATE Cases SET Note = '''it's secret-name WHERE CaseId = 4711", "UPDATE Cases SET Note = ?")]
    [InlineData("UPDATE Customers SET LastName = 'O\\'Brien', AccessCode = 'secret-name' WHERE CustomerId = 7", "UPDATE Customers SET LastName = ?, AccessCode = ? WHERE CustomerId = ?")]
    [InlineData("SELECT * FROM Customers WHERE Email = \"secret-name@example.com\"", "SELECT * FROM Customers WHERE Email = ?")]
    [InlineData("SELECT * FROM Customers WHERE Name = \"secret-name\" AND Tag IN UNNEST([\"secret-tag\", \"x\"])", "SELECT * FROM Customers WHERE Name = ? AND Tag IN UNNEST([?, ?])")]
    [InlineData("SELECT a # don't\nFROM t WHERE k = 'secret-name'", "SELECT a \nFROM t WHERE k = ?")]
    public void GetSanitizedSql_TripleQuotedLiteralDialect_SanitizesLiteral(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.GoogleSql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetSanitizedSql_TripleQuoteWithoutTripleQuotedLiteralDialect_IsEscapedQuote(bool useBackslashEscapes)
    {
        // In the other dialects '''' is a literal containing a single quote, so the literal which follows is a separate one.
        var sql = "SELECT * FROM Users WHERE Nickname = '''' AND Password = 'secret-name'";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, ToDialect(useBackslashEscapes));

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal("SELECT * FROM Users WHERE Nickname = ? AND Password = ?", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_UnterminatedDoubleQuotedStringWithBackslashDialect_SanitizesLiteral()
    {
        var sql = "SELECT * FROM Users WHERE Name = \"secret-value";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.MySql);

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
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, ToDialect(useBackslashEscapes));

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        // The quoted identifier is copied verbatim, and its quote does not affect the literals.
        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);

        // Control: the same statement without a quote in the identifier.
        var control = SqlProcessor.GetSanitizedSql(WithoutApostrophe(sql), ToDialect(useBackslashEscapes));
        Assert.Equal(WithoutApostrophe(expected), control.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT `a\\`b` FROM t WHERE a = '`' AND b = 'secret-name'", "SELECT `a\\`b` FROM t WHERE a = ? AND b = ?")]
    [InlineData("SELECT `a\\\\\\`b` FROM t WHERE a = '`' AND b = 'secret-name'", "SELECT `a\\\\\\`b` FROM t WHERE a = ? AND b = ?")]
    [InlineData("SELECT `a\\`b\\`c` FROM t WHERE a = '`' AND b = 'secret-name'", "SELECT `a\\`b\\`c` FROM t WHERE a = ? AND b = ?")]
    [InlineData("SELECT * FROM t WHERE a IN ('x', `a\\`)b`) AND b = '`' AND c = 'secret-name'", "SELECT * FROM t WHERE a IN (?) AND b = ? AND c = ?")]
    [InlineData("SELECT `a\\\\` FROM t WHERE a = '`' AND b = 'secret-name'", "SELECT `a\\\\` FROM t WHERE a = ? AND b = ?")]
    public void GetSanitizedSql_GoogleSqlEscapedIdentifiers_DoNotChangeLiteralBoundaries(string sql, string expected)
    {
        var result = GetSanitizedSql(sql, SqlDialect.GoogleSql);

        Assert.Equal(expected, result.SanitizedSql);
        Assert.DoesNotContain("secret-name", result.SanitizedSql, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-name", result.DbQuerySummary, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSanitizedSql_MySqlBackslashes_DoNotEscapeIdentifierDelimiters()
    {
        const string sql = "SELECT `a\\` FROM t WHERE a = 'secret-name'";

        var result = GetSanitizedSql(sql, SqlDialect.MySql);

        Assert.Equal("SELECT `a\\` FROM t WHERE a = ?", result.SanitizedSql);
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
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, ToDialect(useBackslashEscapes));

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
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, ToDialect(useBackslashEscapes));

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
    [InlineData("0b101010101")]
    [InlineData("0B101010101")]
    [InlineData("0o173")]
    [InlineData("0O173")]
    [InlineData("0xABC_DEF")]
    [InlineData("0X_ABC_DEF")]
    [InlineData("0b_1010_1010")]
    [InlineData("0o_173_001")]
    [InlineData("-0xDEADBEEF")]
    [InlineData("+0XDEADBEEF")]
    [InlineData("-0b101010101")]
    [InlineData("+0o173")]
    [InlineData("1_000_000")]
    [InlineData("1_234.567_890")]
    [InlineData(".123_456")]
    [InlineData("-1_234.567_890")]
    [InlineData("1_234e+1_000")]
    [InlineData("1.234e-1_000")]
    [InlineData("1.25f")]
    [InlineData("1.25F")]
    [InlineData("1.25d")]
    [InlineData("1.25D")]
    [InlineData("-1.25e+10f")]
    [InlineData("+.25D")]
    public void GetSanitizedSql_NumericLiteral_IsEntirelySanitized(string literal)
    {
        var statement = GetSanitizedSql($"SELECT {literal} FROM t");

        Assert.Equal("SELECT ? FROM t", statement.SanitizedSql);
        Assert.Equal("SELECT t", statement.DbQuerySummary);
    }

    [Theory]
    [InlineData((int)SqlDialect.MySql, "0b101010101")]
    [InlineData((int)SqlDialect.Default, "0o173")]
    [InlineData((int)SqlDialect.Sqlite, "1_000_000")]
    [InlineData((int)SqlDialect.Oracle, "1.25f")]
    [InlineData((int)SqlDialect.Oracle, "1.25D")]
    public void GetSanitizedSql_NumericLiteralInExpression_IsEntirelySanitized(int dialect, string literal)
    {
        var statement = GetSanitizedSql($"SELECT * FROM t WHERE x = {literal} AND y = 'secret'", (SqlDialect)dialect);

        Assert.Equal("SELECT * FROM t WHERE x = ? AND y = ?", statement.SanitizedSql);
        Assert.Equal("SELECT t", statement.DbQuerySummary);
    }

    [Theory]
    [InlineData("SELECT 0b1010, 0o173, 0xABC_DEF, 1_000_000, 1.25f FROM t", "SELECT ?, ?, ?, ?, ? FROM t")]
    [InlineData("INSERT INTO t VALUES (-0xDEADBEEF, +0b1010, .123_456)", "INSERT INTO t VALUES (?, ?, ?)")]
    [InlineData("SELECT * FROM t WHERE x IN (0b1010, 0o173, 1.25d)", "SELECT * FROM t WHERE x IN (?)")]
    [InlineData("SELECT * FROM t WHERE x IN (0b1010) AND y = 0xABC_DEF", "SELECT * FROM t WHERE x IN (?) AND y = ?")]
    public void GetSanitizedSql_MultipleNumericLiterals_AreEntirelySanitized(string sql, string expected)
    {
        var statement = GetSanitizedSql(sql);

        Assert.Equal(expected, statement.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT value_123, value0xABC, value0b101, value0o173 FROM t")]
    [InlineData("SELECT 1 FROM t AS df")]
    public void GetSanitizedSql_NumericTextInIdentifiers_IsPreserved(string sql)
    {
        var statement = GetSanitizedSql(sql);

        Assert.Equal(sql.Replace("SELECT 1", "SELECT ?"), statement.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT 1foo", "SELECT ?foo")]
    [InlineData("SELECT 1dollars", "SELECT ?dollars")]
    [InlineData("SELECT 1_name", "SELECT ?_name")]
    [InlineData("SELECT 1__000", "SELECT ?__000")]
    [InlineData("SELECT 0bank", "SELECT ?bank")]
    [InlineData("SELECT 0octopus", "SELECT ?octopus")]
    [InlineData("SELECT 0xABC_tail", "SELECT ?_tail")]
    public void GetSanitizedSql_WordFollowingNumericPrefix_IsPreserved(string sql, string expected)
    {
        var statement = GetSanitizedSql(sql);

        Assert.Equal(expected, statement.SanitizedSql);
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

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, ToDialect(useBackslashEscapes));

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
        var backslashDialect = SqlProcessor.GetSanitizedSql(sql, SqlDialect.MySql);
        var defaultDialectAgain = SqlProcessor.GetSanitizedSql(sql);
        var backslashDialectAgain = SqlProcessor.GetSanitizedSql(sql, SqlDialect.MySql);

        Assert.Equal($"SELECT {columns} FROM Users WHERE Name = \"secret-value\" AND Id = ?", defaultDialect.SanitizedSql);
        Assert.Equal($"SELECT {columns} FROM Users WHERE Name = ? AND Id = ?", backslashDialect.SanitizedSql);
        Assert.Equal(defaultDialect.SanitizedSql, defaultDialectAgain.SanitizedSql);
        Assert.Equal(backslashDialect.SanitizedSql, backslashDialectAgain.SanitizedSql);
    }

    [Theory]
    [InlineData("ALTER USER app_reader IDENTIFIED BY Tr0ub4dor3xQ9", "ALTER USER ? IDENTIFIED BY ?")]
    [InlineData("ALTER USER app_reader identified by Tr0ub4dor3xQ9", "ALTER USER ? identified by ?")]
    [InlineData("alter user app_reader Identified By NewPw_2026 Replace OldPw_2025", "alter user ? Identified By ? Replace ?")]
    [InlineData("ALTER USER if123 IDENTIFIED BY NewPw_2026 REPLACE OldPw_2025", "ALTER USER ? IDENTIFIED BY ? REPLACE ?")]
    [InlineData("ALTER USER if IDENTIFIED BY NewSecret REPLACE OldSecret", "ALTER USER ? IDENTIFIED BY ? REPLACE ?")]
    [InlineData("ALTER USER if#123 IDENTIFIED BY NewSecret REPLACE OldSecret", "ALTER USER ? IDENTIFIED BY ? REPLACE ?")]
    [InlineData("ALTER USER not IDENTIFIED BY NewSecret", "ALTER USER ? IDENTIFIED BY ?")]
    [InlineData("ALTER USER exists IDENTIFIED BY NewSecret", "ALTER USER ? IDENTIFIED BY ?")]
    [InlineData("CREATE USER IF NOT EXISTS alice IDENTIFIED BY NewSecret", "CREATE USER IF NOT EXISTS ? IDENTIFIED BY ?")]
    [InlineData("Create Or Replace User alice Identified By pw", "Create Or Replace User ? Identified By ?")]
    [InlineData("CREATE USER app_writer IDENTIFIED BY \"P@ss#w0rd!Zk7\" DEFAULT TABLESPACE users", "CREATE USER ? IDENTIFIED BY \"?\" DEFAULT TABLESPACE users")]
    [InlineData("ALTER USER app_reader IDENTIFIED BY NewPw_2026 REPLACE OldPw_2025", "ALTER USER ? IDENTIFIED BY ? REPLACE ?")]
    [InlineData("ALTER USER app_reader IDENTIFIED BY \"New Pw\" /* c */ REPLACE \"Old Pw\"", "ALTER USER ? IDENTIFIED BY \"?\"  REPLACE \"?\"")]
    [InlineData("CREATE DATABASE LINK sales_link CONNECT TO remote_user IDENTIFIED BY LinkPw_9x USING 'sales_tns'", "CREATE DATABASE LINK sales_link CONNECT TO remote_user IDENTIFIED BY ? USING ?")]
    [InlineData("CREATE ROLE dw_manager IDENTIFIED BY RolePw_1", "CREATE ROLE dw_manager IDENTIFIED BY ?")]
    [InlineData("GRANT CONNECT TO new_user IDENTIFIED BY GrantPw_1", "GRANT CONNECT TO new_user IDENTIFIED BY ?")]
    [InlineData("SET ROLE dw_manager IDENTIFIED BY RolePw_1", "SET ROLE dw_manager IDENTIFIED BY ?")]
    [InlineData("ALTER USER u IDENTIFIED BY Secret#Winter$1", "ALTER USER ? IDENTIFIED BY ?")]
    [InlineData("ALTER USER u IDENTIFIED BY Select1 DEFAULT ROLE ALL", "ALTER USER ? IDENTIFIED BY ? DEFAULT ROLE ALL")]
    [InlineData("ALTER USER u IDENTIFIED BY If_9", "ALTER USER ? IDENTIFIED BY ?")]
    [InlineData("CREATE USER u IDENTIFIED BY VALUES 'S:0123456789ABCDEF'", "CREATE USER ? IDENTIFIED BY VALUES ?")]
    [InlineData("CREATE USER u IDENTIFIED BY Values", "CREATE USER ? IDENTIFIED BY ?")]
    [InlineData("CREATE USER u IDENTIFIED BY 1pw DEFAULT TABLESPACE users", "CREATE USER ? IDENTIFIED BY ? DEFAULT TABLESPACE users")]
    [InlineData("ALTER USER u IDENTIFIED BY N'new-secret' REPLACE old_secret", "ALTER USER ? IDENTIFIED BY ? REPLACE ?")]
    [InlineData("CREATE USER u IDENTIFIED BY nq'[secret-pw]' DEFAULT TABLESPACE users", "CREATE USER ? IDENTIFIED BY ? DEFAULT TABLESPACE users")]
    [InlineData("CREATE USER u IDENTIFIED BY \"abc", "CREATE USER ? IDENTIFIED BY \"?")]
    [InlineData("ALTER USER u IDENTIFIED BY tiger; SELECT REPLACE(name, 'a', 'b') FROM t", "ALTER USER ? IDENTIFIED BY ?; SELECT REPLACE(name, ?, ?) FROM t")]
    [InlineData("ALTER USER u IDENTIFIED BY 'new-secret' REPLACE old_secret", "ALTER USER ? IDENTIFIED BY ? REPLACE ?")]
    [InlineData("CREATE USER u IDENTIFIED BY pw PASSWORD EXPIRE", "CREATE USER ? IDENTIFIED BY ? PASSWORD EXPIRE")]
    public void GetSanitizedSql_PasswordWhichIsNotALiteral_IsSanitized(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("ALTER USER a\u0301lice IDENTIFIED BY p\u0301assword", "ALTER USER ? IDENTIFIED BY ?")]
    [InlineData("ALTER USER alice IDENTIFIED BY p\u0301assword REPLACE o\u0301ldpassword", "ALTER USER ? IDENTIFIED BY ? REPLACE ?")]
    public void GetSanitizedSql_OracleUnicodePasswords_AreRedactedAsWholeTokens(string sql, string expected)
    {
        var result = GetSanitizedSql(sql, SqlDialect.Oracle);

        Assert.Equal(expected, result.SanitizedSql);
    }

    [Theory]
    [InlineData("CREATE USER u IDENTIFIED BY 'secret-pw' REQUIRE SSL", "CREATE USER ? IDENTIFIED BY ? REQUIRE SSL")]
    [InlineData("CREATE USER u IDENTIFIED BY \"secret-pw\" REQUIRE SSL", "CREATE USER ? IDENTIFIED BY ? REQUIRE SSL")]
    [InlineData("ALTER USER u IDENTIFIED BY 'new-pw' REPLACE 'old-pw'", "ALTER USER ? IDENTIFIED BY ? REPLACE ?")]
    public void GetSanitizedSql_PasswordWhichIsALiteralWithBackslashDialect_IsSanitized(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.MySql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("-pw", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("CREATE USER [alice@contoso.com] FOR LOGIN [alice@contoso.com]", "CREATE USER [?] FOR LOGIN [?]")]
    [InlineData("CREATE USER alice FROM LOGIN alice", "CREATE USER ? FROM LOGIN ?")]
    [InlineData("create user alice from login alice with default_schema = [dbo]", "create user ? from login ? with default_schema = [dbo]")]
    [InlineData("IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'alice@contoso.com') DROP USER [alice@contoso.com]", "IF EXISTS (SELECT ? FROM sys.database_principals WHERE name = ?) DROP USER [?]")]
    [InlineData("IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'alice') CREATE LOGIN [alice] WITH PASSWORD = N'Pa55w0rd!'", "IF NOT EXISTS (SELECT ? FROM sys.server_principals WHERE name = ?) CREATE LOGIN [?] WITH PASSWORD = ?")]
    [InlineData("CREATE LOGIN [alice] WITH PASSWORD = N'Pa55w0rd!'; CREATE USER [alice] FOR LOGIN [alice]", "CREATE LOGIN [?] WITH PASSWORD = ?; CREATE USER [?] FOR LOGIN [?]")]
    [InlineData("DROP USER [alice]; DROP LOGIN [alice]", "DROP USER [?]; DROP LOGIN [?]")]
    [InlineData("SELECT 1; CREATE LOGIN alice WITH PASSWORD = 'Pa55w0rd!'", "SELECT ?; CREATE LOGIN ? WITH PASSWORD = ?")]
    [InlineData("INSERT INTO audit VALUES (1); DROP LOGIN [alice]", "INSERT INTO audit VALUES (?); DROP LOGIN [?]")]
    [InlineData("SELECT 1; DROP USER IF EXISTS alice", "SELECT ?; DROP USER IF EXISTS ?")]
    [InlineData("ALTER LOGIN [alice] WITH NAME = [bob]", "ALTER LOGIN [?] WITH NAME = [?]")]
    [InlineData("ALTER USER [alice] WITH NAME = [bob], LOGIN = [carol]", "ALTER USER [?] WITH NAME = [?], LOGIN = [?]")]
    [InlineData("ALTER USER alice WITH LOGIN = alice", "ALTER USER ? WITH LOGIN = ?")]
    [InlineData("ALTER USER alice RENAME TO bob", "ALTER USER ? RENAME TO ?")]
    [InlineData("DROP USER alice, bob", "DROP USER ?, ?")]
    [InlineData("SELECT 1; DROP USER alice, bob; SELECT a, b FROM t", "SELECT ?; DROP USER ?, ?; SELECT a, b FROM t")]
    [InlineData("IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'alice') ALTER LOGIN [alice] WITH NAME = [bob]", "IF EXISTS (SELECT ? FROM sys.server_principals WHERE name = ?) ALTER LOGIN [?] WITH NAME = [?]")]
    [InlineData("ALTER TABLE t ADD c int\nALTER USER alice WITH NAME = bob", "ALTER TABLE t ADD c int\nALTER USER ? WITH NAME = ?")]
    [InlineData("DROP USER alice,\n bob , [carol]", "DROP USER ?,\n ? , [?]")]
    public void GetSanitizedSql_LoginOrUserNames_AreSanitized(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("DROP USER alice\nSELECT a, b FROM t", "DROP USER ?\nSELECT a, b FROM t")]
    [InlineData("ALTER TABLE accounts ALTER login TYPE text", "ALTER TABLE accounts ALTER login TYPE text")]
    [InlineData("ALTER USER type RENAME TO bob", "ALTER USER ? RENAME TO ?")]
    [InlineData("CREATE USER 1alice IDENTIFIED BY 'x'", "CREATE USER ?? IDENTIFIED BY ?")]
    [InlineData("DROP USER 'alice', bob", "DROP USER ?, ?")]
    [InlineData("DROP USER alice, 'bob', carol", "DROP USER ?, ?, ?")]
    [InlineData("DROP USER alice@host, bob@host", "DROP USER ?, ?")]
    [InlineData("DROP USER if_x, not1", "DROP USER ?, ?")]
    [InlineData("DROP USER exists, not", "DROP USER ?, ?")]
    [InlineData("DROP USER IF EXISTS alice, bob", "DROP USER IF EXISTS ?, ?")]
    [InlineData("DROP USER IF /* c */ EXISTS alice, bob", "DROP USER IF  EXISTS ?, ?")]
    [InlineData("DROP USER if", "DROP USER ?")]
    [InlineData("DROP USER IF EXISTS .[alice@example.com]", "DROP USER IF EXISTS .[?]")]
    [InlineData("CREATE TABLE audit (id int); DROP USER IF EXISTS .[alice@example.com]", "CREATE TABLE audit (id int); DROP USER IF EXISTS .[?]")]
    [InlineData("DROP USER .alice", "DROP USER .?")]
    [InlineData("CREATE USER from_date FOR LOGIN select1", "CREATE USER ? FOR LOGIN ?")]
    [InlineData("CREATE USER db_reader FOR LOGIN acct$alice", "CREATE USER ? FOR LOGIN ?")]
    [InlineData("SELECT 1; CREATE OR REPLACE USER alice IDENTIFIED BY 'x'", "SELECT ?; CREATE OR REPLACE USER ? IDENTIFIED BY ?")]
    [InlineData("ALTER TABLE t ADD c int\nALTER LOGIN [alice] WITH NAME = [bob]", "ALTER TABLE t ADD c int\nALTER LOGIN [?] WITH NAME = [?]")]
    [InlineData("SELECT 1; ALTER TABLE accounts ALTER login TYPE text", "SELECT ?; ALTER TABLE accounts ALTER login TYPE text")]
    [InlineData("ALTER TABLE accounts ALTER login SET DEFAULT 'x'", "ALTER TABLE accounts ALTER login SET DEFAULT ?")]
    [InlineData("ALTER TABLE accounts DROP login, email", "ALTER TABLE accounts DROP login, email")]
    [InlineData("ALTER TABLE accounts DROP login CASCADE", "ALTER TABLE accounts DROP login CASCADE")]
    [InlineData("ALTER TABLE accounts ALTER login COLLATE nocase", "ALTER TABLE accounts ALTER login COLLATE nocase")]
    [InlineData("CREATE USER alice WITHOUT LOGIN\nRESTORE DATABASE sales WITH NAME = archive", "CREATE USER ? WITHOUT LOGIN\nRESTORE DATABASE sales WITH NAME = archive")]
    [InlineData("CREATE USER alice WITHOUT LOGIN\nREVOKE SELECT ON t FROM u, v", "CREATE USER ? WITHOUT LOGIN\nREVOKE SELECT ON t FROM u, v")]
    [InlineData("ALTER TABLE accounts DROP COLUMN login", "ALTER TABLE accounts DROP COLUMN login")]
    [InlineData("CREATE USER alice WITHOUT LOGIN\nALTER DATABASE sales MODIFY NAME = archive", "CREATE USER ? WITHOUT LOGIN\nALTER DATABASE sales MODIFY NAME = archive")]
    [InlineData("DROP USER alice, 'bob'; SELECT col FROM t", "DROP USER ?, ?; SELECT col FROM t")]
    [InlineData("DROP USER 'bob'\nSELECT col FROM t", "DROP USER ?\nSELECT col FROM t")]
    [InlineData("ALTER TABLE t ALTER COLUMN login TYPE text; ALTER USER alice WITH LOGIN = bob", "ALTER TABLE t ALTER COLUMN login TYPE text; ALTER USER ? WITH LOGIN = ?")]
    [InlineData("CREATE LOGIN alice WITH PASSWORD = 'x', DEFAULT_DATABASE = sales", "CREATE LOGIN ? WITH PASSWORD = ?, DEFAULT_DATABASE = sales")]
    [InlineData("ALTER DATABASE sales MODIFY NAME = archive", "ALTER DATABASE sales MODIFY NAME = archive")]
    [InlineData("ALTER TABLE sales RENAME TO archive", "ALTER TABLE sales RENAME TO archive")]
    [InlineData("ALTER USER alice SET search_path TO sales", "ALTER USER ? SET search_path TO sales")]
    [InlineData("UPDATE t SET name = bob, login = carol WHERE id = 1", "UPDATE t SET name = bob, login = carol WHERE id = ?")]
    [InlineData("SELECT * FROM login WHERE user = 'alice'", "SELECT * FROM login WHERE user = ?")]
    [InlineData("CREATE USER [alice] WITHOUT LOGIN; SELECT * FROM login WHERE id = 1", "CREATE USER [?] WITHOUT LOGIN; SELECT * FROM login WHERE id = ?")]
    public void GetSanitizedSql_NotLoginOrUserName_IsNotSanitized(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("CREATE USER a\u0301lice WITH PASSWORD 'secret-password'", "CREATE USER ? WITH PASSWORD ?")]
    [InlineData("CREATE USER \U0001D49Clice WITH PASSWORD 'secret-password'", "CREATE USER ? WITH PASSWORD ?")]
    [InlineData("ALTER USER a\u0301lice RENAME TO b\u0301ob", "ALTER USER ? RENAME TO ?")]
    [InlineData("DROP USER \U0001D49Clice, b\u0301ob", "DROP USER ?, ?")]
    [InlineData("CREATE USER U&\"d\\0061t\\+000061\" WITH PASSWORD 'secret-password'", "CREATE USER U&\"?\" WITH PASSWORD ?")]
    [InlineData("ALTER USER alice RENAME TO u&\"d!0061ta\" UESCAPE '!'", "ALTER USER ? RENAME TO u&\"?\" UESCAPE ?")]
    [InlineData("DROP USER U&\"d\\0061ta\", bob", "DROP USER U&\"?\", ?")]
    [InlineData("DROP USER U&\"d!0061ta\" UESCAPE '!', bob", "DROP USER U&\"?\" UESCAPE ?, ?")]
    [InlineData("DROP USER U&\"a\" UESCAPE '!', U&\"b\" UESCAPE '!', bob", "DROP USER U&\"?\" UESCAPE ?, U&\"?\" UESCAPE ?, ?")]
    public void GetSanitizedSql_PostgreSqlUnicodeAccountNames_AreRedactedAsWholeTokens(string sql, string expected)
    {
        var result = GetSanitizedSql(sql);

        Assert.Equal(expected, result.SanitizedSql);
        Assert.DoesNotContain("lice", result.DbQuerySummary, StringComparison.Ordinal);
        Assert.DoesNotContain("0061", result.DbQuerySummary, StringComparison.Ordinal);
        Assert.DoesNotContain("\u0301", result.DbQuerySummary, StringComparison.Ordinal);
        Assert.DoesNotContain("\U0001D49C", result.DbQuerySummary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SELECT * FROM accounts; DROP USER alice, bob", "SELECT * FROM accounts; DROP USER ?, ?", "SELECT accounts")]
    [InlineData("SELECT from_date, user_id FROM t WHERE a = 'x'", "SELECT from_date, user_id FROM t WHERE a = ?", "SELECT t")]
    [InlineData("SELECT alter, user FROM t WHERE x = 'secret-name'", "SELECT alter, user FROM t WHERE x = ?", "SELECT t")]
    [InlineData("SELECT create, login FROM t", "SELECT create, login FROM t", "SELECT t")]
    [InlineData("SELECT modify user FROM Orders", "SELECT modify user FROM Orders", "SELECT Orders")]
    [InlineData("SELECT 'x' user, a FROM t", "SELECT ? user, a FROM t", "SELECT t")]
    [InlineData("ALTER USER u IDENTIFIED BY 'secret-pw'; REPLACE INTO t VALUES (1)", "ALTER USER ? IDENTIFIED BY ?; REPLACE INTO t VALUES (?)", "ALTER USER")]
    [InlineData("CREATE USER u IDENTIFIED BY secret_pw; REPLACE INTO t VALUES (1)", "CREATE USER ? IDENTIFIED BY ?; REPLACE INTO t VALUES (?)", "CREATE USER")]
    [InlineData("DROP USER if not_x", "DROP USER ? not_x", "DROP USER")]
    [InlineData("CREATE TABLE audit (id int); CREATE USER IF NOT EXISTS 'alice' IDENTIFIED BY 'password'", "CREATE TABLE audit (id int); CREATE USER IF NOT EXISTS ? IDENTIFIED BY ?", "CREATE TABLE audit")]
    [InlineData("CREATE TABLE audit (id int); DROP USER IF EXISTS [alice] FROM LOGIN [alice]", "CREATE TABLE audit (id int); DROP USER IF EXISTS [?] FROM LOGIN [?]", "CREATE TABLE audit")]
    [InlineData("DROP USER alice, 'bob', carol", "DROP USER ?, ?, ?", "DROP USER")]
    [InlineData("CREATE USER db_reader FOR LOGIN acct$alice", "CREATE USER ? FOR LOGIN ?", "CREATE USER")]
    [InlineData("SELECT 1; CREATE OR REPLACE USER alice IDENTIFIED BY 'x'", "SELECT ?; CREATE OR REPLACE USER ? IDENTIFIED BY ?", "SELECT")]
    [InlineData("IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'alice') DROP USER [alice]", "IF EXISTS (SELECT ? FROM sys.database_principals WHERE name = ?) DROP USER [?]", "SELECT sys.database_principals")]
    public void GetSanitizedSql_LoginOrUserNames_AreNotInSummary(string sql, string expectedSql, string expectedSummary)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.Equal(expectedSql, sqlStatementInfo.SanitizedSql);
        Assert.Equal(expectedSummary, sqlStatementInfo.DbQuerySummary);
    }

    [Theory]
    [InlineData("CREATE USER [alice@contoso.com] FOR LOGIN [alice@contoso.com]", "CREATE USER [?] FOR LOGIN [?]")]
    [InlineData("ALTER USER alice RENAME TO bob", "ALTER USER ? RENAME TO ?")]
    [InlineData("DROP USER alice, bob", "DROP USER ?, ?")]
    [InlineData("DROP USER alice, 'bob'; SELECT col FROM t", "DROP USER ?, ?; SELECT col FROM t")]
    [InlineData("DROP USER IF EXISTS alice", "DROP USER IF EXISTS ?")]
    [InlineData("ALTER USER if#123 IDENTIFIED BY 'secret-pw'", "ALTER USER ?")]
    [InlineData("CREATE USER if#comment\nIDENTIFIED BY 'secret-pw'", "CREATE USER ?\nIDENTIFIED BY ?")]
    public void GetSanitizedSql_LoginOrUserNamesWithBackslashDialect_AreSanitized(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.MySql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("CREATE USER alice IDENTIFIED BY 'first', bob IDENTIFIED BY 'second'", "CREATE USER ? IDENTIFIED BY ?, ? IDENTIFIED BY ?")]
    [InlineData("CREATE USER 'alice'@'localhost' IDENTIFIED BY 'first', bob@localhost IDENTIFIED BY 'second'", "CREATE USER ?@? IDENTIFIED BY ?, ? IDENTIFIED BY ?")]
    [InlineData("ALTER USER alice IDENTIFIED BY 'first', bob IDENTIFIED BY 'second'", "ALTER USER ? IDENTIFIED BY ?, ? IDENTIFIED BY ?")]
    [InlineData("ALTER USER 'alice'@localhost IDENTIFIED BY 'first'", "ALTER USER ?@? IDENTIFIED BY ?")]
    [InlineData("DROP USER 'alice'@localhost, 'bob'@localhost", "DROP USER ?@?, ?@?")]
    [InlineData("CREATE USER IF NOT EXISTS alice, bob, carol", "CREATE USER IF NOT EXISTS ?, ?, ?")]
    [InlineData("CREATE USER alice WITH MAX_QUERIES_PER_HOUR 10, bob WITH MAX_QUERIES_PER_HOUR 20", "CREATE USER ? WITH MAX_QUERIES_PER_HOUR ?, ? WITH MAX_QUERIES_PER_HOUR ?")]
    [InlineData("RENAME USER alice TO bob, carol TO dave", "RENAME USER ? TO ?, ? TO ?")]
    [InlineData("RENAME USER 'alice'@'localhost' TO bob@localhost, carol TO 'dave'", "RENAME USER ?@? TO ?, ? TO ?")]
    [InlineData("CREATE USER alice, /* next account */ bob; SELECT name, email FROM contacts", "CREATE USER ?,  ?; SELECT name, email FROM contacts")]
    [InlineData("CREATE USER alice DEFAULT ROLE reporter, reader", "CREATE USER ? DEFAULT ROLE reporter, reader")]
    [InlineData("ALTER USER alice IDENTIFIED BY 'first'; SELECT name, email FROM contacts", "ALTER USER ? IDENTIFIED BY ?; SELECT name, email FROM contacts")]
    [InlineData("ALTER TABLE t RENAME COLUMN user TO customer", "ALTER TABLE t RENAME COLUMN user TO customer")]
    [InlineData("CREATE USER alice@`private_host` IDENTIFIED BY 'pw'", "CREATE USER ?@`?` IDENTIFIED BY ?")]
    [InlineData("CREATE USER alice@'private_host' IDENTIFIED BY 'pw'", "CREATE USER ?@? IDENTIFIED BY ?")]
    [InlineData("CREATE USER alice@`h1` IDENTIFIED BY 'pw', bob@`h2` IDENTIFIED BY 'pw'", "CREATE USER ?@`?` IDENTIFIED BY ?, ?@`?` IDENTIFIED BY ?")]
    [InlineData("CREATE USER alice@ `private_host` IDENTIFIED BY 'pw'", "CREATE USER ?@ `?` IDENTIFIED BY ?")]
    [InlineData("CREATE USER alice@ /* c */ `private_host` IDENTIFIED BY 'pw'", "CREATE USER ?@  `?` IDENTIFIED BY ?")]
    [InlineData("CREATE USER alice@\n`private_host` IDENTIFIED BY 'pw'", "CREATE USER ?@\n`?` IDENTIFIED BY ?")]
    [InlineData("CREATE USER alice@ private_host IDENTIFIED BY 'pw'", "CREATE USER ?@ ? IDENTIFIED BY ?")]
    [InlineData("CREATE USER 'alice'@ 'private_host' IDENTIFIED BY 'pw'", "CREATE USER ?@ ? IDENTIFIED BY ?")]
    [InlineData("SELECT @a, @b FROM t WHERE c = @d", "SELECT @a, @b FROM t WHERE c = @d")]
    public void GetSanitizedSql_MySqlAccountLists_AreRedactedWithoutAffectingOtherLists(string sql, string expected)
    {
        var result = GetSanitizedSql(sql, SqlDialect.MySql);

        Assert.Equal(expected, result.SanitizedSql);
        Assert.DoesNotContain("alice", result.DbQuerySummary, StringComparison.Ordinal);
        Assert.DoesNotContain("bob", result.DbQuerySummary, StringComparison.Ordinal);
        Assert.DoesNotContain("carol", result.DbQuerySummary, StringComparison.Ordinal);
        Assert.DoesNotContain("dave", result.DbQuerySummary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CREATE USER u IDENTIFIED BY RANDOM PASSWORD", "CREATE USER ? IDENTIFIED BY RANDOM PASSWORD")]
    [InlineData("CREATE USER u IDENTIFIED BY PASSWORD 'secret-hash'", "CREATE USER ? IDENTIFIED BY PASSWORD ?")]
    [InlineData("CREATE USER u IDENTIFIED BY RANDOM#don't\nPASSWORD COMMENT 'secret-name'", "CREATE USER ? IDENTIFIED BY RANDOM\nPASSWORD COMMENT ?")]
    public void GetSanitizedSql_PasswordModifierWithBackslashDialect_IsNotChanged(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.MySql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_ManyReplaceWordsAfterPassword_CompletesInBoundedTime()
    {
        var sql = "ALTER USER u IDENTIFIED BY pw" + string.Concat(Enumerable.Repeat(" /*c*/", 40_000)) + " ; " + string.Concat(Enumerable.Repeat("SELECT REPLACE(a, 'b', 'c') /*c*/ ", 40_000));

        var stopwatch = Stopwatch.StartNew();
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);
        stopwatch.Stop();

        Assert.StartsWith("ALTER USER ? IDENTIFIED BY ?", sqlStatementInfo.SanitizedSql, StringComparison.Ordinal);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Took {stopwatch.Elapsed}.");
    }

    [Theory]
    [InlineData("CREATE USER u IDENTIFIED EXTERNALLY", "CREATE USER ? IDENTIFIED EXTERNALLY")]
    [InlineData("SELECT a FROM t GROUP BY b", "SELECT a FROM t GROUP BY b")]
    [InlineData("SELECT a FROM t ORDER BY b", "SELECT a FROM t ORDER BY b")]
    [InlineData("SELECT Password FROM Users WHERE Password = Hash", "SELECT Password FROM Users WHERE Password = Hash")]
    [InlineData("CREATE USER u WITH PASSWORD = @pw", "CREATE USER ? WITH PASSWORD = @pw")]
    [InlineData("CREATE LOGIN u WITH PASSWORD = @pw", "CREATE LOGIN ? WITH PASSWORD = @pw")]
    [InlineData("CREATE USER u WITH PASSWORD = 'secret-pw'", "CREATE USER ? WITH PASSWORD = ?")]
    public void GetSanitizedSql_NotAPassword_IsNotChanged(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT Id, Name AS ['Tis the season] FROM Accounts WHERE ApiKey = 'secret-name'", "SELECT Id, Name AS ['Tis the season] FROM Accounts WHERE ApiKey = ?")]
    [InlineData("SELECT Total AS ['24 revenue] FROM Invoices WHERE Customer = 'Contoso Ltd' AND Ssn = 'secret-name'", "SELECT Total AS ['24 revenue] FROM Invoices WHERE Customer = ? AND Ssn = ?")]
    [InlineData("UPDATE Accounts SET Verified = 1 WHERE[Mother's Maiden Name] = 'secret-name' AND Email = 'bob@example.com'", "UPDATE Accounts SET Verified = ? WHERE[Mother's Maiden Name] = ? AND Email = ?")]
    [InlineData("DELETE FROM[O'Brien] WHERE [Owner] = 'secret-name'", "DELETE FROM[O'Brien] WHERE [Owner] = ?")]
    [InlineData("SELECT x INTO[Bob's Backup] FROM t WHERE y = 'secret-name'", "SELECT x INTO[Bob's Backup] FROM t WHERE y = ?")]
    [InlineData("SELECT TOP 5[Bob's col] FROM t WHERE y = 'secret-name'", "SELECT TOP ?[Bob's col] FROM t WHERE y = ?")]
    [InlineData("SELECT * FROM t PIVOT (SUM(x) FOR y IN (['90s], [2000s])) p WHERE owner = 'secret-name'", "SELECT * FROM t PIVOT (SUM(x) FOR y IN (['90s], [2000s])) p WHERE owner = ?")]
    [InlineData("SELECT * FROM [a].['s Name] WHERE owner = 'secret-name'", "SELECT * FROM [a].['s Name] WHERE owner = ?")]
    [InlineData("SELECT [ 'x] FROM t WHERE a = 'secret-name'", "SELECT [ 'x] FROM t WHERE a = ?")]
    [InlineData("SELECT [a]]'b] FROM t WHERE a = 'secret-name'", "SELECT [a]]'b] FROM t WHERE a = ?")]
    [InlineData("SELECT [2024] FROM t WHERE a = 'secret-name'", "SELECT [2024] FROM t WHERE a = ?")]
    public void GetSanitizedSql_BracketsDelimitIdentifiersOnly_BracketIsIdentifier(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.SqlServer);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_BracketsDelimitIdentifiersOnlyWithNameLongerThanLimit_RedactsRemainder()
    {
        // SQL Server limits an identifier to 128 characters, but SQLite does not, so it is not known where this one ends.
        var sql = $"SELECT ['{new string('a', 200)}] FROM t WHERE a = 'secret-name'";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.SqlServer);

        Assert.Equal("SELECT [?", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_BracketsDelimitIdentifiersOnlyWithNameLongerThanLimitInInClause_RedactsRemainder()
    {
        var name = $"['{new string('a', 130)}]] ) ']";
        var sql = $"SELECT x FROM t WHERE y IN ('v', {name}) AND z = 'secret-name'";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.SqlServer);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_BracketsDelimitIdentifiersOnlyWithNameLongerThanLimitWithoutApostrophe_BracketIsIdentifier()
    {
        // SQLite does not limit the length of an identifier, and there is no string literal in this one.
        var name = $"[{new string('a', 200)}]";
        var sql = $"SELECT {name} FROM Accounts WHERE ApiKey = 'secret-name'";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.SqlServer);

        Assert.Equal($"SELECT {name} FROM Accounts WHERE ApiKey = ?", sqlStatementInfo.SanitizedSql);
        Assert.Equal($"SELECT Accounts", sqlStatementInfo.DbQuerySummary);
    }

    [Fact]
    public void GetSanitizedSql_BracketsDelimitIdentifiersOnlyWithNameAtLimit_BracketIsIdentifier()
    {
        var name = $"['{new string('a', 100)}]";
        var sql = $"SELECT {name} FROM t WHERE a = 'secret-name'";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.SqlServer);

        Assert.Equal($"SELECT {name} FROM t WHERE a = ?", sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    public void GetSanitizedSql_SameInstanceWithBracketsDelimitIdentifiersOnly_RespectsDialect(int extraColumns)
    {
        var columns = GetColumnList(extraColumns);
        var sql = $"SELECT {columns}, ['Tis the season] FROM t WHERE a = 'secret-name'";

        var identifiersOnly = SqlProcessor.GetSanitizedSql(sql, SqlDialect.SqlServer);
        var defaultDialect = SqlProcessor.GetSanitizedSql(sql);
        var identifiersOnlyAgain = SqlProcessor.GetSanitizedSql(sql, SqlDialect.SqlServer);
        var defaultDialectAgain = SqlProcessor.GetSanitizedSql(sql);

        Assert.Equal($"SELECT {columns}, ['Tis the season] FROM t WHERE a = ?", identifiersOnly.SanitizedSql);
        Assert.NotEqual(identifiersOnly.SanitizedSql, defaultDialect.SanitizedSql);
        Assert.Equal(identifiersOnly.SanitizedSql, identifiersOnlyAgain.SanitizedSql);
        Assert.Equal(defaultDialect.SanitizedSql, defaultDialectAgain.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT price$gbp$ AS Value FROM accounts WHERE owner = 'mallory$gbp$' AND tenant_key = 'secret-name'", "SELECT price$gbp$ AS Value FROM accounts WHERE owner = ? AND tenant_key = ?")]
    [InlineData("SELECT price$$ AS Value FROM accounts WHERE owner = 'mallory$$' AND tenant_key = 'secret-name'", "SELECT price$$ AS Value FROM accounts WHERE owner = ? AND tenant_key = ?")]
    [InlineData("INSERT INTO notes (owner, cost$gbp$, note) VALUES ('mallory', 1, 'x$gbp$'), ('alice', 2, 'secret-name 4111')", "INSERT INTO notes (owner, cost$gbp$, note) VALUES (?, ?, ?), (?, ?, ?)")]
    [InlineData("SELECT * FROM t WHERE a IN ('x', b$$c) AND d = '$$' AND e = 'secret-name'", "SELECT * FROM t WHERE a IN (?) AND d = ? AND e = ?")]
    [InlineData("SELECT DR$idx$I FROM t WHERE a = '$idx$' AND b = 'secret-name'", "SELECT DR$idx$I FROM t WHERE a = ? AND b = ?")]
    [InlineData("SELECT col1$gbp$ FROM t WHERE a = '$gbp$' AND b = 'secret-name'", "SELECT col1$gbp$ FROM t WHERE a = ? AND b = ?")]
    [InlineData("SELECT fee_$gbp$ FROM t WHERE a = '$gbp$' AND b = 'secret-name'", "SELECT fee_$gbp$ FROM t WHERE a = ? AND b = ?")]
    [InlineData("SELECT \U0001D49C$gbp$ FROM t WHERE a = '$gbp$' AND b = 'secret-name'", "SELECT \U0001D49C$gbp$ FROM t WHERE a = ? AND b = ?")]
    [InlineData("SELECT [Price$gbp$] FROM t WHERE a = '$gbp$' AND b = 'secret-name'", "SELECT [Price$gbp$] FROM t WHERE a = ? AND b = ?")]
    public void GetSanitizedSql_DollarSignInIdentifier_IsNotDollarQuote(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT $$secret-name$$", "SELECT ?")]
    [InlineData("SELECT * FROM t WHERE a = $$secret-name$$ AND b = 1", "SELECT * FROM t WHERE a = ? AND b = ?")]
    [InlineData("SELECT fn($$secret-name$$, $tag$secret-name$tag$)", "SELECT fn(?, ?)")]
    [InlineData("SELECT * FROM t WHERE a IN ($$secret-name$$, 'x')", "SELECT * FROM t WHERE a IN (?, ?)")]
    [InlineData("SELECT * FROM t WHERE tsv@@$$secret-name$$", "SELECT * FROM t WHERE tsv@@?")]
    [InlineData("SELECT $$a$$$$secret-name$$", "SELECT ??")]
    [InlineData("SELECT $t$a$t$$t$secret-name$t$", "SELECT ??")]
    [InlineData("SELECT * FROM t WHERE a IN ($$a$$$$secret-name$$, 'x') AND b = 'y'", "SELECT * FROM t WHERE a IN (??, ?) AND b = ?")]
    [InlineData("SELECT * FROM t WHERE a = 0x1F$$secret-name$$", "SELECT * FROM t WHERE a = ??")]
    [InlineData("SELECT * FROM t WHERE a = 1$$secret-name$$", "SELECT * FROM t WHERE a = ??")]
    [InlineData("SELECT * FROM t WHERE a = 'x'$$secret-name$$", "SELECT * FROM t WHERE a = ??")]
    [InlineData("SELECT * FROM t WHERE arr<@$$secret-name$$", "SELECT * FROM t WHERE arr<@?")]
    [InlineData("SELECT * FROM t WHERE flags#$$secret-name$$", "SELECT * FROM t WHERE flags#?")]
    public void GetSanitizedSql_DollarQuotedString_IsSanitized(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT $\u00e9$secret-name$\u00e9$", "SELECT ?")]
    [InlineData("SELECT $t\u00e4g$secret-name$t\u00e4g$", "SELECT ?")]
    [InlineData("SELECT $\U0001D49C$secret-name$\U0001D49C$", "SELECT ?")]
    [InlineData("SELECT $a\u0301$secret-name$a\u0301$", "SELECT ?")]
    [InlineData("SELECT $\u00e9$secret-name", "SELECT ?")]
    [InlineData("SELECT * FROM t WHERE a IN ($\u00e9$secret-name$\u00e9$, 'x')", "SELECT * FROM t WHERE a IN (?, ?)")]
    [InlineData("SELECT a\u0301$tag$ FROM t WHERE a = '$tag$' AND b = 'secret-name'", "SELECT a\u0301$tag$ FROM t WHERE a = ? AND b = ?")]
    public void GetSanitizedSql_UnicodeDollarQuoteTags_AreRecognized(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.DoesNotContain("secret-name", sqlStatementInfo.DbQuerySummary);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData((int)SqlDialect.MySql, "SELECT $$col FROM t WHERE a = '$$' AND b = 'secret-name'", "SELECT $$col FROM t WHERE a = ? AND b = ?")]
    [InlineData((int)SqlDialect.MySql, "SELECT $tag$col FROM t WHERE a = '$tag$' AND b = 'secret-name'", "SELECT $tag$col FROM t WHERE a = ? AND b = ?")]
    [InlineData((int)SqlDialect.MySql, "SELECT $\u00e9$col FROM t WHERE a = '$\u00e9$' AND b = 'secret-name'", "SELECT $\u00e9$col FROM t WHERE a = ? AND b = ?")]
    [InlineData((int)SqlDialect.SqlServer, "SELECT @$tag$col FROM t WHERE a = '$tag$' AND b = 'secret-name'", "SELECT @$tag$col FROM t WHERE a = ? AND b = ?")]
    [InlineData((int)SqlDialect.SqlServer, "SELECT * FROM #$tag$col WHERE a = '$tag$' AND b = 'secret-name'", "SELECT * FROM #$tag$col WHERE a = ? AND b = ?")]
    [InlineData((int)SqlDialect.Sqlite, "SELECT $tag$col, '$tag$', 'secret-name'", "SELECT $tag$col, ?, ?")]
    [InlineData((int)SqlDialect.Sqlite, "SELECT :$tag$col, '$tag$', 'secret-name'", "SELECT :$tag$col, ?, ?")]
    [InlineData((int)SqlDialect.Sqlite, "SELECT @$tag$col, '$tag$', 'secret-name'", "SELECT @$tag$col, ?, ?")]
    [InlineData((int)SqlDialect.MySql, "SELECT * FROM t WHERE a IN ('x', $tag$col) AND b = '$tag$' AND c = 'secret-name'", "SELECT * FROM t WHERE a IN (?) AND b = ? AND c = ?")]
    [InlineData((int)SqlDialect.Default, "SELECT $tag$secret-name$tag$", "SELECT ?")]
    public void GetSanitizedSql_DollarSigns_AreParsedAccordingToTheSourceDialect(int dialect, string sql, string expected)
    {
        var result = GetSanitizedSql(sql, (SqlDialect)dialect);

        Assert.Equal(expected, result.SanitizedSql);
        Assert.DoesNotContain("secret-name", result.SanitizedSql, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-name", result.DbQuerySummary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("BEGIN $IF $$DEBUG $THEN dbms_output.put_line('secret-name'); $END END;", "BEGIN $IF $$DEBUG $THEN dbms_output.put_line(?); $END END;")]
    [InlineData("SELECT $$PLSQL_UNIT, 'secret-name' FROM dual", "SELECT $$PLSQL_UNIT, ? FROM dual")]
    [InlineData("SELECT $$a$$, 'secret-name' FROM dual", "SELECT $$a$$, ? FROM dual")]
    public void GetSanitizedSql_DollarSignsWithOracleDialect_AreNotStringsInDollarQuotes(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.Oracle);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("CREATE USER u AS PASSWORD = $$tdsecret$$, PERM = 5", "CREATE USER ? AS PASSWORD = ?, PERM = ?")]
    [InlineData("SELECT $$a, 'secret-name' FROM t", "SELECT $$a, ? FROM t")]
    [InlineData("SELECT e'a\\' , 'secret-name' FROM t", "SELECT e? , ? FROM t")]
    public void GetSanitizedSql_DollarSignsAndPrefixesWithTeradataDialect_AreNotStrings(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.Teradata);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("tdsecret", sqlStatementInfo.SanitizedSql);
        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("SELECT q'{customer's secret-name}' FROM RDB$DATABASE", "SELECT ? FROM RDB$DATABASE")]
    [InlineData("SELECT Q'[customer's secret-name]' FROM RDB$DATABASE", "SELECT ? FROM RDB$DATABASE")]
    [InlineData("SELECT q'(customer's secret-name)' FROM RDB$DATABASE", "SELECT ? FROM RDB$DATABASE")]
    [InlineData("SELECT q'<customer's secret-name>' FROM RDB$DATABASE", "SELECT ? FROM RDB$DATABASE")]
    [InlineData("SELECT q'!customer's secret-name!' FROM RDB$DATABASE", "SELECT ? FROM RDB$DATABASE")]
    [InlineData("SELECT q' customer's secret-name ' FROM RDB$DATABASE", "SELECT ? FROM RDB$DATABASE")]
    [InlineData("SELECT q'\tcustomer's secret-name\t' FROM RDB$DATABASE", "SELECT ? FROM RDB$DATABASE")]
    [InlineData("SELECT q'\ncustomer's secret-name\n' FROM RDB$DATABASE", "SELECT ? FROM RDB$DATABASE")]
    [InlineData("SELECT q'\rcustomer's secret-name\r' FROM RDB$DATABASE", "SELECT ? FROM RDB$DATABASE")]
    [InlineData("SELECT q''customer's secret-name'' FROM RDB$DATABASE", "SELECT ? FROM RDB$DATABASE")]
    [InlineData("SELECT * FROM t WHERE a IN ('x', q'[customer's ) secret-name]') AND b = 'other-secret'", "SELECT * FROM t WHERE a IN (?) AND b = ?")]
    [InlineData("SELECT * FROM t WHERE a IN ('x', q' customer's ) secret-name ') AND b = 'other-secret'", "SELECT * FROM t WHERE a IN (?) AND b = ?")]
    public void GetSanitizedSql_AlternativeQuotedStringsWithFirebirdDialect_AreSanitized(string sql, string expected)
    {
        var result = GetSanitizedSql(sql, SqlDialect.Firebird);

        Assert.Equal(expected, result.SanitizedSql);
        Assert.DoesNotContain("secret-name", result.DbQuerySummary, StringComparison.Ordinal);
        Assert.DoesNotContain("other-secret", result.DbQuerySummary, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSanitizedSql_AlternativeQuotedStringsWithOracleDialect_AreSanitized()
    {
        const string sql = "SELECT q'{customer's secret-name}' FROM dual";

        var result = GetSanitizedSql(sql, SqlDialect.Oracle);

        Assert.Equal("SELECT ? FROM dual", result.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_StringInDollarQuotesWithDefaultDialect_IsSanitized()
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql("SELECT $$secret-name$$ FROM t");

        Assert.Equal("SELECT ? FROM t", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_EscapedBracketWithSqliteDialect_EndsAtTheFirstBracket()
    {
        // SQLite does not escape a closing bracket with another, so [x] is the identifier, and the literal which follows is one.
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql("SELECT [x]] , 'secret-name' , [y] FROM t", SqlDialect.Sqlite);

        Assert.Equal("SELECT [x]] , ? , [y] FROM t", sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_EscapedBracketWithSqlServerDialect_IsPartOfTheIdentifier()
    {
        // SQL Server escapes a closing bracket with another, so the identifier goes on to the last bracket.
        var sql = "SELECT [x]] , 'secret-name' , [y] FROM t";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.SqlServer);

        Assert.Equal(sql, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("CREATE USER u AS PERM = 1000, PASSWORD = TdSecret_1", "CREATE USER ? AS PERM = ?, PASSWORD = ?")]
    [InlineData("ALTER USER u AS PASSWORD = 1abc", "ALTER USER ? AS PASSWORD = ?")]
    [InlineData("alter user u as Password = TdSecret_1", "alter user ? as Password = ?")]
    [InlineData("CREATE USER u AS PASSWORD /* c */ = TdSecret_1", "CREATE USER ? AS PASSWORD  = ?")]
    [InlineData("ALTER USER alice SET service. password = $body$token:secret-pw$body$", "ALTER USER ? SET service. password = ?")]
    [InlineData("ALTER USER u IDENTIFIED BY $$secret-pw$$", "ALTER USER ? IDENTIFIED BY ?")]
    [InlineData("ALTER USER u IDENTIFIED BY $$secret-pw$$ REPLACE $x$old:secret-pw$x$", "ALTER USER ? IDENTIFIED BY ? REPLACE ?")]
    [InlineData("CREATE USER u AS PASSWORD = $$secret-pw$$, PERM = 5", "CREATE USER ? AS PASSWORD = ?, PERM = ?")]
    [InlineData("CREATE USER u IDENTIFIED BY Tr0ub$4dor#", "CREATE USER ? IDENTIFIED BY ?")]
    [InlineData("CREATE LOGIN u WITH PASSWORD = 'secret-pw' MUST_CHANGE, CHECK_EXPIRATION = ON", "CREATE LOGIN ? WITH PASSWORD = ? MUST_CHANGE, CHECK_EXPIRATION = ON")]
    public void GetSanitizedSql_PasswordAfterEqualsSign_IsSanitized(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-pw", sqlStatementInfo.SanitizedSql);
        Assert.DoesNotContain("TdSecret_1", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("MODIFY USER u AS PASSWORD = TdSecret_1", "MODIFY USER ? AS PASSWORD = ?")]
    [InlineData("modify user u as perm = 1000, password = TdSecret_1", "modify user ? as perm = ?, password = ?")]
    [InlineData("SELECT 1; MODIFY USER u AS PASSWORD = TdSecret_1", "SELECT ?; MODIFY USER ? AS PASSWORD = ?")]
    public void GetSanitizedSql_ModifyUserPassword_IsSanitized(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        Assert.DoesNotContain("TdSecret_1", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_AllDialects_AreInAll()
    {
#if NET
        var dialects = Enum.GetValues<SqlDialect>();
#else
        var dialects = Enum.GetValues(typeof(SqlDialect)).Cast<SqlDialect>();
#endif

        foreach (var dialect in dialects)
        {
            Assert.Equal(dialect, dialect & SqlDialect.All);
        }
    }

    [Theory]
    [InlineData("ALTER TABLE t ADD c int\nALTER USER type WITH NAME = bob", "ALTER TABLE t ADD c int\nALTER USER ? WITH NAME = ?")]
    [InlineData("ALTER TABLE t ADD c int\nALTER USER type RENAME TO bob", "ALTER TABLE t ADD c int\nALTER USER ? RENAME TO ?")]
    [InlineData("ALTER TABLE t ADD c int\nALTER USER type IDENTIFIED BY pw1", "ALTER TABLE t ADD c int\nALTER USER ? IDENTIFIED BY ?")]
    [InlineData("ALTER TABLE t ADD c int\nALTER LOGIN set WITH PASSWORD = 'pw'", "ALTER TABLE t ADD c int\nALTER LOGIN ? WITH PASSWORD = ?")]
    [InlineData("ALTER TABLE t ADD c int\nDROP USER type, bob", "ALTER TABLE t ADD c int\nDROP USER ?, ?")]
    [InlineData("ALTER TABLE t ALTER user TYPE text", "ALTER TABLE t ALTER user TYPE text")]
    [InlineData("ALTER TABLE t ALTER login SET DEFAULT 1", "ALTER TABLE t ALTER login SET DEFAULT ?")]
    [InlineData("ALTER TABLE t DROP user CASCADE", "ALTER TABLE t DROP user CASCADE")]
    [InlineData("ALTER TABLE t DROP user, ADD c int", "ALTER TABLE t DROP user, ADD c int")]
    public void GetSanitizedSql_AccountStatementWhichFollowsAlterTable_IsSanitizedAndColumnsAreNot(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("ALTER TABLE t MODIFY user int", "ALTER TABLE t MODIFY user int")]
    [InlineData("SELECT modify user FROM Orders", "SELECT modify user FROM Orders")]
    [InlineData("SELECT a, modify user FROM Orders", "SELECT a, modify user FROM Orders")]
    [InlineData("SELECT 1 modify user FROM Orders", "SELECT ? modify user FROM Orders")]
    [InlineData("ALTER TABLE t ADD c int, MODIFY user VARCHAR(10)", "ALTER TABLE t ADD c int, MODIFY user VARCHAR(?)")]
    [InlineData("ALTER DATABASE sales MODIFY NAME = archive", "ALTER DATABASE sales MODIFY NAME = archive")]
    [InlineData("IDENTIFIED BY = x", "IDENTIFIED BY = x")]
    [InlineData("CREATE USER u IDENTIFIED BY = x", "CREATE USER ? IDENTIFIED BY = x")]
    public void GetSanitizedSql_ModifyOrEqualsWhichIsNotForAnAccount_IsNotChanged(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("CREATE USER u WITHOUT LOGIN\nSELECT Password = Hash FROM Users", "CREATE USER ? WITHOUT LOGIN\nSELECT Password = Hash FROM Users")]
    [InlineData("CREATE USER u WITH PASSWORD = @pw", "CREATE USER ? WITH PASSWORD = @pw")]
    [InlineData("ALTER USER u PASSWORD EXPIRE", "ALTER USER ? PASSWORD EXPIRE")]
    [InlineData("SELECT * FROM users WHERE password = hash", "SELECT * FROM users WHERE password = hash")]
    [InlineData("UPDATE users SET password = hash WHERE id = 1", "UPDATE users SET password = hash WHERE id = ?")]
    public void GetSanitizedSql_PasswordAfterEqualsSignWhichIsNotOne_IsNotChanged(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);

        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("CREATE USER u AS PASSWORD = TdSecret_1", "CREATE USER ? AS PASSWORD = TdSecret_1")]
    [InlineData("SET PASSWORD = PASSWORD('secret-pw')", "SET PASSWORD = PASSWORD(?)")]
    [InlineData("ALTER USER u IDENTIFIED BY 'secret-pw'", "ALTER USER ? IDENTIFIED BY ?")]
    public void GetSanitizedSql_PasswordWithMySqlDialect_IsOnlySanitizedIfItIsALiteral(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.MySql);

        Assert.DoesNotContain("secret-pw", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Theory]
    [InlineData("/* served on GET /notes/* */ SELECT * FROM Notes WHERE Tag = '*/' AND TenantKey = 'secret-name'", " SELECT * FROM Notes WHERE Tag = ? AND TenantKey = ?")]
    [InlineData("/* a /* b */ SELECT * FROM Notes WHERE Tag = '*/' AND TenantKey = 'secret-name'", " SELECT * FROM Notes WHERE Tag = ? AND TenantKey = ?")]
    [InlineData("SELECT * FROM Notes /* a /* b */ WHERE TenantKey = 'secret-name'", "SELECT * FROM Notes  WHERE TenantKey = ?")]
    [InlineData("SELECT * FROM Notes WHERE Id IN (1, /* a /* b */ 'secret-name', 2)", "SELECT * FROM Notes WHERE Id IN (?)")]
    [InlineData("SELECT * FROM dbo. /* a /* b */ Notes WHERE TenantKey = 'secret-name'", "SELECT * FROM dbo.  Notes WHERE TenantKey = ?")]
    public void GetSanitizedSql_BlockCommentsWhichDoNotNest_EndAtFirstCommentEnd(string sql, string expected)
    {
        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql, SqlDialect.NonNestedComments);

        this.output.WriteLine($"Sanitized: {sqlStatementInfo.SanitizedSql}");

        Assert.DoesNotContain("secret-name", sqlStatementInfo.SanitizedSql);
        Assert.Equal(expected, sqlStatementInfo.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_BlockCommentsWithFirebirdDialect_EndAtTheFirstClosingDelimiter()
    {
        const string sql = "SELECT /* outer /* inner */ 'secret-name' FROM RDB$DATABASE";

        var result = GetSanitizedSql(sql, SqlDialect.Firebird);

        Assert.Equal("SELECT  ? FROM RDB$DATABASE", result.SanitizedSql);
    }

    [Fact]
    public void GetSanitizedSql_BlockCommentsWhichNest_EndAtMatchingCommentEnd()
    {
        var sql = "/* a /* b */ don't */ SELECT * FROM Notes WHERE TenantKey = 'secret-name'";

        var sqlStatementInfo = SqlProcessor.GetSanitizedSql(sql);
        var explicitlyNested = SqlProcessor.GetSanitizedSql(sql, SqlDialect.Default);

        Assert.Equal(" SELECT * FROM Notes WHERE TenantKey = ?", sqlStatementInfo.SanitizedSql);
        Assert.Equal(sqlStatementInfo.SanitizedSql, explicitlyNested.SanitizedSql);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    public void GetSanitizedSql_SameInstanceWithDifferentCommentNesting_RespectsDialect(int extraColumns)
    {
        var columns = GetColumnList(extraColumns);
        var sql = $"/* a /* b */ SELECT {columns} FROM Users WHERE Name = '*/' AND Key = 'secret-name'";

        var nested = SqlProcessor.GetSanitizedSql(sql, SqlDialect.Default);
        var notNested = SqlProcessor.GetSanitizedSql(sql, SqlDialect.NonNestedComments);
        var nestedAgain = SqlProcessor.GetSanitizedSql(sql, SqlDialect.Default);
        var notNestedAgain = SqlProcessor.GetSanitizedSql(sql, SqlDialect.NonNestedComments);

        Assert.Equal($" SELECT {columns} FROM Users WHERE Name = ? AND Key = ?", notNested.SanitizedSql);
        Assert.NotEqual(notNested.SanitizedSql, nested.SanitizedSql);
        Assert.Equal(nested.SanitizedSql, nestedAgain.SanitizedSql);
        Assert.Equal(notNested.SanitizedSql, notNestedAgain.SanitizedSql);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    public void GetSanitizedSql_SameInstanceWithTripleQuotedLiteralDialect_RespectsDialect(int extraColumns)
    {
        var columns = GetColumnList(extraColumns);
        var sql = $"SELECT {columns} FROM Users WHERE Name = '''it's secret-name''' AND Id = 1";

        var backslashDialect = SqlProcessor.GetSanitizedSql(sql, SqlDialect.MySql);
        var tripleQuotedDialect = SqlProcessor.GetSanitizedSql(sql, SqlDialect.GoogleSql);
        var backslashDialectAgain = SqlProcessor.GetSanitizedSql(sql, SqlDialect.MySql);
        var tripleQuotedDialectAgain = SqlProcessor.GetSanitizedSql(sql, SqlDialect.GoogleSql);

        Assert.Equal($"SELECT {columns} FROM Users WHERE Name = ?s secret-name?", backslashDialect.SanitizedSql);
        Assert.Equal($"SELECT {columns} FROM Users WHERE Name = ? AND Id = ?", tripleQuotedDialect.SanitizedSql);
        Assert.Equal(backslashDialect.SanitizedSql, backslashDialectAgain.SanitizedSql);
        Assert.Equal(tripleQuotedDialect.SanitizedSql, tripleQuotedDialectAgain.SanitizedSql);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetSanitizedSql_StatementLongerThanCacheLimit_IsNotCached(bool useBackslashEscapes)
    {
        var sql = $"INSERT INTO Notes (Body) VALUES ('{new string('x', 16 * 1024)}')";
        var copy = new string(sql.ToCharArray());

        var first = SqlProcessor.GetSanitizedSql(sql, ToDialect(useBackslashEscapes));
        var second = SqlProcessor.GetSanitizedSql(sql, ToDialect(useBackslashEscapes));
        var fromCopy = SqlProcessor.GetSanitizedSql(copy, ToDialect(useBackslashEscapes));

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
            SqlProcessor.GetSanitizedSql(sql, ToDialect(useBackslashEscapes));
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

        var first = SqlProcessor.GetSanitizedSql(sql, SqlDialect.MySql);
        var fromCopy = SqlProcessor.GetSanitizedSql(copy, SqlDialect.MySql);

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

    private static SqlDialect ToDialect(bool useBackslashEscapes) => useBackslashEscapes ? SqlDialect.MySql : SqlDialect.Default;
}
