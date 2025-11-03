// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Instrumentation;

internal static class SqlProcessor
{
    private const int MaxSummaryLength = 255;
    private const int CacheCapacity = 1000;

    private const char SanitizationPlaceholder = '?';
    private const char SpaceChar = ' ';
    private const char CommaChar = ',';
    private const char OpenSquareBracketChar = '[';
    private const char OpenParenChar = '(';
    private const char CloseParenChar = ')';
    private const char DashChar = '-';
    private const char ForwardSlashChar = '/';
    private const char SingleQuoteChar = '\'';
    private const char AsteriskChar = '*';
    private const char UnderscoreChar = '_';
    private const char DotChar = '.';
    private const char NewLineChar = '\n';
    private const char CarriageReturnChar = '\r';
    private const char TabChar = '\t';

    private static readonly ConcurrentDictionary<string, SqlStatementInfo> Cache = new();

    private static readonly char[] WhitespaceChars = [SpaceChar, TabChar, CarriageReturnChar, NewLineChar];

#if NET
    private static readonly SearchValues<char> WhitespaceSearchValues = SearchValues.Create(WhitespaceChars);
#endif

    // This is not an exhaustive list but covers the majority of common reserved SQL keywords that may follow a FROM clause.
    // This is used when determining if the previous token is a keyword in order to identify the end of a comma separated FROM clause.
    // NOTE: These are ordered so that more likely keywords appear first to shorten the comparison loop.
    private static readonly string[] FromClauseReservedKeywords = [
        "WHERE", "BY", "AS", "JOIN", "WITH", "CROSS", "HAVING", "WINDOW", "LIMIT", "OFFSET", "TABLESAMPLE", "PIVOT", "UNPIVOT"
    ];

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

    private enum SqlKeyword
    {
        Unknown,
        Alter,
        Clustered,
        Create,
        Database,
        Delete,
        Distinct,
        Drop,
        Exec,
        Exists,
        From,
        Function,
        If,
        Index,
        Insert,
        Into,
        Join,
        NonClustered,
        Not,
        On,
        Procedure,
        Role,
        Schema,
        Select,
        Sequence,
        Table,
        Trigger,
        Unique,
        Union,
        Update,
        User,
        View,
    }

