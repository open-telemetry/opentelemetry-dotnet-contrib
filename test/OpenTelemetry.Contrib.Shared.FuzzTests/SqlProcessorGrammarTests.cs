// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using FsCheck.Xunit;
using static OpenTelemetry.Instrumentation.SqlProcessor;

namespace OpenTelemetry.Instrumentation;

/// <summary>
/// Property-based (fuzz) tests that assert nothing a statement contains in a literal or a comment, whatever
/// it is made of and whatever surrounds it, is found in the sanitized statement or in the summary of it.
/// </summary>
public static class SqlProcessorGrammarTests
{
    private const int MaxValue = 1_000;
    private const int MaxSummaryLength = 255;
    private const string FirstLiteralMarker = "private_literal_alpha";
    private const string SecondLiteralMarker = "private_literal_beta";
    private const string TailLiteralMarker = "private_tail_literal";

    private static readonly SqlDialect[] DialectPresets =
    [
        SqlDialect.Default,
        SqlDialect.MySql,
        SqlDialect.GoogleSql,
        SqlDialect.SqlServer,
        SqlDialect.Sqlite,
        SqlDialect.Oracle,
        SqlDialect.Firebird,
        SqlDialect.Teradata,
    ];

    private static readonly string[] LiteralPieces =
    [
        "'", "\"", "`", "\\", "\n", "\r", "(", ")", "[", "]", "/*", "*/",
        "--", "#", "$probe$", "''", ",", ";", "!", " ", "FROM user",
    ];

    private static readonly string[] PathologicalPieces =
    [
        "[", "'", "\"", "`", "(", ")", "IN (", "IN ('a', ", "/*", "-- ",
        "$a$", "FROM a,", "SELECT (", "SELECT [", "ARRAY[", "'a''", "$$",
        "\\'", "/* /*", "WHERE a = (", "N'", "[a]]", "0x", "1e", "--'",
    ];

    [Property(MaxTest = MaxValue, Arbitrary = [typeof(SqlGenerators)])]
    public static void GetSanitizedSql_Never_Reveals_A_Literal_Or_A_Comment(FuzzedSql statement, int contentSeed)
    {
        var (sql, secrets) = statement.Render(contentSeed);

        var actual = SqlProcessor.GetSanitizedSql(sql, ToDialect(statement.UseBackslashEscapes));

        Assert.NotNull(actual.SanitizedSql);
        Assert.NotNull(actual.DbQuerySummary);

        foreach (var secret in secrets)
        {
            Assert.False(
                Contains(actual.SanitizedSql, secret),
                $"The sanitized SQL contains {secret}.{Environment.NewLine}SQL: {sql}{Environment.NewLine}Sanitized: {actual.SanitizedSql}");

            Assert.False(
                Contains(actual.DbQuerySummary, secret),
                $"The summary contains {secret}.{Environment.NewLine}SQL: {sql}{Environment.NewLine}Summary: {actual.DbQuerySummary}");
        }
    }

    [Property(MaxTest = MaxValue, Arbitrary = [typeof(SqlGenerators)])]
    public static void GetSanitizedSql_Does_Not_Depend_On_The_Content_Of_Literals_Or_Comments(FuzzedSql statement, int firstContentSeed, int secondContentSeed)
    {
        // Two statements which differ only in what their literals and comments contain are the same statement once sanitized,
        // so if they are not then something in a literal or a comment has changed how the rest of the statement was read.
        var (first, _) = statement.Render(firstContentSeed);
        var (second, _) = statement.Render(secondContentSeed);

        var firstActual = SqlProcessor.GetSanitizedSql(first, ToDialect(statement.UseBackslashEscapes));
        var secondActual = SqlProcessor.GetSanitizedSql(second, ToDialect(statement.UseBackslashEscapes));

        var description = $"{Environment.NewLine}First: {first}{Environment.NewLine}Second: {second}";

        Assert.True(
            string.Equals(firstActual.SanitizedSql, secondActual.SanitizedSql, StringComparison.Ordinal),
            $"The sanitized SQL differs.{description}{Environment.NewLine}First sanitized: {firstActual.SanitizedSql}{Environment.NewLine}Second sanitized: {secondActual.SanitizedSql}");

        Assert.True(
            string.Equals(firstActual.DbQuerySummary, secondActual.DbQuerySummary, StringComparison.Ordinal),
            $"The summary differs.{description}{Environment.NewLine}First summary: {firstActual.DbQuerySummary}{Environment.NewLine}Second summary: {secondActual.DbQuerySummary}");
    }

