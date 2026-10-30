// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using FsCheck.Xunit;

namespace OpenTelemetry.Instrumentation;

/// <summary>
/// Property-based (fuzz) tests that assert nothing a statement contains in a literal or a comment, whatever
/// it is made of and whatever surrounds it, is found in the sanitized statement or in the summary of it.
/// </summary>
public static class SqlProcessorGrammarTests
{
    private const int MaxValue = 1_000;
    private const int MaxSummaryLength = 255;

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

        var actual = SqlProcessor.GetSanitizedSql(sql, statement.UseBackslashEscapes);

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

        var firstActual = SqlProcessor.GetSanitizedSql(first, statement.UseBackslashEscapes);
        var secondActual = SqlProcessor.GetSanitizedSql(second, statement.UseBackslashEscapes);

        var description = $"{Environment.NewLine}First: {first}{Environment.NewLine}Second: {second}";

        Assert.True(
            string.Equals(firstActual.SanitizedSql, secondActual.SanitizedSql, StringComparison.Ordinal),
            $"The sanitized SQL differs.{description}{Environment.NewLine}First sanitized: {firstActual.SanitizedSql}{Environment.NewLine}Second sanitized: {secondActual.SanitizedSql}");

        Assert.True(
            string.Equals(firstActual.DbQuerySummary, secondActual.DbQuerySummary, StringComparison.Ordinal),
            $"The summary differs.{description}{Environment.NewLine}First summary: {firstActual.DbQuerySummary}{Environment.NewLine}Second summary: {secondActual.DbQuerySummary}");
    }

    [Property(MaxTest = MaxValue, Arbitrary = [typeof(SqlGenerators)])]
    public static void GetSanitizedSql_Is_Stable_Whatever_The_Dialect_Is_Requested_First(FuzzedSql statement, int contentSeed)
    {
        // The two dialects are cached apart, so asking for one must not change the result for the other.
        var (sql, _) = statement.Render(contentSeed);

        var other = SqlProcessor.GetSanitizedSql(sql, !statement.UseBackslashEscapes);
        var first = SqlProcessor.GetSanitizedSql(sql, statement.UseBackslashEscapes);
        var again = SqlProcessor.GetSanitizedSql(sql, statement.UseBackslashEscapes);
        var otherAgain = SqlProcessor.GetSanitizedSql(sql, !statement.UseBackslashEscapes);

        Assert.Equal(first, again);
        Assert.Equal(other, otherAgain);
    }

    [Property(MaxTest = MaxValue, Arbitrary = [typeof(SqlGenerators)])]
    public static void GetSanitizedSql_Mixed_Up_SQL_Does_Not_Throw_And_Respects_The_Summary_Limit(SqlSoup soup, bool useBackslashEscapes)
    {
        SqlStatementInfo actual = default;

        var exception = Record.Exception(() => actual = SqlProcessor.GetSanitizedSql(soup.Text, useBackslashEscapes));

        Assert.True(exception is null, $"{exception?.GetType().Name}: {exception?.Message} for {soup}");
        Assert.NotNull(actual.SanitizedSql);
        Assert.NotNull(actual.DbQuerySummary);
        Assert.True(actual.DbQuerySummary.Length <= MaxSummaryLength, $"The summary is {actual.DbQuerySummary.Length} characters for {soup}");
    }

    [Property(MaxTest = 40)]
    public static void GetSanitizedSql_Completes_In_Bounded_Time_For_Pathological_Input(byte selector, bool useBackslashEscapes, byte lengthSelector)
    {
        // Each piece is the start of something which may or may not end later, so a sanitizer which looks ahead for the end of
        // each one without remembering it has not been found takes time which grows with the square of the length.
        var piece = PathologicalPieces[selector % PathologicalPieces.Length];
        var repeats = 20_000 + (lengthSelector * 200);
        var sql = "SELECT * FROM t WHERE " + string.Concat(Enumerable.Repeat(piece, repeats));

        var stopwatch = Stopwatch.StartNew();
        var actual = SqlProcessor.GetSanitizedSql(sql, useBackslashEscapes);
        stopwatch.Stop();

        Assert.NotNull(actual.SanitizedSql);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Took {stopwatch.Elapsed} for {repeats:N0} repeats of {piece}, which is {sql.Length:N0} characters.");
    }

    private static bool Contains(string text, string value) =>
#if NET
        text.Contains(value, StringComparison.Ordinal);
#else
        text.IndexOf(value, StringComparison.Ordinal) >= 0;
#endif
}