    public static SqlStatementInfo GetSanitizedSql(string? sql)
    {
        if (sql == null)
        {
            return default;
        }

        if (Cache.TryGetValue(sql, out var sqlStatementInfo))
        {
            return sqlStatementInfo;
        }

        sqlStatementInfo = SanitizeSql(sql.AsSpan());

        // Fast-path capacity check using our own approximate count to avoid ConcurrentDictionary.Count cost.
        if (Volatile.Read(ref approxCacheCount) >= CacheCapacity)
        {
            return sqlStatementInfo;
        }

        // Attempt to add when under capacity. Increment our count only on successful add.
        if (Cache.TryAdd(sql, sqlStatementInfo))
        {
            Interlocked.Increment(ref approxCacheCount);
            return sqlStatementInfo;
        }

        // If another thread added meanwhile, return the cached value if available.
        return Cache.TryGetValue(sql, out var existing) ? existing : sqlStatementInfo;
    }

#if !NET
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiLetter(char c)
    {
        var lower = (char)(c | 0x20);
        return lower >= 'a' && lower <= 'z';
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiDigit(char c) =>
#if NET
        char.IsAsciiDigit(c);
#else
        c >= '0' && c <= '9';
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiIdentifierChar(char c) =>
#if NET
        char.IsLetter(c) || char.IsAsciiDigit(c) || c == UnderscoreChar || c == DotChar;
#else
        char.IsLetter(c) || IsAsciiDigit(c) || c == UnderscoreChar || c == DotChar;
#endif

    private static SqlStatementInfo SanitizeSql(ReadOnlySpan<char> sql)
    {
        // We use a single buffer for both sanitized SQL and DB query summary.
        // We rent a buffer twice the size of the input SQL to ensure
        // we have enough space for the sanitized SQL and summary. The summary starts
        // from the middle position of the rented buffer.
        var rentedBuffer = ArrayPool<char>.Shared.Rent(sql.Length * 2);

        var buffer = rentedBuffer.AsSpan();

        ParseState state = default;

        // Precompute the summary buffer slice once and carry it via state to avoid repeated Span.Slice calls.
        state.SummaryBuffer = buffer.Slice(rentedBuffer.Length / 2);

        while (state.ParsePosition < sql.Length)
        {
            if (SkipComment(sql, ref state))
            {
                continue;
            }

            if (SanitizeStringLiteral(sql, buffer, ref state) ||
                SanitizeHexLiteral(sql, buffer, ref state) ||
                SanitizeNumericLiteral(sql, buffer, ref state))
            {
                continue;
            }

            if (ParseWhitespace(sql, buffer, ref state))
            {
                continue;
            }

            if (state.SummaryPosition >= MaxSummaryLength)
            {
                ParseNextTokenFast(sql, buffer, ref state);
            }
            else
            {
                ParseNextToken(sql, buffer, ref state);
            }
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

            summary = summary.Slice(0, indexOfLastWhitespace);
        }

        var summaryLength = summary.Length;

        // Trim trailing whitespace
        if (summaryLength > 0)
        {
            var lastChar = summary[summaryLength - 1];

            if (lastChar == SpaceChar || lastChar == TabChar || lastChar == NewLineChar || lastChar == CarriageReturnChar)
            {
                summaryLength -= 1;
            }
        }

        var sqlStatementInfo = new SqlStatementInfo(
            buffer.Slice(0, state.SanitizedPosition).ToString(),
            summary.Slice(0, summaryLength).ToString());

        // We don't clear the buffer as we know the content has been sanitized
        ArrayPool<char>.Shared.Return(rentedBuffer);

        return sqlStatementInfo;
    }

    private static void ParseNextTokenFast(
        ReadOnlySpan<char> sql,
        Span<char> buffer,
        ref ParseState state)
    {
        var start = state.ParsePosition;

#if NET
        var indexOfNextWhitespace = sql.Slice(start).IndexOfAny(WhitespaceSearchValues);
#else
        var indexOfNextWhitespace = sql.Slice(start).IndexOfAny(WhitespaceChars);
#endif

        var length = indexOfNextWhitespace >= 0 ? indexOfNextWhitespace : sql.Length - start;

        sql.Slice(start, length).CopyTo(buffer.Slice(state.SanitizedPosition));
        state.SanitizedPosition += length;
        state.ParsePosition += length;

        // Note, for efficiency, we do not attempt to update the previous token start/end positions
        // We no longer use these when in fast path mode.
    }

    private static void ParseNextToken(
        ReadOnlySpan<char> sql,
        Span<char> buffer,
        ref ParseState state)
    {
        var start = state.ParsePosition;
        var currentChar = sql[start];

        // Quick first-character filter: only attempt keyword matching if the current char is an ASCII letter.
#if NET
        var mayBeKeyword = char.IsAsciiLetter(currentChar);
#else
        var mayBeKeyword = IsAsciiLetter(currentChar);
#endif

        if (mayBeKeyword)
        {
            var remainingSql = sql.Slice(start);

            // Determine the length of the next contiguous ascii-letter run.
            // This allows some fast paths in the comparisons below.
            var asciiLetterLength = 1;
            while (asciiLetterLength < remainingSql.Length)
            {
                var ch = remainingSql[asciiLetterLength];
#if NET
                if (!char.IsAsciiLetter(ch))
#else
                if (!IsAsciiLetter(ch))
#endif
                {
                    break;
                }

                asciiLetterLength++;
            }

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

            for (int i = 0; i < keywordsToCheck.Length; i++)
            {
                var potentialKeywordInfo = keywordsToCheck[i];
                var keywordSpan = potentialKeywordInfo.KeywordText.AsSpan();
                var keywordLength = keywordSpan.Length;

                // If the next token length doesn't match the keyword length, it can't be a match.
                if (asciiLetterLength != keywordLength)
                {
                    continue;
                }

                var sqlToCopy = remainingSql.Slice(0, keywordLength);
                var matchedKeyword = true;

                // Compare the potential keyword in a case-insensitive manner.
                for (var charPos = 1; charPos < keywordLength; charPos++)
                {
                    // We know that sqlToCopy is all ascii letters so this comparison is safe.
                    if ((sqlToCopy[charPos] | 0x20) != keywordSpan[charPos])
                    {
                        matchedKeyword = false;
                        break;
                    }
                }

                if (matchedKeyword)
                {
                    // Copy the keyword to the sanitized buffer.
                    sqlToCopy.CopyTo(buffer.Slice(state.SanitizedPosition));
                    state.SanitizedPosition += keywordLength;

                    // Potentially copy the keyword to the summary buffer.
                    if (SqlKeywordInfo.CaptureInSummary(in state, potentialKeywordInfo))
                    {
                        if (state.SummaryPosition == 0)
                        {
                            state.FirstSummaryKeyword = potentialKeywordInfo.SqlKeyword;
                        }

                        sqlToCopy.CopyTo(state.SummaryBuffer.Slice(state.SummaryPosition));
                        state.SummaryPosition += keywordLength;

                        // Add a space after the keyword. The trailing space will be trimmed later.
                        state.SummaryBuffer[state.SummaryPosition++] = ' ';

                        state.PreviousSummaryKeyword = potentialKeywordInfo.SqlKeyword;
                    }

                    state.CaptureNextTokenInSummary = SqlKeywordInfo.CaptureNextTokenInSummary(in state, potentialKeywordInfo.SqlKeyword);
                    state.PreviousParsedKeyword = potentialKeywordInfo;
                    state.ParsePosition += keywordLength;
                    state.PreviousTokenStartPosition = start;
                    state.PreviousTokenEndPosition = start + keywordLength;
                    state.InFromClause = potentialKeywordInfo.SqlKeyword == SqlKeyword.From;

                    // No further parsing needed for this token
                    return;
                }
            }
        }

        // If we get this far, we have not matched a keyword, so we copy the token as-is.

        if (char.IsLetter(currentChar) || currentChar == UnderscoreChar)
        {
            // This first block handles identifiers (which start with a letter or underscore).

            // Scan the token once, then bulk-copy to minimize per-char branching.
            var i = start;
            while (i < sql.Length)
            {
                var ch = sql[i];
                if (IsAsciiIdentifierChar(ch))
                {
                    i++;
                    continue;
                }

                break;
            }

            var length = i - start;
            if (length > 0)
            {
                var tokenSpan = sql.Slice(start, length);

                // Special handling: if we are in a FROM clause, check if this identifier is a reserved keyword
                // that indicates the end of the FROM clause.
                if (state.InFromClause)
                {
                    foreach (var reservedKeyword in FromClauseReservedKeywords)
                    {
                        if (IsCaseInsensitiveMatch(tokenSpan, reservedKeyword))
                        {
                            // We've matched a reserved keyword so are no longer in the FROM clause.
                            state.InFromClause = false;
                            break;
                        }
                    }
                }

                // Copy to sanitized buffer.
                tokenSpan.CopyTo(buffer.Slice(state.SanitizedPosition));
                state.SanitizedPosition += length;

                // Optionally copy to summary buffer.
                if (state.CaptureNextTokenInSummary)
                {
                    tokenSpan.CopyTo(state.SummaryBuffer.Slice(state.SummaryPosition));
                    state.SummaryPosition += length;

                    // Add a space after the identifier. The trailing space will be trimmed later.
                    state.SummaryBuffer[state.SummaryPosition++] = SpaceChar;
                }
            }

            state.ParsePosition = i;
            state.CaptureNextTokenInSummary = false;
            state.PreviousTokenStartPosition = start;
            state.PreviousTokenEndPosition = i;
        }
        else
        {
            // If we are in a FROM clause, we want to capture the next identifier following a comma or open square bracket.
            // Commas may occur when listing multiple tables in a FROM clause.
            // Brackets may occur when using schema-qualified or delimited identifiers.
            state.CaptureNextTokenInSummary = state.InFromClause && (currentChar == CommaChar || currentChar == OpenSquareBracketChar);

            buffer[state.SanitizedPosition++] = currentChar;
            state.ParsePosition++;

            // NOTE: We don't update previous token start/end positions for single-char tokens.
        }

        static bool IsCaseInsensitiveMatch(ReadOnlySpan<char> tokenSpan, string reservedKeyword)
        {
            if (tokenSpan.Length != reservedKeyword.Length)
            {
                return false;
            }

            var reservedKeywordSpan = reservedKeyword.AsSpan();

            for (var charPos = 0; charPos < tokenSpan.Length; charPos++)
            {
                if ((tokenSpan[charPos] | 0x20) != (reservedKeywordSpan[charPos] | 0x20))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private static bool ParseWhitespace(ReadOnlySpan<char> sql, Span<char> buffer, ref ParseState state)
    {
        var foundWhitespace = false;

        while (state.ParsePosition < sql.Length)
        {
            var currentChar = sql[state.ParsePosition];

#if NET
            if (WhitespaceSearchValues.Contains(currentChar))
#else
            if (currentChar == SpaceChar || currentChar == TabChar || currentChar == CarriageReturnChar || currentChar == NewLineChar)
#endif
            {
                foundWhitespace = true;
                buffer[state.SanitizedPosition++] = currentChar;
                state.ParsePosition++;
                continue; // keep consuming contiguous whitespace
            }

            break; // stop when currentChar is not whitespace
        }

        return foundWhitespace;
    }

    private static bool SkipComment(ReadOnlySpan<char> sql, ref ParseState state)
    {
        var i = state.ParsePosition;
        var ch = sql[i];
        var length = sql.Length;

        var iPlusOne = i + 1;
        var iPlusTwo = i + 2;

        // Scan past multi-line comment
        if (ch == '/' && iPlusOne < length && sql[iPlusOne] == AsteriskChar)
        {
            var rest = sql.Slice(iPlusTwo);
            while (!rest.IsEmpty)
            {
                var starIdx = rest.IndexOf(AsteriskChar);
                if (starIdx < 0)
                {
                    // Unterminated comment, consume to end
                    state.ParsePosition = length;
                    return true;
                }

                // Check for closing */
                if (starIdx + 1 < rest.Length && rest[starIdx + 1] == ForwardSlashChar)
                {
                    state.ParsePosition = iPlusTwo + starIdx + 2; // position after */
                    return true;
                }

                // Continue searching after this '*'
                rest = rest.Slice(starIdx + 1);
            }

            state.ParsePosition = length;
            return true;
        }

        // Scan past single-line comment
        if (ch == DashChar && iPlusOne < length && sql[iPlusOne] == DashChar)
        {
            // Find next line break efficiently and preserve the newline for whitespace handling
            var rest = sql.Slice(iPlusTwo);
            var idx = rest.IndexOfAny(CarriageReturnChar, NewLineChar);
            if (idx >= 0)
            {
                // Position at the newline so ParseWhitespace can copy it
                state.ParsePosition = iPlusTwo + idx;
            }
            else
            {
                state.ParsePosition = sql.Length;
            }

            return true;
        }

        return false;
    }

    private static bool SanitizeStringLiteral(ReadOnlySpan<char> sql, Span<char> buffer, ref ParseState state)
    {
        var currentChar = sql[state.ParsePosition];
        if (currentChar == SingleQuoteChar)
        {
            if (TrySanitizeLiteralsForInClause(sql, buffer, ref state, state.ParsePosition))
            {
                return true;
            }

            var rest = sql.Slice(state.ParsePosition + 1);
            while (!rest.IsEmpty)
            {
                var idx = rest.IndexOf(SingleQuoteChar);
                var idxPlusOne = idx + 1;
                if (idx < 0)
                {
                    state.ParsePosition = sql.Length;
                    return true;
                }

                if (idxPlusOne < rest.Length && rest[idxPlusOne] == SingleQuoteChar)
                {
                    // Skip escaped quote ('')
                    rest = rest.Slice(idx + 2);
                    continue;
                }

                // Found terminating quote
                state.ParsePosition = sql.Length - rest.Length + idxPlusOne;

                buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
                return true;
            }

            buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
            return true;
        }

        return false;
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
#if NET
                if (char.IsAsciiHexDigit(ch))
                {
                    continue;
                }
#else
                if (IsAsciiDigit(ch) ||
                    ch == 'A' || ch == 'a' ||
                    ch == 'B' || ch == 'b' ||
                    ch == 'C' || ch == 'c' ||
                    ch == 'D' || ch == 'd' ||
                    ch == 'E' || ch == 'e' ||
                    ch == 'F' || ch == 'f')
                {
                    continue;
                }
#endif

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
        if ((currentChar == '-' || currentChar == '+') && iPlusOne < length && (IsAsciiDigit(sql[iPlusOne]) || sql[iPlusOne] == DotChar))
        {
            i += 1;
            iPlusOne = i + 1;
            currentChar = sql[i];
        }

        // Scan past leading decimal point
        var periodMatched = false;
        if (currentChar == '.' && iPlusOne < length && IsAsciiDigit(sql[iPlusOne]))
        {
            periodMatched = true;
            i += 1;
            currentChar = sql[i];
        }

        if (IsAsciiDigit(currentChar))
        {
            if (TrySanitizeLiteralsForInClause(sql, buffer, ref state, i))
            {
                return true;
            }

            var exponentMatched = false;
            for (i += 1; i < length; ++i)
            {
                currentChar = sql[i];
                if (IsAsciiDigit(currentChar))
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

            var closingParenIndex = sql.Slice(parsePosition).IndexOf(CloseParenChar);

            if (closingParenIndex > 0)
            {
                state.ParsePosition = parsePosition + closingParenIndex;
                buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
                return true;
            }
        }

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

        // NOTE: If the number of bool fields increases significantly, consider combining into a bitfield.

        public bool CaptureNextTokenInSummary; // 1 byte

        /// <summary>
        /// Used to track if we are in a FROM clause for special handling of comma-separated table lists.
        /// When set to <c>true</c>, subsequent unmatched tokens will be compared against reserved keywords.
        /// As soon as we match a reserved keyword, we exit the FROM clause state.
        /// </summary>
        public bool InFromClause; // 1 byte
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
            CreateKeyword = new("create", SqlKeyword.Create, Unknown);
            DatabaseKeyword = new("database", SqlKeyword.Database, DdlKeywords);
            DeleteKeyword = new("delete", SqlKeyword.Delete, Unknown);
            DropKeyword = new("drop", SqlKeyword.Drop, Unknown);
            ExecKeyword = new("exec", SqlKeyword.Exec, Unknown);
            ExistsKeyword = new("exists", SqlKeyword.Exists);
            FromKeyword = new("from", SqlKeyword.From);
            FunctionKeyword = new("function", SqlKeyword.Function, DdlKeywords);
            IfKeyword = new("if", SqlKeyword.If);
            IndexKeyword = new("index", SqlKeyword.Index, [.. DdlKeywords, SqlKeyword.Unique, SqlKeyword.Clustered, SqlKeyword.NonClustered]);
            InsertKeyword = new("insert", SqlKeyword.Insert, Unknown);
            IntoKeyword = new("into", SqlKeyword.Into);
            JoinKeyword = new("join", SqlKeyword.Join);
            NotKeyword = new("not", SqlKeyword.Not);
            OnKeyword = new("on", SqlKeyword.On);
            ProcedureKeyword = new("procedure", SqlKeyword.Procedure, DdlKeywords);
            RoleKeyword = new("role", SqlKeyword.Role, DdlKeywords);
            SchemaKeyword = new("schema", SqlKeyword.Schema, DdlKeywords);
            SelectKeyword = new("select", SqlKeyword.Select, [SqlKeyword.Select, SqlKeyword.Unknown]);
            SequenceKeyword = new("sequence", SqlKeyword.Sequence, DdlKeywords);
            TableKeyword = new("table", SqlKeyword.Table, DdlKeywords);
            TriggerKeyword = new("trigger", SqlKeyword.Trigger, DdlKeywords);
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
                UserKeyword,
                RoleKeyword,
                SequenceKeyword,
                SchemaKeyword,
                FunctionKeyword,
            ];

            // Phase 3: Wire follow relationships
            AlterKeyword.FollowedByKeywords = DdlSubKeywords;
            CreateKeyword.FollowedByKeywords = DdlSubKeywords;
            DatabaseKeyword.FollowedByKeywords = [IfKeyword];
            DropKeyword.FollowedByKeywords = DdlSubKeywords;
            FromKeyword.FollowedByKeywords = [JoinKeyword, UnionKeyword];
            FunctionKeyword.FollowedByKeywords = [IfKeyword];
            IfKeyword.FollowedByKeywords = [NotKeyword, ExistsKeyword];
            IndexKeyword.FollowedByKeywords = [OnKeyword, IfKeyword];
            InsertKeyword.FollowedByKeywords = [IntoKeyword];
            JoinKeyword.FollowedByKeywords = [OnKeyword];
            NotKeyword.FollowedByKeywords = [ExistsKeyword];
            OnKeyword.FollowedByKeywords = [JoinKeyword];
            ProcedureKeyword.FollowedByKeywords = [IfKeyword];
            RoleKeyword.FollowedByKeywords = [IfKeyword];
            SchemaKeyword.FollowedByKeywords = [IfKeyword, UnionKeyword];
            SelectKeyword.FollowedByKeywords = [FromKeyword];
            SequenceKeyword.FollowedByKeywords = [IfKeyword];
            TableKeyword.FollowedByKeywords = [IfKeyword];
            TriggerKeyword.FollowedByKeywords = [IfKeyword];
            UnionKeyword.FollowedByKeywords = [SelectKeyword];
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

        public static SqlKeywordInfo CreateKeyword { get; }

        public static SqlKeywordInfo DatabaseKeyword { get; }

        public static SqlKeywordInfo DeleteKeyword { get; }

        public static SqlKeywordInfo DropKeyword { get; }

        public static SqlKeywordInfo ExecKeyword { get; }

        public static SqlKeywordInfo ExistsKeyword { get; }

        public static SqlKeywordInfo FromKeyword { get; }

        public static SqlKeywordInfo FunctionKeyword { get; }

        public static SqlKeywordInfo IfKeyword { get; }

        public static SqlKeywordInfo IndexKeyword { get; }

        public static SqlKeywordInfo InsertKeyword { get; }

        public static SqlKeywordInfo IntoKeyword { get; }

        public static SqlKeywordInfo JoinKeyword { get; }

        public static SqlKeywordInfo NotKeyword { get; }

        public static SqlKeywordInfo OnKeyword { get; }

        public static SqlKeywordInfo ProcedureKeyword { get; }

        public static SqlKeywordInfo RoleKeyword { get; }

        public static SqlKeywordInfo SchemaKeyword { get; }

        public static SqlKeywordInfo SelectKeyword { get; }

        public static SqlKeywordInfo SequenceKeyword { get; }

        public static SqlKeywordInfo TableKeyword { get; }

        public static SqlKeywordInfo TriggerKeyword { get; }

        public static SqlKeywordInfo UnionKeyword { get; }

        public static SqlKeywordInfo UnknownKeyword { get; }

        public static SqlKeywordInfo UpdateKeyword { get; }

        public static SqlKeywordInfo UserKeyword { get; }

        public static SqlKeywordInfo ViewKeyword { get; }

        public static SqlKeywordInfo[] DdlSubKeywords { get; }

        public string KeywordText { get; }

        public SqlKeyword SqlKeyword { get; }

        public SqlKeywordInfo[] FollowedByKeywords { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CaptureNextTokenInSummary(in ParseState state, SqlKeyword currentKeyword) => currentKeyword switch
        {
            SqlKeyword.Exec => true,
            SqlKeyword.From => state.PreviousSummaryKeyword is SqlKeyword.Select or SqlKeyword.Distinct,
            SqlKeyword.Into => state.FirstSummaryKeyword is SqlKeyword.Insert,
            SqlKeyword.Join => state.FirstSummaryKeyword is SqlKeyword.Select or SqlKeyword.Join,
            SqlKeyword.Database or SqlKeyword.Schema or SqlKeyword.Table or SqlKeyword.Index or SqlKeyword.View
                or SqlKeyword.Procedure or SqlKeyword.Trigger or SqlKeyword.Function or SqlKeyword.User
                or SqlKeyword.Role or SqlKeyword.Sequence => state.FirstSummaryKeyword is SqlKeyword.Create or SqlKeyword.Alter or SqlKeyword.Drop,
            SqlKeyword.Exists => state.FirstSummaryKeyword is SqlKeyword.Create or SqlKeyword.Alter or SqlKeyword.Drop,
            _ => false,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CaptureInSummary(in ParseState state, SqlKeywordInfo currentKeyword)
        {
            if (currentKeyword.captureInSummaryWhenPrevious == null || currentKeyword.captureInSummaryWhenPrevious.Length == 0)
            {
                return false;
            }

            var prev = state.PreviousParsedKeyword?.SqlKeyword ?? SqlKeyword.Unknown;
            for (int i = 0; i < currentKeyword.captureInSummaryWhenPrevious.Length; i++)
            {
                if (currentKeyword.captureInSummaryWhenPrevious[i] == prev)
                {
                    return true;
                }
            }

            if (currentKeyword.SqlKeyword == SqlKeyword.Select
                && state.FirstSummaryKeyword is not SqlKeyword.Create && state.PreviousParsedKeyword?.SqlKeyword is not SqlKeyword.Union)
            {
                return true;
            }

            return false;
        }
    }
}
