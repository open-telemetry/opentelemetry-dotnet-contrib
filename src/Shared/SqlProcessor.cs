// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Instrumentation;

internal static class SqlProcessor
{
    private const int MaxSummaryLength = 255;
    private const int MaxBracketedIdentifierLength = 128;
    private const int CacheCapacity = 1000;

    // The minimum length of a statement for which the most recently sanitized statement is remembered
    // per thread while the cache is not full. Below this length looking a statement up in the cache is
    // about as cheap as maintaining the per-thread entry.
    private const int LastStatementMinLength = 128;

    private const char SanitizationPlaceholder = '?';
    private const char SpaceChar = ' ';
    private const char CommaChar = ',';
    private const char OpenSquareBracketChar = '[';
    private const char CloseSquareBracketChar = ']';
    private const char OpenParenChar = '(';
    private const char CloseParenChar = ')';
    private const char DashChar = '-';
    private const char ForwardSlashChar = '/';
    private const char SingleQuoteChar = '\'';
    private const char DoubleQuoteChar = '"';
    private const char BacktickChar = '`';
    private const char BackslashChar = '\\';
    private const char DollarChar = '$';
    private const char HashChar = '#';
    private const char AtChar = '@';
    private const char AsteriskChar = '*';
    private const char UnderscoreChar = '_';
    private const char DotChar = '.';
    private const char NewLineChar = '\n';
    private const char CarriageReturnChar = '\r';
    private const char TabChar = '\t';
    private const char UnicodePrefixChar = 'N';

    private const string ArrayKeyword = "ARRAY";

    private static readonly ConcurrentDictionary<string, SqlStatementInfo> Cache = new();
    private static readonly ConcurrentDictionary<string, SqlStatementInfo> BackslashEscapeCache = new();

    private static readonly char[] WhitespaceChars = [SpaceChar, TabChar, CarriageReturnChar, NewLineChar];
#if !NET
    private static readonly char[] LineBreakChars = [CarriageReturnChar, NewLineChar];
#endif

    // The characters which can start a construct that a ')' may legitimately appear inside
    // (a string literal, a quoted identifier or a comment), plus ')' itself. Used to find the
    // end of an IN clause.
    private static readonly char[] InClauseScanChars =
    [
        CloseParenChar,
        SingleQuoteChar,
        DoubleQuoteChar,
        BacktickChar,
        OpenSquareBracketChar,
        DollarChar,
        DashChar,
        ForwardSlashChar,
        HashChar,
    ];

#if NET
    private static readonly SearchValues<char> AsciiLetterSearchValues = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");
    private static readonly SearchValues<char> LineBreakSearchValues = SearchValues.Create("\n\r");
    private static readonly SearchValues<char> WhitespaceSearchValues = SearchValues.Create(WhitespaceChars);
    private static readonly SearchValues<char> InClauseScanSearchValues = SearchValues.Create(InClauseScanChars);
#endif

    // This is not an exhaustive list but covers the majority of common reserved SQL keywords that may follow a FROM clause.
    // This is used when determining if the previous token is a keyword in order to identify the end of a comma separated FROM clause.
    // NOTE: These are ordered so that more likely keywords appear first to shorten the comparison loop.
    private static readonly string[] FromClauseReservedKeywords = [
        "WHERE", "BY", "AS", "JOIN", "WITH", "CROSS", "HAVING", "WINDOW", "LIMIT", "OFFSET", "TABLESAMPLE", "PIVOT", "UNPIVOT"
    ];

    private static readonly int MaxFromClauseReservedKeywordLength = FromClauseReservedKeywords.Max(k => k.Length);
    private static readonly int MinFromClauseReservedKeywordLength = FromClauseReservedKeywords.Min(k => k.Length);

    // We can extend this in the future to include more keywords if needed.
    // The keywords should be ordered by frequency of use to optimize performance.
    // This only includes keywords that may be the first keyword in a statement.
    private static readonly SqlKeywordInfo[] SqlKeywords =
    [
        SqlKeywordInfo.SelectKeyword,
        SqlKeywordInfo.InsertKeyword,
        SqlKeywordInfo.UpdateKeyword,
        SqlKeywordInfo.DeleteKeyword,
        SqlKeywordInfo.CreateKeyword,
        SqlKeywordInfo.AlterKeyword,
        SqlKeywordInfo.DropKeyword,
        SqlKeywordInfo.ExecKeyword,
        SqlKeywordInfo.ExecuteKeyword,
        SqlKeywordInfo.GrantKeyword,
        SqlKeywordInfo.DenyKeyword,
        SqlKeywordInfo.TruncateKeyword,
        SqlKeywordInfo.RevokeKeyword,
        SqlKeywordInfo.BulkKeyword,
        SqlKeywordInfo.DisableKeyword,
        SqlKeywordInfo.EnableKeyword,
        SqlKeywordInfo.BackupKeyword,
        SqlKeywordInfo.RestoreKeyword,
    ];

    // This is a special case used when handling sub-queries in parentheses.
    private static readonly SqlKeywordInfo[] SelectOnlyKeywordArray =
    [
        SqlKeywordInfo.SelectKeyword,
    ];

    // Maintain our own approximate count to avoid ConcurrentDictionary.Count on hot path.
    // We only increment on successful TryAdd. This may result in a slightly oversized cache
    // under high concurrency but this is acceptable for this scenario.
    private static int approxCacheCount;
    private static int approxBackslashEscapeCacheCount;

    // The most recent statement sanitized on the current thread. Instrumentations commonly sanitize the
    // same command text instance more than once per command on the same thread (for example EF Core's
    // CommandExecuting event followed by SqlClient's WriteCommandBefore event for the same DbCommand),
    // so a reference-equality check lets the repeated call skip hashing the full text for the cache
    // lookup (or re-sanitizing it entirely once the cache is full). This retains at most one statement
    // (and its sanitized form) per thread until that thread sanitizes a different statement.
    [ThreadStatic]
    private static LastSanitizedStatement? lastSanitizedStatement;

    private enum SqlKeyword
    {
        Unknown,
        Backup,
        Bulk,
        Alter,
        Clustered,
        Connect,
        Create,
        Database,
        Delete,
        Deny,
        Disable,
        Distinct,
        Drop,
        Enable,
        Exec,
        Execute,
        Exists,
        From,
        Function,
        Grant,
        If,
        Index,
        Insert,
        Into,
        Join,
        Login,
        NonClustered,
        Not,
        On,
        Procedure,
        Restore,
        Revoke,
        Role,
        Schema,
        Select,
        Sequence,
        Statistics,
        Table,
        Trigger,
        Truncate,
        Unique,
        Union,
        Update,
        User,
        View,
    }

    /// <summary>
    /// Sanitizes a SQL statement by replacing its literal values with placeholders and computes the
    /// corresponding <c>db.query.summary</c>. Results are cached per statement and dialect.
    /// </summary>
    /// <param name="sql">The SQL statement to sanitize.</param>
    /// <param name="useBackslashEscapes">
    /// <see langword="true"/> if the source database is MySQL or MariaDB with their default SQL modes
    /// (<c>NO_BACKSLASH_ESCAPES</c> and <c>ANSI_QUOTES</c> disabled), in which case a backslash is
    /// treated as a string-literal escape character, a double-quoted (<c>"..."</c>) value is treated
    /// as a string literal rather than a quoted identifier, <c>#</c> starts a comment and block
    /// comments do not nest; otherwise <see langword="false"/>.
    /// </param>
    /// <returns>The sanitized SQL and query summary.</returns>
    public static SqlStatementInfo GetSanitizedSql(string? sql, bool useBackslashEscapes = false) =>
        sql == null
        ? default
        : useBackslashEscapes
        ? GetSanitizedSql(sql, BackslashEscapeCache, ref approxBackslashEscapeCacheCount, useBackslashEscapes: true)
        : GetSanitizedSql(sql, Cache, ref approxCacheCount, useBackslashEscapes: false);

    private static SqlStatementInfo GetSanitizedSql(
        string sql,
        ConcurrentDictionary<string, SqlStatementInfo> cache,
        ref int approxCount,
        bool useBackslashEscapes)
    {
        if (sql.Length < LastStatementMinLength && approxCount < CacheCapacity)
        {
            return GetOrAddCachedSql(sql, cache, ref approxCount, useBackslashEscapes);
        }

        // Strings are immutable, so the same instance sanitized
        // with the same dialect always produces the same result.
        var last = lastSanitizedStatement;
        if (last != null &&
            ReferenceEquals(sql, last.Sql) &&
            useBackslashEscapes == last.UseBackslashEscapes)
        {
            return last.StatementInfo;
        }

        var sqlStatementInfo = GetOrAddCachedSql(sql, cache, ref approxCount, useBackslashEscapes);

        last ??= lastSanitizedStatement = new();
        last.Sql = sql;
        last.UseBackslashEscapes = useBackslashEscapes;
        last.StatementInfo = sqlStatementInfo;

        return sqlStatementInfo;
    }

