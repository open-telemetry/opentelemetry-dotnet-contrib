// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Instrumentation;

internal static class SqlProcessor
{
    private const int CacheCapacity = 1000;

    private static readonly ConcurrentDictionary<string, SqlStatementInfo> Cache = new();

#if NET
    private static readonly SearchValues<char> WhitespaceSearchValues = SearchValues.Create([' ', '\t', '\r', '\n']);
#endif

    // We can extend this in the future to include more keywords if needed.
    // The keywords should be ordered by frequency of use to optimize performance.
    // This only includes keywords that are standalone or which are often the first keyword in a statement.
    private static readonly SqlKeywordInfo[] SqlKeywords =
    [
        SqlKeywordInfo.SelectKeyword,
        SqlKeywordInfo.InsertKeyword,
        SqlKeywordInfo.UpdateKeyword,
        SqlKeywordInfo.DeleteKeyword,
        SqlKeywordInfo.CreateKeyword,
        SqlKeywordInfo.AlterKeyword,
        SqlKeywordInfo.DropKeyword,
    ];

    // This is a special case used when handling sub-queries in parentheses.
    private static readonly SqlKeywordInfo[] SelectOnlyKeywordArray =
    [
        SqlKeywordInfo.SelectKeyword,
    ];

    // Maintain our own approximate count to avoid ConcurrentDictionary.Count on hot path.
    // We only increment on successful TryAdd. This may result in a slightly oversized cache under high concurrency
    // but this is acceptable for the use case.
    private static int approxCacheCount;

    private enum SqlKeyword
    {
        Unknown,
        Select,
        Insert,
        Update,
        Delete,
        From,
        Into,
        Join,
        Create,
        Alter,
        Drop,
        Table,
        Index,
        Procedure,
        View,
        Database,
        Trigger,
        Union,
        Unique,
        NonClustered,
        Clustered,
        Distinct,
        On,
        Schema,
        Function,
        User,
        Role,
        Sequence,
        If,
        Not,
        Exists,
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
    // Private helpers (kept after public methods to satisfy analyzers)
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
        char.IsAsciiLetter(c) || char.IsAsciiDigit(c) || c == '_' || c == '.';
#else
        IsAsciiLetter(c) || IsAsciiDigit(c) || c == '_' || c == '.';
#endif

    private static SqlStatementInfo SanitizeSql(ReadOnlySpan<char> sql)
    {
        // We use a single buffer for both sanitized SQL and DB query summary
        // DB query summary starts from the index of the length of the input SQL
        // We rent a buffer twice the size of the input SQL to ensure we have enough space
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

            ParseNextToken(sql, buffer, ref state);
        }

        var summaryLength = Math.Min(state.SummaryPosition, 255);

        // Trim trailing space (if required)
        if (summaryLength > 0 && state.SummaryBuffer[summaryLength - 1] == ' ')
        {
            summaryLength -= 1;
        }

        var sqlStatementInfo = new SqlStatementInfo(
            buffer.Slice(0, state.SanitizedPosition).ToString(),
            state.SummaryBuffer.Slice(0, summaryLength).ToString());

        // We don't clear the buffer as we know the content has been sanitized
        ArrayPool<char>.Shared.Return(rentedBuffer);

        return sqlStatementInfo;
    }