    [Property(MaxTest = MaxValue)]
    public static void GetSanitizedSql_All_Dialect_Presets_Keep_Literal_Content_Private(int firstContentSeed, int secondContentSeed, byte contextSelector)
    {
        // Every case exercises every preset. Changing only a literal's contents must leave both outputs unchanged,
        // even when those contents resemble comments, identifiers or the end of an IN list.
        var firstContent = RenderLiteralContent(firstContentSeed) + FirstLiteralMarker;
        var secondContent = RenderLiteralContent(secondContentSeed) + SecondLiteralMarker;

        foreach (var dialect in DialectPresets)
        {
            foreach (var renderLiteral in LiteralRenderers(dialect))
            {
                var tailLiteral = renderLiteral(TailLiteralMarker);
                var firstSql = PlaceLiteral(renderLiteral(firstContent), tailLiteral, dialect, contextSelector);
                var secondSql = PlaceLiteral(renderLiteral(secondContent), tailLiteral, dialect, contextSelector);
                var first = SqlProcessor.GetSanitizedSql(firstSql, dialect);
                var second = SqlProcessor.GetSanitizedSql(secondSql, dialect);
                var description = $"Dialect: {dialect}{Environment.NewLine}First: {firstSql}{Environment.NewLine}Second: {secondSql}";

                Assert.True(
                    string.Equals(first.SanitizedSql, second.SanitizedSql, StringComparison.Ordinal),
                    $"The sanitized SQL depends on literal content.{Environment.NewLine}{description}{Environment.NewLine}First sanitized: {first.SanitizedSql}{Environment.NewLine}Second sanitized: {second.SanitizedSql}");
                Assert.True(
                    string.Equals(first.DbQuerySummary, second.DbQuerySummary, StringComparison.Ordinal),
                    $"The summary depends on literal content.{Environment.NewLine}{description}{Environment.NewLine}First summary: {first.DbQuerySummary}{Environment.NewLine}Second summary: {second.DbQuerySummary}");

                foreach (var marker in new[] { FirstLiteralMarker, SecondLiteralMarker, TailLiteralMarker })
                {
                    Assert.False(
                        Contains(first.SanitizedSql, marker) || Contains(second.SanitizedSql, marker) ||
                        Contains(first.DbQuerySummary, marker) || Contains(second.DbQuerySummary, marker),
                        $"A literal marker was revealed.{Environment.NewLine}{description}");
                }
            }
        }
    }

    [Property(MaxTest = MaxValue, Arbitrary = [typeof(SqlGenerators)])]
    public static void GetSanitizedSql_Is_Stable_Whatever_The_Dialect_Is_Requested_First(FuzzedSql statement, int contentSeed)
    {
        // The two dialects are cached apart, so asking for one must not change the result for the other.
        var (sql, _) = statement.Render(contentSeed);

        var other = SqlProcessor.GetSanitizedSql(sql, ToDialect(!statement.UseBackslashEscapes));
        var first = SqlProcessor.GetSanitizedSql(sql, ToDialect(statement.UseBackslashEscapes));
        var again = SqlProcessor.GetSanitizedSql(sql, ToDialect(statement.UseBackslashEscapes));
        var otherAgain = SqlProcessor.GetSanitizedSql(sql, ToDialect(!statement.UseBackslashEscapes));

        Assert.Equal(first, again);
        Assert.Equal(other, otherAgain);
    }

    [Property(MaxTest = MaxValue, Arbitrary = [typeof(SqlGenerators)])]
    public static void GetSanitizedSql_Mixed_Up_SQL_Does_Not_Throw_And_Respects_The_Summary_Limit(SqlSoup soup, bool useBackslashEscapes, bool bracketsDelimitIdentifiersOnly)
    {
        SqlStatementInfo actual = default;

        var exception = Record.Exception(() => actual = SqlProcessor.GetSanitizedSql(soup.Text, ToDialect(useBackslashEscapes, bracketsDelimitIdentifiersOnly)));

        Assert.True(exception is null, $"{exception?.GetType().Name}: {exception?.Message} for {soup}");
        Assert.NotNull(actual.SanitizedSql);
        Assert.NotNull(actual.DbQuerySummary);
        Assert.True(actual.DbQuerySummary.Length <= MaxSummaryLength, $"The summary is {actual.DbQuerySummary.Length} characters for {soup}");
    }

    [Property(MaxTest = 40)]
    public static void GetSanitizedSql_Completes_In_Bounded_Time_For_Pathological_Input(byte selector, bool useBackslashEscapes, byte lengthSelector, bool bracketsDelimitIdentifiersOnly)
    {
        // Each piece is the start of something which may or may not end later, so a sanitizer which looks ahead for the end of
        // each one without remembering it has not been found takes time which grows with the square of the length.
        var piece = PathologicalPieces[selector % PathologicalPieces.Length];
        var repeats = 20_000 + (lengthSelector * 200);
        var sql = "SELECT * FROM t WHERE " + string.Concat(Enumerable.Repeat(piece, repeats));

        var stopwatch = Stopwatch.StartNew();
        var actual = SqlProcessor.GetSanitizedSql(sql, ToDialect(useBackslashEscapes, bracketsDelimitIdentifiersOnly));
        stopwatch.Stop();

        Assert.NotNull(actual.SanitizedSql);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Took {stopwatch.Elapsed} for {repeats:N0} repeats of {piece}, which is {sql.Length:N0} characters.");
    }