    private static SqlStatementInfo GetOrAddCachedSql(
        string sql,
        ConcurrentDictionary<string, SqlStatementInfo> cache,
        ref int approxCount,
        bool useBackslashEscapes)
    {
        if (cache.TryGetValue(sql, out var sqlStatementInfo))
        {
            return sqlStatementInfo;
        }

        sqlStatementInfo = SanitizeSql(sql, useBackslashEscapes);

        // Fast-path capacity check using our own approximate count to avoid ConcurrentDictionary.Count cost.
        if (Volatile.Read(ref approxCount) >= CacheCapacity)
        {
            return sqlStatementInfo;
        }

        // Attempt to add when under capacity. Increment our count only on successful add.
        if (cache.TryAdd(sql, sqlStatementInfo))
        {
            Interlocked.Increment(ref approxCount);
            return sqlStatementInfo;
        }

        // If another thread added meanwhile, return the cached value if available.
        return cache.TryGetValue(sql, out var existing) ? existing : sqlStatementInfo;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsUnescapedIdentifierChar(char c) =>
        char.IsLetter(c) || char.IsAsciiDigit(c) || c == UnderscoreChar || c == DotChar;

    // Whether the character can be part of a word, such as an identifier, keyword or variable name.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWordChar(char c) =>
        char.IsLetterOrDigit(c) || c is UnderscoreChar or DollarChar or HashChar or AtChar;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidTokenCharacter(char currentChar, int indexInToken, in ParseState state)
    {
        // If we are not capturing the next token as an identifier, we only accept unescaped identifier characters.
        if (!state.CaptureNextNonKeywordTokenAsIdentifier)
        {
            return IsUnescapedIdentifierChar(currentChar);
        }

        // In unescaped identifiers, periods are invalid at the start but valid in the middle (for schema-qualified names).
        return (currentChar != DotChar || indexInToken != 0) && IsUnescapedIdentifierChar(currentChar);
    }

    private static SqlStatementInfo SanitizeSql(string sql, bool useBackslashEscapes)
    {
        var sqlSpan = sql.AsSpan();

        // We use a single buffer for both sanitized SQL and DB query summary.
        // We rent a buffer twice the size of the input SQL to ensure
        // we have enough space for the sanitized SQL and summary. The summary starts
        // from the middle position of the rented buffer.
        var rentedBuffer = ArrayPool<char>.Shared.Rent(sqlSpan.Length * 2);

        var buffer = rentedBuffer.AsSpan();

        ParseState state = default;
        state.UseBackslashEscapes = useBackslashEscapes;

        // Precompute the summary buffer slice once and carry it via state to avoid repeated Span.Slice calls.
        state.SummaryBuffer = buffer.Slice(rentedBuffer.Length / 2);

        while (state.ParsePosition < sqlSpan.Length)
        {
            // Most tokens are keywords or identifiers, and a token which starts with an ASCII letter
            // cannot be a comment, a literal or whitespace, so those checks are skipped for it.
            if (!char.IsAsciiLetter(sqlSpan[state.ParsePosition]))
            {
                if (SkipComment(sqlSpan, ref state))
                {
                    continue;
                }

                if (SanitizeStringLiteral(sqlSpan, buffer, ref state) ||
                    SanitizeDollarQuotedLiteral(sqlSpan, buffer, ref state) ||
                    SanitizeHexLiteral(sqlSpan, buffer, ref state) ||
                    SanitizeNumericLiteral(sqlSpan, buffer, ref state))
                {
                    continue;
                }

                if (ParseWhitespace(sqlSpan, buffer, ref state))
                {
                    continue;
                }
            }

            // Reaching the summary length limit must not change how the statement itself is
            // parsed. Tokenization continues unchanged and only the accumulation of the summary
            // stops, which ParseNextToken handles via SummaryIsComplete.
            ParseNextToken(sqlSpan, buffer, ref state);
        }

        var summary = state.SummaryBuffer.Slice(0, state.SummaryPosition);

        // If we have exceeded the max length for the summary, find the index of the last whitespace
        // and trim the summary to that position. This avoids truncating within an operation name or target.
        if (state.SummaryPosition > MaxSummaryLength)
        {
#if NET
            var indexOfLastWhitespace = summary.Slice(0, MaxSummaryLength).LastIndexOfAny(WhitespaceSearchValues);
#else
            var indexOfLastWhitespace = summary.Slice(0, MaxSummaryLength).LastIndexOfAny(WhitespaceChars);
#endif

            summary = summary.Slice(0, indexOfLastWhitespace >= 0 ? indexOfLastWhitespace : MaxSummaryLength);
        }

        var summaryLength = summary.Length;

        // Trim trailing whitespace
        if (summaryLength > 0)
        {
            var lastChar = summary[summaryLength - 1];

            if (lastChar is SpaceChar or TabChar or NewLineChar or CarriageReturnChar)
            {
                summaryLength -= 1;
            }
        }

        var sanitizedSqlSpan = buffer.Slice(0, state.SanitizedPosition);

        // If the sanitized SQL is identical to the input SQL, we can reuse the original string instance.
        var sanitizedSql = sanitizedSqlSpan.SequenceEqual(sqlSpan) ? sql : sanitizedSqlSpan.ToString();

        var sqlStatementInfo = new SqlStatementInfo(
            sanitizedSql,
            summary.Slice(0, summaryLength).ToString());

        if (state.RentedSummaryBuffer != null)
        {
            ArrayPool<char>.Shared.Return(state.RentedSummaryBuffer);
        }

        // We don't clear the buffer as we know the content has been sanitized
        ArrayPool<char>.Shared.Return(rentedBuffer);

        return sqlStatementInfo;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendSummaryChar(char value, ref ParseState state)
    {
        EnsureSummaryCapacity(checked(state.SummaryPosition + 1), ref state);

        state.SummaryBuffer[state.SummaryPosition++] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendSummaryToken(ReadOnlySpan<char> value, ref ParseState state)
    {
        EnsureSummaryCapacity(checked(state.SummaryPosition + value.Length), ref state);

        value.CopyTo(state.SummaryBuffer.Slice(state.SummaryPosition));

        state.SummaryPosition += value.Length;
    }

    private static void EnsureSummaryCapacity(int requiredCapacity, ref ParseState state)
    {
        if (requiredCapacity <= state.SummaryBuffer.Length)
        {
            return;
        }

        var doubledCapacity = state.SummaryBuffer.Length <= (int.MaxValue / 2)
            ? state.SummaryBuffer.Length * 2
            : int.MaxValue;

        var newBuffer = ArrayPool<char>.Shared.Rent(Math.Max(requiredCapacity, doubledCapacity));

        state.SummaryBuffer.Slice(0, state.SummaryPosition).CopyTo(newBuffer);

        if (state.RentedSummaryBuffer != null)
        {
            ArrayPool<char>.Shared.Return(state.RentedSummaryBuffer);
        }

        state.RentedSummaryBuffer = newBuffer;
        state.SummaryBuffer = newBuffer.AsSpan();
    }

    private static void ParseNextToken(
        ReadOnlySpan<char> sql,
        Span<char> buffer,
        ref ParseState state)
    {
        var start = state.ParsePosition;
        var currentChar = sql[start];

        // Quick first-character filter: only attempt keyword matching if the current char is an ASCII letter.
        // NOTE: We don't check CaptureNextNonKeywordTokenAsIdentifier here because we want to capture and handle keywords
        // first, before considering identifiers.
        var mayBeKeyword = char.IsAsciiLetter(currentChar);

        if (mayBeKeyword)
        {
            var sqlLength = sql.Length;
            var remaining = sqlLength - start;

            // Determine the length of the next contiguous ascii-letter run.
            // This allows some fast paths in the comparisons below.
#if NET
            var asciiLetterLength = sql.Slice(start, remaining)
                                       .IndexOfAnyExcept(AsciiLetterSearchValues);

            if (asciiLetterLength < 0)
            {
                asciiLetterLength = remaining;
            }
#else
            var asciiLetterLength = 1;
            while (asciiLetterLength < remaining)
            {
                var ch = sql[start + asciiLetterLength];
                if (!char.IsAsciiLetter(ch))
                {
                    break;
                }

                asciiLetterLength++;
            }
#endif

            // IMPLEMENTATION NOTE: At one stage we tried checking if the length was between 2 and 12 (inclusive)
            // the range of shortest and longest keywords. This ended up being slower in practice
            // as many tokens fall into this range and it was faster to skip the length check.

            ReadOnlySpan<SqlKeywordInfo> keywordsToCheck;

            // Check if the previous character is '(', in which case, we only check against the SELECT keyword.
            // Otherwise, check if the previous keyword may be the start of a keyword chain so we can limit the
            // number of keyword comparisons we need to do by only comparing for tokens we expect to appear next.
            if (state.ParsePosition > 0 && sql[state.ParsePosition - 1] == OpenParenChar)
            {
                keywordsToCheck = SelectOnlyKeywordArray;
            }
            else
            {
                var previousKeywordInfo = state.PreviousParsedKeyword;

                keywordsToCheck = previousKeywordInfo != null && previousKeywordInfo.FollowedByKeywords.Length > 0
                    ? (ReadOnlySpan<SqlKeywordInfo>)previousKeywordInfo.FollowedByKeywords
                    : (ReadOnlySpan<SqlKeywordInfo>)SqlKeywords;
            }

            for (var i = 0; i < keywordsToCheck.Length; i++)
            {
                var potentialKeywordInfo = keywordsToCheck[i];
                var keywordSpan = potentialKeywordInfo.KeywordText.AsSpan();
                var keywordLength = keywordSpan.Length;

                // If the next token length doesn't match the keyword length, it can't be a match.
                if (asciiLetterLength != keywordLength)
                {
                    continue;
                }

                var matchedKeyword = true;

                // Compare the potential keyword in a case-insensitive manner using indices instead of slicing.
                for (var charPos = 0; charPos < keywordLength; charPos++)
                {
                    // We know that sql[start..] is all ascii letters so this comparison is safe.
                    if ((sql[start + charPos] | 0x20) != keywordSpan[charPos])
                    {
                        matchedKeyword = false;
                        break;
                    }
                }

                if (matchedKeyword)
                {
                    sql.Slice(start, keywordLength).CopyTo(buffer.Slice(state.SanitizedPosition));
                    state.SanitizedPosition += keywordLength;

                    // Potentially copy the keyword to the summary buffer.
                    if (!state.SummaryIsComplete && SqlKeywordInfo.CaptureInSummary(in state, potentialKeywordInfo))
                    {
                        if (state.SummaryPosition == 0)
                        {
                            state.FirstSummaryKeyword = potentialKeywordInfo.SqlKeyword;
                        }

                        AppendSummaryToken(sql.Slice(start, keywordLength), ref state);

                        // Add a space after the keyword. The trailing space will be trimmed later.
                        AppendSummaryChar(SpaceChar, ref state);

                        state.PreviousSummaryKeyword = potentialKeywordInfo.SqlKeyword;
                    }

                    state.CaptureNextNonKeywordTokenAsIdentifier = SqlKeywordInfo.CaptureNextTokenInSummary(in state, potentialKeywordInfo.SqlKeyword);
                    state.SanitizeNextNonKeywordToken = SqlKeywordInfo.SanitizeNextToken(in state, potentialKeywordInfo.SqlKeyword);
                    state.InFromClause = potentialKeywordInfo.SqlKeyword == SqlKeyword.From || (state.PreviousParsedKeyword?.SqlKeyword == SqlKeyword.From && state.CaptureNextNonKeywordTokenAsIdentifier);
                    state.PreviousParsedKeyword = potentialKeywordInfo;
                    state.ParsePosition += keywordLength;
                    state.PreviousTokenStartPosition = start;
                    state.PreviousTokenEndPosition = start + keywordLength;
                    state.PreviousKeywordEndPosition = start + keywordLength;

                    // No further parsing needed for this token
                    return;
                }
            }
        }

        // If we get this far, we have not matched a keyword, so we copy the token as-is.
        if (IsValidTokenCharacter(currentChar, 0, state))
        {
            // This first block handles identifiers (which start with a letter or underscore).

            // Scan the token once using indices, then bulk-copy to minimize per-char branching.
            var i = start;
            var position = -1;
            while (i < sql.Length)
            {
                position++;

                if (!IsValidTokenCharacter(sql[i], position, state))
                {
                    break;
                }

                i++;
            }

            var length = i - start;
            if (length > 0)
            {
                // Special handling: if we are in a FROM clause, check if this identifier is a reserved keyword
                // that indicates the end of the FROM clause.
                if (state.InFromClause)
                {
                    var isReservedKeyword = false;

                    // Fast check to ensure the length is within the range of known reserved keywords.
                    if (length >= MinFromClauseReservedKeywordLength && length <= MaxFromClauseReservedKeywordLength)
                    {
                        for (var k = 0; k < FromClauseReservedKeywords.Length; k++)
                        {
                            var keyword = FromClauseReservedKeywords[k];
                            if (length == keyword.Length && IsCaseInsensitiveMatch(sql, start, length, keyword))
                            {
                                isReservedKeyword = true;
                                break;
                            }
                        }
                    }

                    if (isReservedKeyword)
                    {
                        state.InFromClause = false;
                    }
                }

                if (state.SanitizeNextNonKeywordToken)
                {
                    buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
                }
                else
                {
                    sql.Slice(start, length).CopyTo(buffer.Slice(state.SanitizedPosition));
                    state.SanitizedPosition += length;
                }

                // Optionally copy to summary buffer.
                if (state.CaptureNextNonKeywordTokenAsIdentifier && !state.SummaryIsComplete)
                {
                    AppendSummaryToken(sql.Slice(start, length), ref state);

                    // Add a space after the identifier. The trailing space will be trimmed later.
                    AppendSummaryChar(SpaceChar, ref state);
                }
            }

            state.ParsePosition = i;
            state.CaptureNextNonKeywordTokenAsIdentifier = false;
            state.SanitizeNextNonKeywordToken = false;
            state.PreviousTokenStartPosition = start;
            state.PreviousTokenEndPosition = i;
        }
        else
        {
            // Quoted identifiers (e.g. [Order Details]) are copied as a single token, so that any quote
            // characters they contain are never mistaken for the start of a string literal.
            if ((currentChar is OpenSquareBracketChar or DoubleQuoteChar or BacktickChar) &&
                ParseQuotedIdentifier(sql, buffer, ref state))
            {
                return;
            }

            // If we end up here, we copy a single-character token to the sanitized buffer.

            // If we are in a FROM clause, we want to capture the next identifier following a comma or period.
            // Commas may occur when listing multiple tables in a FROM clause.
            // Periods may occur when using schema-qualified identifiers.
            state.CaptureNextNonKeywordTokenAsIdentifier = state.InFromClause && (currentChar is CommaChar or DotChar);

            buffer[state.SanitizedPosition++] = currentChar;
            state.ParsePosition++;

            // NOTE: We don't update previous token start/end positions for single-char tokens.
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsCaseInsensitiveMatch(ReadOnlySpan<char> sql, int tokenStart, int tokenLength, string reservedKeyword)
        {
            if (tokenLength != reservedKeyword.Length)
            {
                return false;
            }

            for (var charPos = 0; charPos < tokenLength; charPos++)
            {
                if ((sql[tokenStart + charPos] | 0x20) != (reservedKeyword[charPos] | 0x20))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private static bool ParseQuotedIdentifier(ReadOnlySpan<char> sql, Span<char> buffer, ref ParseState state)
    {
        var start = state.ParsePosition;
        var end = FindQuotedIdentifierEnd(sql, start, ref state);

        if (end < 0)
        {
            return false;
        }

        var length = end - start + 1;

        // Redact the whole name after LOGIN or USER (e.g. CREATE LOGIN [DOMAIN\user]), keeping its
        // delimiters. A double-quoted value which starts with a number is also redacted, because it is
        // more likely to be a string literal (e.g. in GoogleSQL, or SQL Server with QUOTED_IDENTIFIER OFF)
        // than an identifier. An empty name has nothing to redact, and the sanitized SQL must not be
        // longer than the input.
        if (length > 2 &&
            (state.SanitizeNextNonKeywordToken ||
             (sql[start] == DoubleQuoteChar && StartsWithNumber(sql.Slice(start + 1, length - 2).TrimStart()))))
        {
            buffer[state.SanitizedPosition++] = sql[start];
            buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
            buffer[state.SanitizedPosition++] = sql[end];
        }
        else
        {
            sql.Slice(start, length).CopyTo(buffer.Slice(state.SanitizedPosition));
            state.SanitizedPosition += length;

            // Only bracketed identifiers in a FROM clause are included in the summary, as they were
            // before quoted identifiers were parsed as a single token.
            if (state.InFromClause && sql[start] == OpenSquareBracketChar && !state.SummaryIsComplete)
            {
                AppendSummaryToken(sql.Slice(start, length), ref state);

                // Add a period for a schema-qualified name, otherwise a space. A trailing space will be trimmed later.
                var nextPosition = end + 1;
                AppendSummaryChar(nextPosition < sql.Length && sql[nextPosition] == DotChar ? DotChar : SpaceChar, ref state);
            }
        }

        state.ParsePosition = end + 1;
        state.CaptureNextNonKeywordTokenAsIdentifier = false;
        state.SanitizeNextNonKeywordToken = false;
        state.PreviousTokenStartPosition = start;
        state.PreviousTokenEndPosition = end + 1;

        return true;
    }

    /// <summary>
    /// Finds the end of the quoted identifier (<c>[...]</c>, <c>`...`</c>, or <c>"..."</c> unless the
    /// dialect uses it for a string literal) which starts at <paramref name="start"/>.
    /// </summary>
    /// <returns>
    /// The index of the closing delimiter, or -1 if there is no quoted identifier at <paramref name="start"/>,
    /// in which case the delimiter is parsed as a single character and what follows is parsed as usual.
    /// </returns>
    private static int FindQuotedIdentifierEnd(ReadOnlySpan<char> sql, int start, ref ParseState state) => sql[start] switch
    {
        OpenSquareBracketChar => FindBracketedIdentifierEnd(sql, start, ref state),
        BacktickChar => FindDelimitedIdentifierEnd(sql, start, BacktickChar, ref state.NoTerminatingBacktickQuotedIdentifierAhead),
        DoubleQuoteChar when !state.UseBackslashEscapes => FindDelimitedIdentifierEnd(sql, start, DoubleQuoteChar, ref state.NoTerminatingDoubleQuotedIdentifierAhead),
        _ => -1,
    };

    private static int FindDelimitedIdentifierEnd(ReadOnlySpan<char> sql, int start, char delimiter, ref bool noTerminatingIdentifierAhead)
    {
        if (noTerminatingIdentifierAhead)
        {
            return -1;
        }

        var i = start + 1;
        while (i < sql.Length)
        {
            var delimiterIndex = sql.Slice(i).IndexOf(delimiter);
            if (delimiterIndex < 0)
            {
                break;
            }

            i += delimiterIndex;

            // A doubled delimiter ("" or ``) is an escaped delimiter within the identifier.
            if (i + 1 < sql.Length && sql[i + 1] == delimiter)
            {
                i += 2;
                continue;
            }

            return i;
        }

        // Avoid scanning to the end of the input again for any later delimiter.
        noTerminatingIdentifierAhead = true;
        return -1;
    }

    private static int FindBracketedIdentifierEnd(ReadOnlySpan<char> sql, int start, ref ParseState state)
    {
        // SQL Server limits identifiers to 128 characters, so the closing bracket must be within that length.
        // Each escaped bracket (]]) is one character of the identifier, so extends the search by one.
        var searchEnd = Math.Min(sql.Length, start + MaxBracketedIdentifierLength + 2);
        var i = FindNextCloseSquareBracket(sql, start + 1, ref state);

        if (i >= searchEnd || IsArraySubscriptOrConstructor(sql, start, in state))
        {
            return -1;
        }

        while (i < searchEnd)
        {
            // A doubled closing bracket (]]) is an escaped bracket within the identifier.
            if (i + 1 < sql.Length && sql[i + 1] == CloseSquareBracketChar)
            {
                searchEnd = Math.Min(sql.Length, searchEnd + 1);
                i = FindNextCloseSquareBracket(sql, i + 2, ref state);
                continue;
            }

            // A name which is going to be redacted is always treated as an identifier, as none of it is copied.
            return (state.SanitizeNextNonKeywordToken || IsBracketedIdentifier(sql.Slice(start + 1, i - start - 1))) ? i : -1;
        }

        return -1;

        static bool IsBracketedIdentifier(ReadOnlySpan<char> content)
        {
            // Brackets can also delimit a list of values in some dialects (e.g. ['a', 'b'] or [1, 2] in
            // DuckDB, ClickHouse and GoogleSQL). An odd number of quotes cannot all be part of string
            // literals, so that content is an identifier (e.g. [Manager's Approval]), unless it starts
            // with a quote, when the bracket which ends it is inside a string literal (e.g. ['a]b', 'c']).
            // Content without quotes is an identifier unless it starts with a number (e.g. [1, 2]).
            // Anything else is parsed as SQL, which redacts any literals and at worst over-redacts an
            // identifier (e.g. [2024] or [It's Bob's]).
            content = content.TrimStart();

            if (content.IsEmpty)
            {
                return false;
            }

#if NET
            var singleQuotes = content.Count(SingleQuoteChar);
#else
            var singleQuotes = 0;
            foreach (var c in content)
            {
                if (c == SingleQuoteChar)
                {
                    singleQuotes++;
                }
            }
#endif

            return singleQuotes == 0
                ? !StartsWithNumber(content)
                : (singleQuotes & 1) == 1 && content[0] != SingleQuoteChar;
        }
    }

    /// <summary>
    /// Finds the first <c>]</c> at or after <paramref name="from"/>, or returns the length of the input if there is none.
    /// </summary>
    /// <remarks>
    /// The result is cached, so that a run of brackets which do not start an identifier (e.g. <c>[[[[</c>) does not
    /// search the same characters again. It is only reused for a position between the one it was found from and
    /// the bracket it found, so it remains correct if a caller searches from an earlier position.
    /// </remarks>
    private static int FindNextCloseSquareBracket(ReadOnlySpan<char> sql, int from, ref ParseState state)
    {
        if (from < state.CloseSquareBracketSearchStart || from > state.NextCloseSquareBracket)
        {
            var index = from < sql.Length ? sql.Slice(from).IndexOf(CloseSquareBracketChar) : -1;
            state.CloseSquareBracketSearchStart = from;
            state.NextCloseSquareBracket = index < 0 ? sql.Length : from + index;
        }

        return state.NextCloseSquareBracket;
    }

    private static bool StartsWithNumber(ReadOnlySpan<char> value)
    {
        // Whether the value starts with a numeric literal (e.g. 2024, -1 or .5).
        var i = !value.IsEmpty && (value[0] is '+' or DashChar) ? 1 : 0;

        if (i < value.Length && value[i] == DotChar)
        {
            i++;
        }

        return i < value.Length && char.IsAsciiDigit(value[i]);
    }

    private static bool IsArraySubscriptOrConstructor(ReadOnlySpan<char> sql, int start, in ParseState state)
    {
        // A '[' which directly follows an expression is an array subscript (e.g. items[1], (items)[1] or
        // "items"[1] in PostgreSQL), except after a keyword (e.g. FROM[Orders]). One which follows the
        // ARRAY keyword is an array constructor (e.g. ARRAY['a', 'b'] or ARRAY ['a', 'b']).
        if (start == 0)
        {
            return false;
        }

        var previousChar = sql[start - 1];

        if (IsWordChar(previousChar))
        {
            return state.PreviousKeywordEndPosition != start;
        }

        if (previousChar is CloseParenChar or CloseSquareBracketChar or DoubleQuoteChar or BacktickChar)
        {
            return true;
        }

        var end = start;
        while (end > 0 && sql[end - 1] is SpaceChar or TabChar or CarriageReturnChar or NewLineChar)
        {
            end--;
        }

        var keywordStart = end - ArrayKeyword.Length;
        if (end == start || keywordStart < 0 || (keywordStart > 0 && IsWordChar(sql[keywordStart - 1])))
        {
            return false;
        }

        for (var i = 0; i < ArrayKeyword.Length; i++)
        {
            if ((sql[keywordStart + i] | 0x20) != (ArrayKeyword[i] | 0x20))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ParseWhitespace(ReadOnlySpan<char> sql, Span<char> buffer, ref ParseState state)
    {
        var start = state.ParsePosition;
#if NET
        var remaining = sql.Slice(start);
        var length = remaining.IndexOfAnyExcept(WhitespaceSearchValues);
        if (length == 0)
        {
            return false;
        }

        if (length < 0)
        {
            length = remaining.Length;
        }
#else
        var i = start;
        while (i < sql.Length)
        {
            var currentChar = sql[i];
            if (currentChar is not (SpaceChar or TabChar or CarriageReturnChar or NewLineChar))
            {
                break;
            }

            i++;
        }

        var length = i - start;
        if (length == 0)
        {
            return false;
        }
#endif

        sql.Slice(start, length).CopyTo(buffer.Slice(state.SanitizedPosition));
        state.SanitizedPosition += length;
        state.ParsePosition = start + length;
        return true;
    }

    private static bool SkipComment(ReadOnlySpan<char> sql, ref ParseState state)
    {
        var commentEnd = FindCommentEnd(sql, state.ParsePosition, state.UseBackslashEscapes);

        if (commentEnd < 0)
        {
            return false;
        }

        state.ParsePosition = commentEnd;
        return true;
    }

    /// <summary>
    /// Finds the end of the comment which starts at <paramref name="start"/>.
    /// </summary>
    /// <returns>
    /// The index after a multi-line comment, the index of the line break which ends a single-line comment
    /// (so that it is copied as whitespace), the length of the input if the comment is not terminated, or
    /// -1 if there is no comment at <paramref name="start"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindCommentEnd(ReadOnlySpan<char> sql, int start, bool useBackslashEscapes)
    {
        var ch = sql[start];
        var next = start + 1;

        // Scan past multi-line comment
        if (ch == ForwardSlashChar && next < sql.Length && sql[next] == AsteriskChar)
        {
            // SQL Server and PostgreSQL allow block comments to be nested, so a comment such as
            // /* /* */ don't */ only ends at the second "*/". MySQL/MariaDB do not.
            return FindMultiLineCommentEnd(sql, start, allowNesting: !useBackslashEscapes);
        }

        // Scan past single-line comment. MySQL/MariaDB also start one with '#', which is otherwise
        // part of an identifier in the other dialects (e.g. a SQL Server #temp table).
        if ((ch == DashChar && next < sql.Length && sql[next] == DashChar) || (ch == HashChar && useBackslashEscapes))
        {
#if NET
            var lineBreakIndex = sql.Slice(next).IndexOfAny(LineBreakSearchValues);
#else
            var lineBreakIndex = sql.Slice(next).IndexOfAny(LineBreakChars);
#endif

            // Position at the newline so ParseWhitespace can copy it
            return lineBreakIndex < 0 ? sql.Length : next + lineBreakIndex;
        }

        return -1;
    }

    private static int FindMultiLineCommentEnd(ReadOnlySpan<char> sql, int start, bool allowNesting)
    {
        var depth = 1;
        var i = start + 2;

        while (i < sql.Length)
        {
            var index = allowNesting
                ? sql.Slice(i).IndexOfAny(AsteriskChar, ForwardSlashChar)
                : sql.Slice(i).IndexOf(AsteriskChar);

            if (index < 0)
            {
                break;
            }

            i += index;

            var next = i + 1;
            if (next < sql.Length)
            {
                if (sql[i] == AsteriskChar && sql[next] == ForwardSlashChar)
                {
                    if (--depth == 0)
                    {
                        return i + 2;
                    }

                    i += 2;
                    continue;
                }

                if (sql[i] == ForwardSlashChar && sql[next] == AsteriskChar)
                {
                    depth++;
                    i += 2;
                    continue;
                }
            }

            i++;
        }

        // Unterminated comment, consume to end
        return sql.Length;
    }

    private static bool SanitizeStringLiteral(ReadOnlySpan<char> sql, Span<char> buffer, ref ParseState state)
    {
        var currentChar = sql[state.ParsePosition];
        if (currentChar == SingleQuoteChar)
        {
            return TrySanitizeLiteralsForInClause(sql, buffer, ref state, state.ParsePosition) ||
                   SanitizeQuotedLiteral(sql, buffer, ref state, SingleQuoteChar);
        }

        // MySQL/MariaDB (the same dialects for which useBackslashEscapes is set) also treat a
        // double-quoted value as a string literal unless the ANSI_QUOTES sql_mode is enabled.
        // The other dialects supported here use '"' exclusively to delimit a quoted identifier
        // (e.g. "columnName"), so this must remain gated on the dialect flag: otherwise a quoted
        // identifier in those dialects would be misidentified as a literal and redacted, and
        // conversely a MySQL/MariaDB double-quoted literal would leak into the sanitized SQL
        // verbatim (it is never recognized as an identifier or literal by any other check here).
        return currentChar == DoubleQuoteChar &&
               state.UseBackslashEscapes &&
               SanitizeQuotedLiteral(sql, buffer, ref state, DoubleQuoteChar);
    }

    private static bool SanitizeQuotedLiteral(ReadOnlySpan<char> sql, Span<char> buffer, ref ParseState state, char delimiter)
    {
        var prefixLength = 0;
        var literalEnd = delimiter == SingleQuoteChar
            ? FindStringLiteralEnd(sql, state.ParsePosition, state.UseBackslashEscapes, out prefixLength)
            : FindQuotedLiteralEnd(sql, state.ParsePosition, delimiter, state.UseBackslashEscapes);

        if (literalEnd < 0)
        {
            state.ParsePosition = sql.Length;
        }
        else
        {
            // Skip a prefix of the literal (e.g. the N of the Unicode literal N'foo'), which has
            // already been copied as a token, by overwriting it instead.
            state.SanitizedPosition -= prefixLength;
            state.ParsePosition = literalEnd + 1;
        }

        buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
        return true;
    }

    /// <summary>
    /// Finds the end of the single-quoted string literal whose opening quote is at <paramref name="quotePosition"/>.
    /// </summary>
    /// <param name="sql">The SQL statement.</param>
    /// <param name="quotePosition">The position of the opening quote.</param>
    /// <param name="useBackslashEscapes">Whether the dialect treats a backslash as an escape character.</param>
    /// <param name="prefixLength">
    /// The number of characters which precede the opening quote and are part of the literal
    /// (e.g. the <c>N</c> of the Unicode literal <c>N'foo'</c>).
    /// </param>
    /// <returns>The index of the closing quote, or -1 if the literal is not terminated.</returns>
    private static int FindStringLiteralEnd(ReadOnlySpan<char> sql, int quotePosition, bool useBackslashEscapes, out int prefixLength)
    {
        prefixLength = 0;

        if (quotePosition == 0)
        {
            return FindQuotedLiteralEnd(sql, quotePosition, SingleQuoteChar, useBackslashEscapes);
        }

        var previousChar = sql[quotePosition - 1];

        // These prefixes are only recognized when they are a separate token (so not the end of
        // ELSE'a'), and not for MySQL/MariaDB, which do not support them.
        if (!useBackslashEscapes)
        {
            // A PostgreSQL escape string (e.g. E'it\'s'), in which a backslash escapes a quote.
            if (previousChar is 'E' or 'e' && IsSeparateToken(sql, quotePosition - 1))
            {
                prefixLength = 1;
                return FindQuotedLiteralEnd(sql, quotePosition, SingleQuoteChar, useBackslashEscapes: true);
            }

            // An Oracle alternative quoting literal (e.g. q'[it's]', or nq'[it's]' for a national
            // character literal), in which quotes are not escaped. The delimiter which follows the
            // opening quote can be any character other than whitespace, and the literal ends at the
            // first closing delimiter (']' for '[', '}' for '{', ')' for '(' and '>' for '<',
            // otherwise the same character) which is followed by a quote.
            if (previousChar is 'q' or 'Q' &&
                quotePosition + 1 < sql.Length &&
                sql[quotePosition + 1] is not (SpaceChar or TabChar or CarriageReturnChar or NewLineChar or SingleQuoteChar))
            {
                var prefixStart = quotePosition - 1;
                if (prefixStart > 0 && sql[prefixStart - 1] is 'n' or 'N')
                {
                    prefixStart--;
                }

                if (IsSeparateToken(sql, prefixStart))
                {
                    prefixLength = quotePosition - prefixStart;
                    return FindAlternativeQuotedLiteralEnd(sql, quotePosition);
                }
            }
        }

        if (previousChar is UnicodePrefixChar)
        {
            prefixLength = 1;
        }

        return FindQuotedLiteralEnd(sql, quotePosition, SingleQuoteChar, useBackslashEscapes);

        static bool IsSeparateToken(ReadOnlySpan<char> sql, int position)
        {
            return position == 0 || !IsWordChar(sql[position - 1]);
        }
    }

    private static int FindAlternativeQuotedLiteralEnd(ReadOnlySpan<char> sql, int quotePosition)
    {
        var openingDelimiter = sql[quotePosition + 1];
        var closingDelimiter = openingDelimiter switch
        {
            OpenSquareBracketChar => CloseSquareBracketChar,
            OpenParenChar => CloseParenChar,
            '{' => '}',
            '<' => '>',
            _ => openingDelimiter,
        };

        var i = quotePosition + 2;
        while (i < sql.Length)
        {
            var delimiterIndex = sql.Slice(i).IndexOf(closingDelimiter);
            if (delimiterIndex < 0)
            {
                break;
            }

            i += delimiterIndex + 1;

            if (i < sql.Length && sql[i] == SingleQuoteChar)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Finds the end of the quoted literal whose opening delimiter is at <paramref name="quotePosition"/>.
    /// </summary>
    /// <returns>The index of the closing delimiter, or -1 if the literal is not terminated.</returns>
    private static int FindQuotedLiteralEnd(ReadOnlySpan<char> sql, int quotePosition, char delimiter, bool useBackslashEscapes)
    {
        var searchPos = quotePosition + 1;
        while (searchPos < sql.Length)
        {
            var quoteIndex = sql.Slice(searchPos).IndexOf(delimiter);
            if (quoteIndex < 0)
            {
                break;
            }

            searchPos += quoteIndex;

            // Skip a backslash-escaped delimiter (\' or \"). MySQL/MariaDB (with the default
            // NO_BACKSLASH_ESCAPES disabled) treat a backslash as a string escape character in
            // both single- and double-quoted literals, as does PostgreSQL in an escape string
            // (E'...'), so a delimiter preceded by an odd number of backslashes does not terminate
            // the literal. Without this a value such as 'a\'secret' would be incorrectly parsed and
            // the trailing "secret" copied into the sanitized SQL verbatim. This is gated on the
            // dialect because '\' is not an escape in the other engines, where treating it as one
            // would instead cause a doubled-quote-escaped literal to be incorrectly parsed.
            if (useBackslashEscapes && IsBackslashEscaped(sql, searchPos, quotePosition))
            {
                searchPos += 1;
                continue;
            }

            if (searchPos + 1 < sql.Length && sql[searchPos + 1] == delimiter)
            {
                // Skip escaped delimiter ('' or "")
                searchPos += 2;
                continue;
            }

            // Found terminating delimiter
            return searchPos;
        }

        return -1;
    }

    private static bool IsBackslashEscaped(ReadOnlySpan<char> sql, int quoteIndex, int literalStart)
    {
        var backslashes = 0;
        for (var i = quoteIndex - 1; i > literalStart && sql[i] == BackslashChar; i--)
        {
            backslashes++;
        }

        return (backslashes & 1) == 1;
    }

    private static bool SanitizeDollarQuotedLiteral(ReadOnlySpan<char> sql, Span<char> buffer, ref ParseState state)
    {
        if (sql[state.ParsePosition] != DollarChar ||
            !TryFindDollarQuotedLiteralEnd(sql, state.ParsePosition, out var literalEnd))
        {
            return false;
        }

        state.ParsePosition = literalEnd;
        buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
        return true;
    }

    /// <summary>
    /// Finds the end of the dollar-quoted string literal which starts at <paramref name="start"/>, if any.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if a dollar-quoted string literal starts at <paramref name="start"/>, in which
    /// case <paramref name="literalEnd"/> is the index after it, or the length of the input if it is not
    /// terminated; otherwise <see langword="false"/>.
    /// </returns>
    private static bool TryFindDollarQuotedLiteralEnd(ReadOnlySpan<char> sql, int start, out int literalEnd)
    {
        // PostgreSQL dollar-quoted string: $tag$...$tag$ (the tag is optional, so $$...$$ is valid).
        // The body between the delimiters is a literal with no escaping, so it must be redacted.
        // This syntax is unambiguous across the SQL dialects handled here, so it is safe to apply.
        literalEnd = -1;

        if (sql[start] != DollarChar)
        {
            return false;
        }

        // Parse the opening delimiter: a dollar sign, an optional tag, then a closing dollar sign.
        var tagEnd = start + 1;
        if (tagEnd < sql.Length && sql[tagEnd] != DollarChar)
        {
            if (!IsDollarQuoteTagStartChar(sql[tagEnd]))
            {
                return false;
            }

            tagEnd++;
            while (tagEnd < sql.Length && IsDollarQuoteTagChar(sql[tagEnd]))
            {
                tagEnd++;
            }
        }

        if (tagEnd >= sql.Length || sql[tagEnd] != DollarChar)
        {
            return false;
        }

        var delimiter = sql.Slice(start, tagEnd - start + 1);
        var bodyStart = tagEnd + 1;

        var closeOffset = sql.Slice(bodyStart).IndexOf(delimiter);

        literalEnd = closeOffset < 0 ? sql.Length : bodyStart + closeOffset + delimiter.Length;
        return true;

        static bool IsDollarQuoteTagStartChar(char c)
        {
            return char.IsAsciiLetter(c) || c == UnderscoreChar;
        }

        static bool IsDollarQuoteTagChar(char c)
        {
            return char.IsAsciiLetterOrDigit(c) || c == UnderscoreChar;
        }
    }

    private static bool SanitizeHexLiteral(ReadOnlySpan<char> sql, Span<char> buffer, ref ParseState state)
    {
        var i = state.ParsePosition;
        var ch = sql[i];
        var length = sql.Length;
        var iPlusOne = i + 1;

        if (ch == '0' && iPlusOne < length && (sql[iPlusOne] == 'x' || sql[iPlusOne] == 'X'))
        {
            if (TrySanitizeLiteralsForInClause(sql, buffer, ref state, i))
            {
                return true;
            }

            for (i += 2; i < length; ++i)
            {
                ch = sql[i];
                if (char.IsAsciiHexDigit(ch))
                {
                    continue;
                }

                i -= 1;
                break;
            }

            state.ParsePosition = ++i;

            buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
            return true;
        }

        return false;
    }

    private static bool SanitizeNumericLiteral(ReadOnlySpan<char> sql, Span<char> buffer, ref ParseState state)
    {
        var i = state.ParsePosition;
        var currentChar = sql[i];
        var length = sql.Length;
        var iPlusOne = i + 1;

        // Scan past leading sign
        if ((currentChar == '-' || currentChar == '+') && iPlusOne < length && (char.IsAsciiDigit(sql[iPlusOne]) || sql[iPlusOne] == DotChar))
        {
            i += 1;
            iPlusOne = i + 1;
            currentChar = sql[i];
        }

        // Scan past leading decimal point
        var periodMatched = false;
        if (currentChar == '.' && iPlusOne < length && char.IsAsciiDigit(sql[iPlusOne]))
        {
            periodMatched = true;
            i += 1;
            currentChar = sql[i];
        }

        if (char.IsAsciiDigit(currentChar))
        {
            if (TrySanitizeLiteralsForInClause(sql, buffer, ref state, i))
            {
                return true;
            }

            var exponentMatched = false;
            for (i += 1; i < length; ++i)
            {
                currentChar = sql[i];
                if (char.IsAsciiDigit(currentChar))
                {
                    continue;
                }

                if (!periodMatched && currentChar == '.')
                {
                    periodMatched = true;
                    continue;
                }

                if (!exponentMatched && (currentChar == 'e' || currentChar == 'E'))
                {
                    // Scan past sign in exponent
                    if (i + 1 < length && (sql[i + 1] == '-' || sql[i + 1] == '+'))
                    {
                        i += 1;
                    }

                    exponentMatched = true;
                    continue;
                }

                i -= 1;
                break;
            }

            state.ParsePosition = ++i;

            buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
            return true;
        }

        return false;
    }

    private static bool TrySanitizeLiteralsForInClause(ReadOnlySpan<char> sql, Span<char> buffer, ref ParseState state, int parsePosition)
    {
        // Special case: We may be in an IN clause with a list of literals.
        // If the previously sanitized character was '(' and the previous token was "IN", we can simplify the sanitization.
        // In this case, we fast-path to the closing parenthesis and replace the entire contents with a single '?'.

        if (state.SanitizedPosition > 0 && buffer[state.SanitizedPosition - 1] == OpenParenChar
            && state.PreviousTokenEndPosition - state.PreviousTokenStartPosition == 2)
        {
            // Check the token is actually "IN" (case-insensitive) to avoid false positives.
            var firstChar = sql[state.PreviousTokenStartPosition];
            var secondChar = sql[state.PreviousTokenStartPosition + 1];

            if (!((firstChar == 'i' || firstChar == 'I') && (secondChar == 'n' || secondChar == 'N')))
            {
                return false;
            }

            // The closing parenthesis has to be located with a scan which is aware of literals,
            // quoted identifiers and comments. A plain IndexOf(')') can match a ')' inside a value
            // (for example "IN ('a)b', 'secret')"), which would leave the parser positioned in the
            // middle of that literal. Every subsequent quote would then be mismatched and the
            // remaining values would be copied into the sanitized SQL verbatim instead of being replaced.
            if (TryFindEndOfInClause(sql, parsePosition, in state, out var closeParenIndex))
            {
                state.ParsePosition = closeParenIndex;
                buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds the index of the parenthesis which closes an <c>IN (</c> clause, ignoring any
    /// parenthesis which appears inside a string literal, a quoted identifier or a comment.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if a closing parenthesis was found, in which case
    /// <paramref name="closeParenIndex"/> is its index in <paramref name="sql"/>.
    /// <see langword="false"/> if the clause is not terminated, in which case the caller
    /// falls back to sanitizing each value individually.
    /// </returns>
    private static bool TryFindEndOfInClause(ReadOnlySpan<char> sql, int start, in ParseState state, out int closeParenIndex)
    {
        // The list is scanned ahead of the parser, which continues from its start if it is not
        // terminated, so anything the scan records about later positions (such as there being no
        // closing delimiter ahead) is kept in a copy of the state rather than the parser's own.
        var scanState = state;
        var length = sql.Length;
        var i = start;

        while (i < length)
        {
#if NET
            var offset = sql.Slice(i).IndexOfAny(InClauseScanSearchValues);
#else
            var offset = sql.Slice(i).IndexOfAny(InClauseScanChars);
#endif

            if (offset < 0)
            {
                break;
            }

            i += offset;

            // Each construct is skipped using the same rules as the main parser, and a construct
            // which is not terminated skips to the end of the input, so that no ')' within it is used.
            int end;
            switch (sql[i])
            {
                case CloseParenChar:
                    closeParenIndex = i;
                    return true;

                case SingleQuoteChar:
                    end = FindStringLiteralEnd(sql, i, state.UseBackslashEscapes, out _);
                    i = end < 0 ? length : end + 1;
                    break;

                case DoubleQuoteChar when state.UseBackslashEscapes:
                    end = FindQuotedLiteralEnd(sql, i, DoubleQuoteChar, useBackslashEscapes: true);
                    i = end < 0 ? length : end + 1;
                    break;

                case DoubleQuoteChar:
                case BacktickChar:
                case OpenSquareBracketChar:
                    // A delimiter which does not start a quoted identifier is parsed as a single character.
                    end = FindQuotedIdentifierEnd(sql, i, ref scanState);
                    i = end < 0 ? i + 1 : end + 1;
                    break;

                case DollarChar:
                    i = TryFindDollarQuotedLiteralEnd(sql, i, out end) ? end : i + 1;
                    break;

                // DashChar, ForwardSlashChar or HashChar
                default:
                    end = FindCommentEnd(sql, i, state.UseBackslashEscapes);
                    i = end < 0 ? i + 1 : end;
                    break;
            }
        }

        closeParenIndex = -1;
        return false;
    }

    private ref struct ParseState
    {
        // ParseState intentionally uses public fields (not properties):
        // - This is a ref struct that lives on the stack and is passed by ref through hot paths.
        // - Fields avoid property accessor calls in tight loops and yield smaller/faster code after inlining.
        // - Grouping Span<> and larger struct fields first helps layout and may reduce padding.
        // - Keeping the struct simple and flat minimizes stack pressure and lets the JIT keep values in registers.

        // Stored in state to avoid slicing repeatedly.
        public Span<char> SummaryBuffer;
        public char[]? RentedSummaryBuffer;

        /// <summary>
        /// Will be set if a keyword has been matched by the parser.
        /// Not all keywords are necessarily matched.
        /// </summary>
        public SqlKeywordInfo? PreviousParsedKeyword; // 8 bytes (reference type)

        public SqlKeyword FirstSummaryKeyword; // 4 bytes (enum, underlying int)
        public SqlKeyword PreviousSummaryKeyword; // 4 bytes (enum, underlying int)

        // These track the current parse position in the input SQL and the current write position
        // for the sanitized SQL and summary buffers.
        public int ParsePosition; // 4 bytes
        public int SanitizedPosition; // 4 bytes
        public int SummaryPosition; // 4 bytes

        // These track the start and end position of the previous (non-literal) token parsed.
        public int PreviousTokenStartPosition; // 4 bytes
        public int PreviousTokenEndPosition; // 4 bytes

        // This tracks the end position of the previous keyword matched by the parser.
        public int PreviousKeywordEndPosition; // 4 bytes

        // The first ']' found at or after CloseSquareBracketSearchStart, or the length of the input if there is none.
        public int CloseSquareBracketSearchStart; // 4 bytes
        public int NextCloseSquareBracket; // 4 bytes

        // NOTE: If the number of bool fields increases significantly, consider combining into a bitfield.

        public bool CaptureNextNonKeywordTokenAsIdentifier; // 1 byte

        public bool SanitizeNextNonKeywordToken; // 1 byte

        /// <summary>
        /// Whether the source dialect treats a backslash as a string-literal escape character
        /// (MySQL/MariaDB). Controls whether <c>\'</c> is recognized as an escaped quote, and the
        /// other dialect differences described by <see cref="GetSanitizedSql(string?, bool)"/>.
        /// </summary>
        public bool UseBackslashEscapes; // 1 byte

        /// <summary>
        /// Used to avoid repeatedly scanning to the end of malformed SQL after finding an unterminated double-quoted identifier.
        /// </summary>
        public bool NoTerminatingDoubleQuotedIdentifierAhead; // 1 byte

        /// <summary>
        /// Used to avoid repeatedly scanning to the end of malformed SQL after finding an unterminated backtick-quoted identifier.
        /// </summary>
        public bool NoTerminatingBacktickQuotedIdentifierAhead; // 1 byte

        /// <summary>
        /// Used to track if we are in a FROM clause for special handling of comma-separated table lists.
        /// When set to <c>true</c>, subsequent unmatched tokens will be compared against reserved keywords.
        /// As soon as we match a reserved keyword, we exit the FROM clause state.
        /// </summary>
        public bool InFromClause; // 1 byte

        /// <summary>
        /// Gets a value indicating whether the summary has reached its maximum length, after which
        /// nothing further is appended to it.
        /// </summary>
        public readonly bool SummaryIsComplete => this.SummaryPosition >= MaxSummaryLength;
    }

    private sealed class LastSanitizedStatement
    {
        public string? Sql;
        public bool UseBackslashEscapes;
        public SqlStatementInfo StatementInfo;
    }

    private sealed class SqlKeywordInfo
    {
        // Used on keywords that are only included in the summary if they are the first keyword in the statement.
        private static readonly SqlKeyword[] Unknown = [SqlKeyword.Unknown];

        private static readonly SqlKeyword[] DdlKeywords =
        [
            SqlKeyword.Create,
            SqlKeyword.Drop,
            SqlKeyword.Alter,
        ];

        private readonly SqlKeyword[]? captureInSummaryWhenPrevious;

        static SqlKeywordInfo()
        {
            // Phase 1: Create all static instances.
            // We will compare the SQL we are parsing in lowercase, so we store these in lowercase also.
            AlterKeyword = new("alter", SqlKeyword.Alter, Unknown);
            BackupKeyword = new("backup", SqlKeyword.Backup, Unknown);
            BulkKeyword = new("bulk", SqlKeyword.Bulk, Unknown);
            ConnectKeyword = new("connect", SqlKeyword.Connect, Unknown);
            CreateKeyword = new("create", SqlKeyword.Create, Unknown);
            DatabaseKeyword = new("database", SqlKeyword.Database, [.. DdlKeywords, SqlKeyword.Backup, SqlKeyword.Restore]);
            DeleteKeyword = new("delete", SqlKeyword.Delete, Unknown);
            DenyKeyword = new("deny", SqlKeyword.Deny, Unknown);
            DisableKeyword = new("disable", SqlKeyword.Disable, Unknown);
            DropKeyword = new("drop", SqlKeyword.Drop, Unknown);
            EnableKeyword = new("enable", SqlKeyword.Enable, Unknown);
            ExecKeyword = new("exec", SqlKeyword.Exec, Unknown);
            ExecuteKeyword = new("execute", SqlKeyword.Execute, Unknown);
            ExistsKeyword = new("exists", SqlKeyword.Exists);
            FromKeyword = new("from", SqlKeyword.From);
            FunctionKeyword = new("function", SqlKeyword.Function, DdlKeywords);
            GrantKeyword = new("grant", SqlKeyword.Grant, Unknown);
            IfKeyword = new("if", SqlKeyword.If);
            IndexKeyword = new("index", SqlKeyword.Index, [.. DdlKeywords, SqlKeyword.Unique, SqlKeyword.Clustered, SqlKeyword.NonClustered]);
            InsertKeyword = new("insert", SqlKeyword.Insert, [SqlKeyword.Unknown, SqlKeyword.Bulk]);
            IntoKeyword = new("into", SqlKeyword.Into);
            JoinKeyword = new("join", SqlKeyword.Join);
            LoginKeyword = new("login", SqlKeyword.Login, DdlKeywords);
            NotKeyword = new("not", SqlKeyword.Not);
            OnKeyword = new("on", SqlKeyword.On);
            ProcedureKeyword = new("procedure", SqlKeyword.Procedure, DdlKeywords);
            RestoreKeyword = new("restore", SqlKeyword.Restore, Unknown);
            RevokeKeyword = new("revoke", SqlKeyword.Revoke, Unknown);
            RoleKeyword = new("role", SqlKeyword.Role, DdlKeywords);
            SchemaKeyword = new("schema", SqlKeyword.Schema, DdlKeywords);
            SelectKeyword = new("select", SqlKeyword.Select, [SqlKeyword.Select, SqlKeyword.Unknown]);
            SequenceKeyword = new("sequence", SqlKeyword.Sequence, DdlKeywords);
            StatisticsKeyword = new("statistics", SqlKeyword.Statistics, [SqlKeyword.Update]);
            TableKeyword = new("table", SqlKeyword.Table, [.. DdlKeywords, SqlKeyword.Truncate]);
            TriggerKeyword = new("trigger", SqlKeyword.Trigger, [.. DdlKeywords, SqlKeyword.Enable, SqlKeyword.Disable]);
            TruncateKeyword = new("truncate", SqlKeyword.Truncate, Unknown);
            UnionKeyword = new("union", SqlKeyword.Union);
            UnknownKeyword = new(string.Empty, SqlKeyword.Unknown);
            UpdateKeyword = new("update", SqlKeyword.Update, Unknown);
            UserKeyword = new("user", SqlKeyword.User, DdlKeywords);
            ViewKeyword = new("view", SqlKeyword.View, DdlKeywords);

            // Phase 2: Build arrays that depend on instances
            // NOTE: This array is sorted by an estimation of the most likely
            // keywords first to optimise the comparison loop.
            DdlSubKeywords =
            [
                TableKeyword,
                IndexKeyword,
                ViewKeyword,
                ProcedureKeyword,
                TriggerKeyword,
                DatabaseKeyword,
                LoginKeyword,
                UserKeyword,
                RoleKeyword,
                SequenceKeyword,
                SchemaKeyword,
                FunctionKeyword,
            ];

            // Phase 3: Wire follow relationships
            AlterKeyword.FollowedByKeywords = DdlSubKeywords;
            BackupKeyword.FollowedByKeywords = [DatabaseKeyword];
            BulkKeyword.FollowedByKeywords = [InsertKeyword];
            CreateKeyword.FollowedByKeywords = DdlSubKeywords;
            DatabaseKeyword.FollowedByKeywords = [IfKeyword];
            DenyKeyword.FollowedByKeywords = [ConnectKeyword];
            DisableKeyword.FollowedByKeywords = [TriggerKeyword];
            DropKeyword.FollowedByKeywords = DdlSubKeywords;
            EnableKeyword.FollowedByKeywords = [TriggerKeyword];
            FromKeyword.FollowedByKeywords = [JoinKeyword, UnionKeyword];
            FunctionKeyword.FollowedByKeywords = [IfKeyword];
            GrantKeyword.FollowedByKeywords = [ConnectKeyword];
            IfKeyword.FollowedByKeywords = [NotKeyword, ExistsKeyword];
            IndexKeyword.FollowedByKeywords = [OnKeyword, IfKeyword];
            InsertKeyword.FollowedByKeywords = [IntoKeyword];
            JoinKeyword.FollowedByKeywords = [OnKeyword];
            LoginKeyword.FollowedByKeywords = [IfKeyword];
            NotKeyword.FollowedByKeywords = [ExistsKeyword];
            OnKeyword.FollowedByKeywords = [JoinKeyword];
            ProcedureKeyword.FollowedByKeywords = [IfKeyword];
            RestoreKeyword.FollowedByKeywords = [DatabaseKeyword];
            RevokeKeyword.FollowedByKeywords = [ConnectKeyword];
            RoleKeyword.FollowedByKeywords = [IfKeyword];
            SchemaKeyword.FollowedByKeywords = [IfKeyword, UnionKeyword];
            SelectKeyword.FollowedByKeywords = [FromKeyword];
            SequenceKeyword.FollowedByKeywords = [IfKeyword];
            TableKeyword.FollowedByKeywords = [IfKeyword];
            TriggerKeyword.FollowedByKeywords = [IfKeyword];
            TruncateKeyword.FollowedByKeywords = [TableKeyword];
            UnionKeyword.FollowedByKeywords = [SelectKeyword];
            UpdateKeyword.FollowedByKeywords = [StatisticsKeyword];
            UserKeyword.FollowedByKeywords = [IfKeyword];
            ViewKeyword.FollowedByKeywords = [IfKeyword];
        }

        private SqlKeywordInfo(
            string keyword,
            SqlKeyword sqlKeyword,
            SqlKeyword[]? captureInSummaryWhenPrevious = null)
        {
            this.KeywordText = keyword;
            this.SqlKeyword = sqlKeyword;
            this.captureInSummaryWhenPrevious = captureInSummaryWhenPrevious;
            this.FollowedByKeywords = [];
        }

        public static SqlKeywordInfo AlterKeyword { get; }

        public static SqlKeywordInfo BackupKeyword { get; }

        public static SqlKeywordInfo BulkKeyword { get; }

        public static SqlKeywordInfo ConnectKeyword { get; }

        public static SqlKeywordInfo CreateKeyword { get; }

        public static SqlKeywordInfo DatabaseKeyword { get; }

        public static SqlKeywordInfo DeleteKeyword { get; }

        public static SqlKeywordInfo DenyKeyword { get; }

        public static SqlKeywordInfo DisableKeyword { get; }

        public static SqlKeywordInfo DropKeyword { get; }

        public static SqlKeywordInfo EnableKeyword { get; }

        public static SqlKeywordInfo ExecKeyword { get; }

        public static SqlKeywordInfo ExecuteKeyword { get; }

        public static SqlKeywordInfo ExistsKeyword { get; }

        public static SqlKeywordInfo FromKeyword { get; }

        public static SqlKeywordInfo FunctionKeyword { get; }

        public static SqlKeywordInfo GrantKeyword { get; }

        public static SqlKeywordInfo IfKeyword { get; }

        public static SqlKeywordInfo IndexKeyword { get; }

        public static SqlKeywordInfo InsertKeyword { get; }

        public static SqlKeywordInfo IntoKeyword { get; }

        public static SqlKeywordInfo JoinKeyword { get; }

        public static SqlKeywordInfo LoginKeyword { get; }

        public static SqlKeywordInfo NotKeyword { get; }

        public static SqlKeywordInfo OnKeyword { get; }

        public static SqlKeywordInfo ProcedureKeyword { get; }

        public static SqlKeywordInfo RestoreKeyword { get; }

        public static SqlKeywordInfo RevokeKeyword { get; }

        public static SqlKeywordInfo RoleKeyword { get; }

        public static SqlKeywordInfo SchemaKeyword { get; }

        public static SqlKeywordInfo SelectKeyword { get; }

        public static SqlKeywordInfo SequenceKeyword { get; }

        public static SqlKeywordInfo StatisticsKeyword { get; }

        public static SqlKeywordInfo TableKeyword { get; }

        public static SqlKeywordInfo TriggerKeyword { get; }

        public static SqlKeywordInfo TruncateKeyword { get; }

        public static SqlKeywordInfo UnionKeyword { get; }

        public static SqlKeywordInfo UnknownKeyword { get; }

        public static SqlKeywordInfo UpdateKeyword { get; }

        public static SqlKeywordInfo UserKeyword { get; }

        public static SqlKeywordInfo ViewKeyword { get; }

        public static SqlKeywordInfo[] DdlSubKeywords { get; }

        public string KeywordText { get; }

        public SqlKeyword SqlKeyword { get; }

        public SqlKeywordInfo[] FollowedByKeywords { get; private set; }

#pragma warning disable IDE0072 // Add missing cases
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CaptureNextTokenInSummary(in ParseState state, SqlKeyword currentKeyword) => currentKeyword switch
        {
            SqlKeyword.Exec => true,
            SqlKeyword.Exists => state.FirstSummaryKeyword is SqlKeyword.Create or SqlKeyword.Alter or SqlKeyword.Drop && state.PreviousSummaryKeyword is not (SqlKeyword.Login or SqlKeyword.User),
            SqlKeyword.From => state.PreviousSummaryKeyword is SqlKeyword.Select or SqlKeyword.Distinct,
            SqlKeyword.Into => state.FirstSummaryKeyword is SqlKeyword.Insert,
            SqlKeyword.Join => state.FirstSummaryKeyword is SqlKeyword.Select or SqlKeyword.Join,
            SqlKeyword.Login or SqlKeyword.User => false,
            SqlKeyword.Statistics => state.FirstSummaryKeyword is SqlKeyword.Update,
            SqlKeyword.Trigger => state.FirstSummaryKeyword is SqlKeyword.Create or SqlKeyword.Alter or SqlKeyword.Drop or SqlKeyword.Disable or SqlKeyword.Enable,
            SqlKeyword.Truncate => state.FirstSummaryKeyword is SqlKeyword.Table,
            SqlKeyword.Database or
            SqlKeyword.Function or
            SqlKeyword.Index or
            SqlKeyword.Procedure or
            SqlKeyword.Role or
            SqlKeyword.Schema or
            SqlKeyword.Sequence or
            SqlKeyword.Table or
            SqlKeyword.View => state.FirstSummaryKeyword is SqlKeyword.Create or SqlKeyword.Alter or SqlKeyword.Drop,
            _ => false,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SanitizeNextToken(in ParseState state, SqlKeyword currentKeyword) => currentKeyword switch
        {
            SqlKeyword.Login or SqlKeyword.User => state.FirstSummaryKeyword is SqlKeyword.Create or SqlKeyword.Alter or SqlKeyword.Drop,
            SqlKeyword.Exists => state.PreviousSummaryKeyword is SqlKeyword.Login or SqlKeyword.User,
            _ => false,
        };
#pragma warning restore IDE0072 // Add missing cases

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CaptureInSummary(in ParseState state, SqlKeywordInfo currentKeyword)
        {
            if (currentKeyword.captureInSummaryWhenPrevious == null || currentKeyword.captureInSummaryWhenPrevious.Length == 0)
            {
                return false;
            }

            var prev = state.PreviousParsedKeyword?.SqlKeyword ?? SqlKeyword.Unknown;
            for (var i = 0; i < currentKeyword.captureInSummaryWhenPrevious.Length; i++)
            {
                if (currentKeyword.captureInSummaryWhenPrevious[i] == prev)
                {
                    return true;
                }
            }

            return currentKeyword.SqlKeyword == SqlKeyword.Select
                && state.FirstSummaryKeyword is not SqlKeyword.Create
                && state.PreviousParsedKeyword?.SqlKeyword is not SqlKeyword.Union;
        }
    }
}