    private static void ParseNextToken(
        ReadOnlySpan<char> sql,
        Span<char> buffer,
        ref ParseState state)
    {
        var nextChar = sql[state.ParsePosition];

        // Quick first-character filter: only attempt keyword matching if the current char is an ASCII letter
#if NET
        var mayBeKeyword = char.IsAsciiLetter(nextChar);
#else
        var mayBeKeyword = IsAsciiLetter(nextChar);
#endif

        // As an optimization, we only compare for keywords if we haven't already captured 255 characters for the summary.
        // Avoid comparing for keywords if the previous token was a keyword that is expected to be followed by an identifier.
        if (mayBeKeyword)
        {
            var remainingSql = sql.Slice(state.ParsePosition);

            // Determine the length of the next contiguous ascii-letter run
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

            // NOTE: At one stage we tried checking if the length was between 2 and 12 (inclusive)
            // the range of shortest and longest keywords. This ended up being slower in practice
            // as many tokens fall into this range and it was faster to skip the length check.

            ReadOnlySpan<SqlKeywordInfo> keywordsToCheck;

            // Check if the previous character is '(', in which case, we only check against the SELECT keyword.
            // Otherwise, check if the previous keyword may be the start of a keyword chain so we can limit the
            // number of keyword comparisons we need to do by only comparing for tokens we expect to appear next.
            if (state.ParsePosition > 0 && sql[state.ParsePosition - 1] == '(')
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

                // First-letter quick check to reduce comparisons early.
                // We know the current char is an ASCII letter so this is a safe way to lowercase.
                // The keyword string is already lowercase so doesn't need to be lowercased here.
                if ((remainingSql[0] | 0x20) != keywordSpan[0])
                {
                    continue;
                }

                var matchedKeyword = true;

                var sqlToCopy = remainingSql.Slice(0, keywordLength);

                // Compare the potential keyword in a case-insensitive manner
                for (var charPos = 1; charPos < keywordLength; charPos++)
                {
                    if ((sqlToCopy[charPos] | 0x20) != keywordSpan[charPos])
                    {
                        matchedKeyword = false;
                        break;
                    }
                }

                if (matchedKeyword)
                {
                    sqlToCopy.CopyTo(buffer.Slice(state.SanitizedPosition));
                    state.SanitizedPosition += keywordLength;

                    // We only capture if we haven't already filled the summary to the max length of 255.
                    // Check if the keyword should be captured in the summary
                    if (state.SummaryPosition < 255 && SqlKeywordInfo.CaptureInSummary(in state, potentialKeywordInfo))
                    {
                        if (state.SummaryPosition == 0)
                        {
                            state.FirstSummaryKeyword = potentialKeywordInfo.SqlKeyword;
                        }

                        sqlToCopy.CopyTo(state.SummaryBuffer.Slice(state.SummaryPosition));
                        state.SummaryPosition += keywordLength;

                        // Add a space after the keyword. The trailing space will be trimmed later if needed.
                        state.SummaryBuffer[state.SummaryPosition++] = ' ';

                        state.PreviousSummaryKeyword = potentialKeywordInfo.SqlKeyword;
                    }

                    state.CaptureNextTokenInSummary = SqlKeywordInfo.CaptureNextTokenInSummary(in state, potentialKeywordInfo.SqlKeyword);
                    state.PreviousParsedKeyword = potentialKeywordInfo;
                    state.ParsePosition += keywordLength;

                    // No further parsing needed for this token
                    return;
                }
            }
        }

        // If we get this far, we have not matched a keyword, so we copy the token as-is
        if (char.IsLetter(nextChar) || nextChar == '_')
        {
            // Scan the identifier token once, then bulk-copy to minimize per-char branching
            var start = state.ParsePosition;
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
                // Copy to sanitized buffer
                sql.Slice(start, length).CopyTo(buffer.Slice(state.SanitizedPosition));
                state.SanitizedPosition += length;

                // Optionally copy to summary buffer
                if (state.SummaryPosition < 255 && state.CaptureNextTokenInSummary)
                {
                    // We may copy paste 255 here which is fine as we slice to max 255 when creating the final string
                    sql.Slice(start, length).CopyTo(state.SummaryBuffer.Slice(state.SummaryPosition));
                    state.SummaryPosition += length;

                    // Add a space after the identifier. The trailing space will be trimmed later if needed.
                    state.SummaryBuffer[state.SummaryPosition++] = ' ';
                }
            }