    private static string RenderLiteralContent(int seed)
    {
        var random = new Random(seed);
        return string.Concat(Enumerable.Range(0, 8).Select(_ => LiteralPieces[random.Next(LiteralPieces.Length)]));
    }

    private static string PlaceLiteral(string literal, string tailLiteral, SqlDialect dialect, byte contextSelector)
    {
        // In MySQL this is subtraction followed by unary negation, not the start of a comment.
        var arithmetic = dialect == SqlDialect.MySql ? "1--1, " : string.Empty;
        return (contextSelector % 4) switch
        {
            0 => $"SELECT {arithmetic}{literal} AS value FROM items WHERE id = 7",
            1 => $"SELECT * FROM items WHERE value IN ({literal}, {tailLiteral}) AND id = 7",
            2 => $"INSERT INTO items VALUES ({literal}, 7)",
            _ => $"SELECT * FROM (SELECT {literal} AS value FROM items) nested WHERE value = {tailLiteral}",
        };
    }

    private static IEnumerable<Func<string, string>> LiteralRenderers(SqlDialect dialect)
    {
        if (dialect == SqlDialect.GoogleSql)
        {
            // Quoted GoogleSQL literals cannot contain actual line breaks; triple-quoted literals can.
            yield return static content => QuoteLiteral(content, '\'', backslashEscapes: true, multiline: false);
            yield return static content => QuoteLiteral(content, '"', backslashEscapes: true, multiline: false);
            yield return static content => TripleQuotedLiteral(content, '\'');
            yield return static content => TripleQuotedLiteral(content, '"');
            yield return static content => "r" + QuoteLiteral(content, '\'', backslashEscapes: true, multiline: false);
            yield return static content => "rb" + TripleQuotedLiteral(content, '\'');
            yield return static content => "BR" + QuoteLiteral(content, '"', backslashEscapes: true, multiline: false);
            yield return static content => "b" + TripleQuotedLiteral(content, '"');
        }
        else if (dialect == SqlDialect.MySql)
        {
            yield return static content => QuoteLiteral(content, '\'', backslashEscapes: true);
            yield return static content => QuoteLiteral(content, '"', backslashEscapes: true);
        }
        else
        {
            yield return static content => QuoteLiteral(content, '\'');
        }

        if (dialect == SqlDialect.Default)
        {
            yield return static content => "E" + QuoteLiteral(content, '\'', backslashEscapes: true);
            yield return static content => "$probe$" + content.Replace("$probe$", "$ probe$") + "$probe$";
        }
        else if (dialect is SqlDialect.Oracle or SqlDialect.Firebird)
        {
            yield return static content => "q'!" + content.Replace("!'", "! '") + "!'";

            if (dialect == SqlDialect.Oracle)
            {
                yield return static content => "nq'!" + content.Replace("!'", "! '") + "!'";
            }
        }
        else if (dialect == SqlDialect.SqlServer)
        {
            yield return static content => "N" + QuoteLiteral(content, '\'');
        }
    }

    private static string QuoteLiteral(string content, char delimiter, bool backslashEscapes = false, bool multiline = true)
    {
        if (backslashEscapes)
        {
            content = content.Replace("\\", "\\\\");
        }

        if (!multiline)
        {
            content = content.Replace("\r", "\\r").Replace("\n", "\\n");
        }

        var quote = delimiter.ToString();
        return quote + content.Replace(quote, backslashEscapes ? "\\" + quote : quote + quote) + quote;
    }

    private static string TripleQuotedLiteral(string content, char delimiter)
    {
        var quotes = new string(delimiter, 3);
        var escapedQuotes = "\\" + delimiter + "\\" + delimiter + "\\" + delimiter;
        return quotes + content.Replace("\\", "\\\\").Replace(quotes, escapedQuotes) + quotes;
    }

    private static SqlDialect ToDialect(bool useBackslashEscapes, bool bracketsDelimitIdentifiersOnly = false) =>
        (useBackslashEscapes ? SqlDialect.MySql : SqlDialect.Default) |
        (bracketsDelimitIdentifiersOnly ? SqlDialect.BracketsDelimitIdentifiersOnly : SqlDialect.Default);

    private static bool Contains(string text, string value) =>
#if NET
        text.Contains(value, StringComparison.Ordinal);
#else
        text.IndexOf(value, StringComparison.Ordinal) >= 0;
#endif
}