            state.ParsePosition = i;
            state.CaptureNextTokenInSummary = false;
        }
        else
        {
            var prevKeyword = state.PreviousParsedKeyword?.SqlKeyword ?? SqlKeyword.Unknown;
            state.CaptureNextTokenInSummary = prevKeyword == SqlKeyword.From && nextChar == ',';

            buffer[state.SanitizedPosition++] = nextChar;
            state.ParsePosition++;
        }
    }

    private static bool ParseWhitespace(ReadOnlySpan<char> sql, Span<char> buffer, ref ParseState state)
    {
        var foundWhitespace = false;

        while (state.ParsePosition < sql.Length)
        {
            var nextChar = sql[state.ParsePosition];

#if NET
            if (WhitespaceSearchValues.Contains(nextChar))
#else
            if (nextChar == ' ' || nextChar == '\t' || nextChar == '\r' || nextChar == '\n')
#endif
            {
                foundWhitespace = true;
                buffer[state.SanitizedPosition++] = nextChar;
                state.ParsePosition++;
                continue; // keep consuming contiguous whitespace
            }

            break; // stop when nextChar is not whitespace
        }

        return foundWhitespace;
    }

    private static bool SkipComment(ReadOnlySpan<char> sql, ref ParseState state)
    {
        var i = state.ParsePosition;
        var ch = sql[i];
        var length = sql.Length;

        // Scan past multi-line comment
        if (ch == '/' && i + 1 < length && sql[i + 1] == '*')
        {
#if NET
            var rest = sql.Slice(i + 2);
            while (!rest.IsEmpty)
            {
                var starIdx = rest.IndexOf('*');
                if (starIdx < 0)
                {
                    // Unterminated comment, consume to end
                    state.ParsePosition = length;
                    return true;
                }

                // Check for closing */
                if (starIdx + 1 < rest.Length && rest[starIdx + 1] == '/')
                {
                    state.ParsePosition = i + 2 + starIdx + 2; // position after */
                    return true;
                }

                // Continue searching after this '*'
                rest = rest.Slice(starIdx + 1);
            }

            state.ParsePosition = length;
            return true;
#else
            for (i += 2; i < length; ++i)
            {
                ch = sql[i];
                if (ch == '*' && i + 1 < length && sql[i + 1] == '/')
                {
                    i += 1;
                    break;
                }
            }

            state.ParsePosition = ++i;
            return true;
#endif
        }

        // Scan past single-line comment
        if (ch == '-' && i + 1 < length && sql[i + 1] == '-')
        {
#if NET
            // Find next line break efficiently and preserve the newline for whitespace handling
            var rest = sql.Slice(i + 2);
            var idx = rest.IndexOfAny('\r', '\n');
            if (idx >= 0)
            {
                // Position at the newline so ParseWhitespace can copy it
                state.ParsePosition = i + 2 + idx;
            }
            else
            {
                state.ParsePosition = sql.Length;
            }

            return true;
#else
            for (i += 2; i < length; ++i)
            {
                ch = sql[i];
                if (ch == '\r' || ch == '\n')
                {
                    i -= 1;
                    break;
                }
            }

            state.ParsePosition = ++i;
            return true;
#endif
        }

        return false;
    }

    private static bool SanitizeStringLiteral(ReadOnlySpan<char> sql, Span<char> buffer, ref ParseState state)
    {
        var nextChar = sql[state.ParsePosition];
        if (nextChar == '\'')
        {
#if NET
            var rest = sql.Slice(state.ParsePosition + 1);
            while (!rest.IsEmpty)
            {
                var idx = rest.IndexOf('\'');
                if (idx < 0)
                {
                    state.ParsePosition = sql.Length;
                    return true;
                }

                if (idx + 1 < rest.Length && rest[idx + 1] == '\'')
                {
                    // Skip escaped quote ('')
                    rest = rest.Slice(idx + 2);
                    continue;
                }

                // Found terminating quote
                state.ParsePosition = sql.Length - rest.Length + idx + 1;

                buffer[state.SanitizedPosition++] = '?';
                return true;
            }

            buffer[state.SanitizedPosition++] = '?';
            return true;
#else
            var i = state.ParsePosition + 1;
            var length = sql.Length;
            for (; i < length; ++i)
            {
                nextChar = sql[i];
                if (nextChar == '\'' && i + 1 < length && sql[i + 1] == '\'')
                {
                    ++i;
                    continue;
                }

                if (nextChar == '\'')
                {
                    break;
                }
            }

            state.ParsePosition = ++i;

            buffer[state.SanitizedPosition++] = '?';
            return true;
#endif
        }

        return false;
    }

    private static bool SanitizeHexLiteral(ReadOnlySpan<char> sql, Span<char> buffer, ref ParseState state)
    {
        var i = state.ParsePosition;
        var ch = sql[i];
        var length = sql.Length;

        if (ch == '0' && i + 1 < length && (sql[i + 1] == 'x' || sql[i + 1] == 'X'))
        {
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

            buffer[state.SanitizedPosition++] = '?';
            return true;
        }

        return false;
    }

    private static bool SanitizeNumericLiteral(ReadOnlySpan<char> sql, Span<char> buffer, ref ParseState state)
    {
        var i = state.ParsePosition;
        var nextChar = sql[i];
        var length = sql.Length;

        // If the digit follows an open bracket, check for a parenthesized digit sequence
        if (i > 0 && sql[i - 1] == '('
            && IsAsciiDigit(nextChar))
        {
            int start = i;
            int j = i;

            // Scan until closing ')', ensure all are digits
            while (j < length && IsAsciiDigit(sql[j]))
            {
                j++;
            }

            if (j < length && sql[j] == ')')
            {
                // Copy the digits and the closing bracket to the buffer
                sql.Slice(start, j - start + 1).CopyTo(buffer.Slice(state.SanitizedPosition));
                state.SanitizedPosition += j - start + 1;
                state.ParsePosition = j + 1;
                return true;
            }

            // If not a valid parenthesized digit sequence, fall through to normal logic
        }

        // Scan past leading sign
        if ((nextChar == '-' || nextChar == '+') && i + 1 < length && (IsAsciiDigit(sql[i + 1]) || sql[i + 1] == '.'))
        {
            i += 1;
            nextChar = sql[i];
        }

        // Scan past leading decimal point
        var periodMatched = false;
        if (nextChar == '.' && i + 1 < length && IsAsciiDigit(sql[i + 1]))
        {
            periodMatched = true;
            i += 1;
            nextChar = sql[i];
        }

        if (IsAsciiDigit(nextChar))
        {
            var exponentMatched = false;
            for (i += 1; i < length; ++i)
            {
                nextChar = sql[i];
                if (IsAsciiDigit(nextChar))
                {
                    continue;
                }

                if (!periodMatched && nextChar == '.')
                {
                    periodMatched = true;
                    continue;
                }

                if (!exponentMatched && (nextChar == 'e' || nextChar == 'E'))
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

            buffer[state.SanitizedPosition++] = '?';
            return true;
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
        public Span<char> SummaryBuffer; // 16 bytes (on x64)

        public SqlKeywordInfo? PreviousParsedKeyword; // 8 bytes (reference type)

        public SqlKeyword FirstSummaryKeyword; // 4 bytes (enum, underlying int)
        public SqlKeyword PreviousSummaryKeyword; // 4 bytes (enum, underlying int)

        // These track the current parse position in the input SQL and the current write position
        // for the sanitized SQL and summary buffers.
        public int ParsePosition; // 4 bytes
        public int SanitizedPosition; // 4 bytes
        public int SummaryPosition; // 4 bytes

        public bool CaptureNextTokenInSummary; // 1 byte
    }

    private sealed class SqlKeywordInfo
    {
        // Used on keywords that are only included in the summary if they are the first keyword in the statement.
        private static readonly SqlKeyword[] Unknown = [SqlKeyword.Unknown];

        private static readonly SqlKeyword[] DdlKeywords = [
            SqlKeyword.Create, SqlKeyword.Drop, SqlKeyword.Alter
        ];

        private readonly SqlKeyword[]? captureInSummaryWhenPrevious;

        static SqlKeywordInfo()
        {
            // Phase 1: Create all static instances.
            // We will compare the SQL we are parsing in lowercase, so we store these in lowercase also.
            AlterKeyword = new("alter", SqlKeyword.Alter, Unknown);
            ClusteredKeyword = new("clustered", SqlKeyword.Clustered, [SqlKeyword.Unique]);
            CreateKeyword = new("create", SqlKeyword.Create, Unknown);
            DatabaseKeyword = new("database", SqlKeyword.Database, DdlKeywords);
            DeleteKeyword = new("delete", SqlKeyword.Delete, Unknown);
            DistinctKeyword = new("distinct", SqlKeyword.Distinct, [SqlKeyword.Select]);
            DropKeyword = new("drop", SqlKeyword.Drop, Unknown);
            ExistsKeyword = new("exists", SqlKeyword.Exists);
            FromKeyword = new("from", SqlKeyword.From);
            FunctionKeyword = new("function", SqlKeyword.Function, DdlKeywords);
            IfKeyword = new("if", SqlKeyword.If);
            IndexKeyword = new("index", SqlKeyword.Index, [.. DdlKeywords, SqlKeyword.Unique, SqlKeyword.Clustered, SqlKeyword.NonClustered]);
            InsertKeyword = new("insert", SqlKeyword.Insert, Unknown);
            IntoKeyword = new("into", SqlKeyword.Into);
            JoinKeyword = new("join", SqlKeyword.Join);
            NonClusteredKeyword = new("nonclustered", SqlKeyword.NonClustered, [SqlKeyword.Unique]);
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
            UniqueKeyword = new("unique", SqlKeyword.Unique, DdlKeywords);
            UnknownKeyword = new(string.Empty, SqlKeyword.Unknown);
            UpdateKeyword = new("update", SqlKeyword.Update, Unknown);
            UserKeyword = new("user", SqlKeyword.User, DdlKeywords);
            ViewKeyword = new("view", SqlKeyword.View, DdlKeywords);

            // Phase 2: Build arrays that depend on instances
            DdlSubKeywords = [
                TableKeyword, IndexKeyword, ViewKeyword, ProcedureKeyword, TriggerKeyword,
                DatabaseKeyword, SchemaKeyword, FunctionKeyword, UserKeyword, RoleKeyword, SequenceKeyword, UniqueKeyword,
                ClusteredKeyword, NonClusteredKeyword
            ];

            // Phase 3: Wire follow relationships
            AlterKeyword.FollowedByKeywords = DdlSubKeywords;
            ClusteredKeyword.FollowedByKeywords = [IndexKeyword];
            CreateKeyword.FollowedByKeywords = DdlSubKeywords;
            DatabaseKeyword.FollowedByKeywords = [IfKeyword];
            DistinctKeyword.FollowedByKeywords = [FromKeyword];
            DropKeyword.FollowedByKeywords = DdlSubKeywords;
            FromKeyword.FollowedByKeywords = [JoinKeyword, UnionKeyword];
            FunctionKeyword.FollowedByKeywords = [IfKeyword];
            IfKeyword.FollowedByKeywords = [NotKeyword, ExistsKeyword];
            IndexKeyword.FollowedByKeywords = [OnKeyword, IfKeyword];
            InsertKeyword.FollowedByKeywords = [IntoKeyword];
            JoinKeyword.FollowedByKeywords = [OnKeyword];
            NonClusteredKeyword.FollowedByKeywords = [IndexKeyword];
            NotKeyword.FollowedByKeywords = [ExistsKeyword];
            OnKeyword.FollowedByKeywords = [JoinKeyword];
            ProcedureKeyword.FollowedByKeywords = [IfKeyword];
            RoleKeyword.FollowedByKeywords = [IfKeyword];
            SchemaKeyword.FollowedByKeywords = [IfKeyword, UnionKeyword];
            SelectKeyword.FollowedByKeywords = [FromKeyword, DistinctKeyword];
            SequenceKeyword.FollowedByKeywords = [IfKeyword];
            TableKeyword.FollowedByKeywords = [IfKeyword];
            TriggerKeyword.FollowedByKeywords = [IfKeyword];
            UnionKeyword.FollowedByKeywords = [SelectKeyword];
            UniqueKeyword.FollowedByKeywords = [IndexKeyword, ClusteredKeyword, NonClusteredKeyword];
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

        public static SqlKeywordInfo ClusteredKeyword { get; }

        public static SqlKeywordInfo CreateKeyword { get; }

        public static SqlKeywordInfo DatabaseKeyword { get; }

        public static SqlKeywordInfo DeleteKeyword { get; }

        public static SqlKeywordInfo DistinctKeyword { get; }

        public static SqlKeywordInfo DropKeyword { get; }

        public static SqlKeywordInfo ExistsKeyword { get; }

        public static SqlKeywordInfo FromKeyword { get; }

        public static SqlKeywordInfo FunctionKeyword { get; }

        public static SqlKeywordInfo IfKeyword { get; }

        public static SqlKeywordInfo IndexKeyword { get; }

        public static SqlKeywordInfo InsertKeyword { get; }

        public static SqlKeywordInfo IntoKeyword { get; }

        public static SqlKeywordInfo JoinKeyword { get; }

        public static SqlKeywordInfo NonClusteredKeyword { get; }

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

        public static SqlKeywordInfo UniqueKeyword { get; }

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
