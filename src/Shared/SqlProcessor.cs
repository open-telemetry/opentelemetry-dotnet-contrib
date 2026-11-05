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

    // The most characters which are read in total, looking for the bracket which ends a list of values, so that
    // many brackets which could start one (e.g. [--a] or [N'a]) do not each read to the end of the statement.
    private const int MaxBracketLookaheadLength = 16 * 1024;
    private const int CacheCapacity = 1000;

    // The number of combinations of the values of SqlDialect, which is one more than the one which has all of them.
    private const int DialectCount = (int)(SqlDialect.BackslashEscapes | SqlDialect.TripleQuotedLiterals | SqlDialect.NonNestedComments | SqlDialect.BracketsDelimitIdentifiersOnly) + 1;

    // The maximum length of a statement which is cached.
    private const int MaxCachedStatementLength = 8192;

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
    private const char EqualsChar = '=';
    private const char SemicolonChar = ';';

    private const string ArrayKeyword = "ARRAY";

    // The words around a password which is not a literal, which are compared ignoring case (e.g. IDENTIFIED BY password).
    private const string IdentifiedWord = "identified";
    private const string ByWord = "by";
    private const string ReplaceWord = "replace";
    private const string ValuesWord = "values";

    // The words which are compared, ignoring case, to find the name of a login or user (e.g. CREATE USER name FOR LOGIN name).
    private const string CreateWord = "create";
    private const string AlterWord = "alter";
    private const string DropWord = "drop";
    private const string UserWord = "user";
    private const string LoginWord = "login";
    private const string ForWord = "for";
    private const string FromWord = "from";
    private const string WithWord = "with";
    private const string NameWord = "name";
    private const string RenameWord = "rename";
    private const string ToWord = "to";
    private const string IfWord = "if";
    private const string NotWord = "not";
    private const string ExistsWord = "exists";
    private const string TableWord = "table";
    private const string OrWord = "or";

    // The words which follow the name of a column in a clause of ALTER TABLE (e.g. ALTER login TYPE text, DROP login CASCADE).
    private static readonly string[] ColumnOperationWords =
    [
        "type",
        "set",
        "drop",
        "add",
        "reset",
        "collate",
        "using",
        "cascade",
        "restrict",
        "alter",
    ];

    // The words which start a statement, which can follow a statement which creates, alters or drops a login or user.
    private static readonly string[] StatementStartWords =
    [
        "select",
        "insert",
        "update",
        "delete",
        "merge",
        "truncate",
        "create",
        "alter",
        "drop",
        "exec",
        "execute",
        "grant",
        "revoke",
        "deny",
        "use",
        "backup",
        "restore",
    ];

    // The sanitized statements of each dialect, which are the index of the cache.
    private static readonly ConcurrentDictionary<string, SqlStatementInfo>[] Caches = CreateCaches();

    private static readonly char[] WhitespaceChars = [SpaceChar, TabChar, CarriageReturnChar, NewLineChar];
#if !NET
    private static readonly char[] LineBreakChars = [CarriageReturnChar, NewLineChar];
#endif

#if NET
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

    private static readonly SearchValues<char> AsciiLetterSearchValues = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");
    private static readonly SearchValues<char> QuoteDollarOrCommentSearchValues = SearchValues.Create("'$-/");
    private static readonly SearchValues<char> LineBreakSearchValues = SearchValues.Create("\n\r");
    private static readonly SearchValues<char> WhitespaceSearchValues = SearchValues.Create(WhitespaceChars);
    private static readonly SearchValues<char> InClauseScanSearchValues = SearchValues.Create(InClauseScanChars);
#endif

    // This is not an exhaustive list but covers the majority of common reserved SQL keywords that may follow a FROM clause.
    // This is used when determining if the previous token is a keyword in order to identify the end of a comma separated FROM clause.
    // NOTE: These are ordered so that more likely keywords appear first to shorten the comparison loop.
    private static readonly string[] FromClauseReservedKeywords =
    [
        "WHERE",
        "BY",
        "AS",
        "JOIN",
        "WITH",
        "CROSS",
        "HAVING",
        "WINDOW",
        "LIMIT",
        "OFFSET",
        "TABLESAMPLE",
        "PIVOT",
        "UNPIVOT",
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
    private static readonly int[] ApproxCacheCounts = new int[DialectCount];

    // The most recent statement sanitized on the current thread. Instrumentations commonly sanitize the
    // same command text instance more than once per command on the same thread (for example EF Core's
    // CommandExecuting event followed by SqlClient's WriteCommandBefore event for the same DbCommand),
    // so a reference-equality check lets the repeated call skip hashing the full text for the cache
    // lookup (or re-sanitizing it entirely once the cache is full). This retains at most one statement
    // (and its sanitized form) per thread until that thread sanitizes a different statement. Statements
    // longer than MaxCachedStatementLength are not remembered, so the memory retained is bounded.
    [ThreadStatic]
    private static LastSanitizedStatement? lastSanitizedStatement;

    // The differences between the dialects of SQL which are sanitized. A statement is cached per combination of them.
    [Flags]
    private enum SqlDialect
    {
        Default = 0,
        BackslashEscapes = 1,
        TripleQuotedLiterals = 2,
        NonNestedComments = 4,
        BracketsDelimitIdentifiersOnly = 8,
    }

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
    /// corresponding <c>db.query.summary</c>. Results are cached per statement and dialect,
    /// unless the statement is too long to be cached.
    /// </summary>
    /// <param name="sql">The SQL statement to sanitize.</param>
    /// <param name="useBackslashEscapes">
    /// <see langword="true"/> if the source database is MySQL or MariaDB with their default SQL modes, or GoogleSQL
    /// (<c>NO_BACKSLASH_ESCAPES</c> and <c>ANSI_QUOTES</c> disabled), in which case a backslash is
    /// treated as a string-literal escape character, a double-quoted (<c>"..."</c>) value is treated
    /// as a string literal rather than a quoted identifier, <c>#</c> starts a comment and block
    /// comments do not nest; otherwise <see langword="false"/>.
    /// </param>
    /// <param name="useTripleQuotedLiterals">
    /// <see langword="true"/> if the source database uses GoogleSQL (e.g. Spanner), in which case
    /// <c>'''...'''</c> and <c>"""..."""</c> are also string literals, in which a quote does not have to
    /// be escaped. GoogleSQL has the other rules described for <paramref name="useBackslashEscapes"/> too,
    /// so this implies <paramref name="useBackslashEscapes"/>; otherwise <see langword="false"/>.
    /// </param>
    /// <param name="allowNestedComments">
    /// <see langword="true"/> if the source database allows block comments to be nested (e.g. SQL Server,
    /// PostgreSQL and Db2), so that a comment such as <c>/* /* */ don't */</c> only ends at the second
    /// <c>*/</c>; <see langword="false"/> if a block comment ends at the first <c>*/</c> (e.g. SQLite and Oracle).
    /// Block comments never nest for the dialects described by <paramref name="useBackslashEscapes"/>.
    /// </param>
    /// <param name="bracketsDelimitIdentifiersOnly">
    /// <see langword="true"/> if the source database is SQL Server or SQLite, in which <c>[...]</c> always delimits
    /// an identifier (even if it has an apostrophe, such as <c>['Tis]</c>), so that it is never a list of values
    /// or the subscript of an array as in some other dialects (e.g. <c>['a', 'b']</c> or <c>items[1]</c>);
    /// otherwise <see langword="false"/>.
    /// </param>
    /// <returns>The sanitized SQL and query summary.</returns>
    public static SqlStatementInfo GetSanitizedSql(
        string? sql,
        bool useBackslashEscapes = false,
        bool useTripleQuotedLiterals = false,
        bool allowNestedComments = true,
        bool bracketsDelimitIdentifiersOnly = false)
    {
        if (sql == null)
        {
            return default;
        }

        var dialect = SqlDialect.Default;

        // GoogleSQL has the rules of MySQL and MariaDB which useBackslashEscapes is for, and neither nest block comments.
        if (useTripleQuotedLiterals)
        {
            dialect |= SqlDialect.TripleQuotedLiterals | SqlDialect.BackslashEscapes | SqlDialect.NonNestedComments;
        }
        else if (useBackslashEscapes)
        {
            dialect |= SqlDialect.BackslashEscapes | SqlDialect.NonNestedComments;
        }
        else if (!allowNestedComments)
        {
            dialect |= SqlDialect.NonNestedComments;
        }

        if (bracketsDelimitIdentifiersOnly)
        {
            dialect |= SqlDialect.BracketsDelimitIdentifiersOnly;
        }

        return GetSanitizedSql(sql, dialect);
    }

    private static ConcurrentDictionary<string, SqlStatementInfo>[] CreateCaches()
    {
        var caches = new ConcurrentDictionary<string, SqlStatementInfo>[DialectCount];

        for (var i = 0; i < caches.Length; i++)
        {
            caches[i] = new();
        }

        return caches;
    }

    private static SqlStatementInfo GetSanitizedSql(string sql, SqlDialect dialect)
    {
        // Statements above the length limit are neither cached nor remembered for the thread,
        // so that the memory retained does not depend on the length of the statements executed.
        if (sql.Length > MaxCachedStatementLength)
        {
            return SanitizeSql(sql, dialect);
        }

        if (sql.Length < LastStatementMinLength && ApproxCacheCounts[(int)dialect] < CacheCapacity)
        {
            return GetOrAddCachedSql(sql, dialect);
        }

        // Strings are immutable, so the same instance sanitized
        // with the same dialect always produces the same result.
        var last = lastSanitizedStatement;
        if (last != null &&
            ReferenceEquals(sql, last.Sql) &&
            dialect == last.Dialect)
        {
            return last.StatementInfo;
        }

        var sqlStatementInfo = GetOrAddCachedSql(sql, dialect);

        last ??= lastSanitizedStatement = new();
        last.Sql = sql;
        last.Dialect = dialect;
        last.StatementInfo = sqlStatementInfo;

        return sqlStatementInfo;
    }

    private static SqlStatementInfo GetOrAddCachedSql(string sql, SqlDialect dialect)
    {
        var cache = Caches[(int)dialect];

        if (cache.TryGetValue(sql, out var sqlStatementInfo))
        {
            return sqlStatementInfo;
        }

        sqlStatementInfo = SanitizeSql(sql, dialect);

        // Fast-path capacity check using our own approximate count to avoid ConcurrentDictionary.Count cost.
        if (Volatile.Read(ref ApproxCacheCounts[(int)dialect]) >= CacheCapacity)
        {
            return sqlStatementInfo;
        }

        // Attempt to add when under capacity. Increment our count only on successful add.
        if (cache.TryAdd(sql, sqlStatementInfo))
        {
            Interlocked.Increment(ref ApproxCacheCounts[(int)dialect]);
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

    private static SqlStatementInfo SanitizeSql(string sql, SqlDialect dialect)
    {
        var sqlSpan = sql.AsSpan();

        // We use a single buffer for both sanitized SQL and DB query summary.
        // We rent a buffer twice the size of the input SQL to ensure
        // we have enough space for the sanitized SQL and summary. The summary starts
        // from the middle position of the rented buffer.
        var rentedBuffer = ArrayPool<char>.Shared.Rent(sqlSpan.Length * 2);

        var buffer = rentedBuffer.AsSpan();

        ParseState state = default;
        state.UseBackslashEscapes = (dialect & SqlDialect.BackslashEscapes) != 0;
        state.UseTripleQuotedLiterals = (dialect & SqlDialect.TripleQuotedLiterals) != 0;
        state.AllowNestedComments = (dialect & SqlDialect.NonNestedComments) == 0;
        state.BracketsDelimitIdentifiersOnly = (dialect & SqlDialect.BracketsDelimitIdentifiersOnly) != 0;

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

                // A password which is a word can start with a digit, which is otherwise the start of a number.
                if (state.SanitizeNextTokenAsPassword && ParsePassword(sqlSpan, buffer, ref state))
                {
                    continue;
                }

                var literalStart = state.ParsePosition;

                if (SanitizeStringLiteral(sqlSpan, buffer, ref state) ||
                    SanitizeDollarQuotedLiteral(sqlSpan, buffer, ref state) ||
                    SanitizeHexLiteral(sqlSpan, buffer, ref state) ||
                    SanitizeNumericLiteral(sqlSpan, buffer, ref state))
                {
                    state.LastLiteralEnd = state.ParsePosition;

                    // The name of a login or user which is a quoted literal (e.g. DROP USER 'name') has been sanitized,
                    // unlike a number which starts a name (e.g. 1name).
                    if (state.SanitizeNextNonKeywordToken && sqlSpan[literalStart] is SingleQuoteChar or DoubleQuoteChar)
                    {
                        state.RedactedLoginOrUserEndPosition = state.ParsePosition;
                        state.SanitizeNextNonKeywordToken = false;
                    }

                    // A password which is a literal has been sanitized, so the token which follows it is not one,
                    // unless it is the word which precedes the password which is replaced (e.g. REPLACE old_password).
                    if (state.SanitizeNextTokenAsPassword)
                    {
                        state.SanitizeNextTokenAsPassword = false;
                        state.PasswordEndPosition = state.ParsePosition;
                    }

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
        // A summary which is exactly the maximum length but ends with a period has stopped part way through
        // a multi-part name (e.g. dbo.), as the period is only added when another part of the name follows,
        // so the incomplete name is trimmed in the same way.
        if (state.SummaryPosition > MaxSummaryLength ||
            (state.SummaryPosition == MaxSummaryLength && summary[MaxSummaryLength - 1] == DotChar))
        {
#if NET
            var indexOfLastWhitespace = summary.Slice(0, MaxSummaryLength).LastIndexOfAny(WhitespaceSearchValues);
#else
            var indexOfLastWhitespace = summary.Slice(0, MaxSummaryLength).LastIndexOfAny(WhitespaceChars);
#endif

            // If the whitespace is within the last name captured (e.g. [Order Details]), the name is incomplete
            // and is removed whole instead of being cut in half.
            if (indexOfLastWhitespace > state.LastNameStartPosition && indexOfLastWhitespace < state.LastNameEndPosition)
            {
                indexOfLastWhitespace = state.LastNameStartPosition;
            }

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
            else
            {
                // A period is added when another part of a multi-part name is expected, so if that part was not
                // captured (e.g. the name was too long to be a delimited identifier) it is left at the end.
                while (summaryLength > 0 && summary[summaryLength - 1] == DotChar)
                {
                    summaryLength -= 1;
                }
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

    // Appends a part of a name, recording where the whole (possibly multi-part) name starts and ends in the summary.
    // A part which follows a period (which is only added when another part of the name is to be captured) continues
    // the name, otherwise it starts a new one. This allows an incomplete name, or one which was cut short by the maximum
    // summary length, to be removed whole, as a delimited identifier (e.g. [Order Details]) can contain whitespace.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendSummaryNamePart(ReadOnlySpan<char> value, ref ParseState state)
    {
        if (state.SummaryPosition == 0 || state.SummaryBuffer[state.SummaryPosition - 1] != DotChar)
        {
            state.LastNameStartPosition = state.SummaryPosition;
        }

        AppendSummaryToken(value, ref state);

        state.LastNameEndPosition = state.SummaryPosition;
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

        // The word which is the password in a statement such as ALTER USER ... IDENTIFIED BY password is not a literal,
        // so it is not otherwise sanitized, and it must not be parsed as a keyword (e.g. Select1) or be split (e.g. a#b).
        if (state.SanitizeNextTokenAsPassword && ParsePassword(sql, buffer, ref state))
        {
            return;
        }

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

                    // Another statement starts, which does not have the names of the statement which precedes it.
                    if ((state.InLoginOrUserStatement || state.InAlterTable) &&
                        !state.SanitizeNextNonKeywordToken &&
                        IsAnyWord(sql.Slice(start, keywordLength), StatementStartWords))
                    {
                        EndStatementContext(sql.Slice(start, keywordLength), ref state);
                    }

                    state.CaptureNextNonKeywordTokenAsIdentifier = SqlKeywordInfo.CaptureNextTokenInSummary(in state, potentialKeywordInfo.SqlKeyword);
                    state.SanitizeNextNonKeywordToken = SqlKeywordInfo.SanitizeNextToken(in state, potentialKeywordInfo.SqlKeyword) &&
                        !(potentialKeywordInfo.SqlKeyword is SqlKeyword.Login or SqlKeyword.User &&
                          state.InAlterTable &&
                          IsColumnOperation(sql, start + keywordLength, state.UseBackslashEscapes, state.AllowNestedComments));

                    if (state.SanitizeNextNonKeywordToken)
                    {
                        state.InLoginOrUserStatement = true;
                        state.InDropLoginOrUserStatement |= state.PreviousParsedKeyword?.SqlKeyword == SqlKeyword.Drop;
                    }
                    else if (potentialKeywordInfo.SqlKeyword == SqlKeyword.Table && state.PreviousParsedKeyword?.SqlKeyword == SqlKeyword.Alter)
                    {
                        state.InAlterTable = true;
                    }

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

            // The whole name of a login or user is redacted, which can have characters which are not part of an
            // identifier elsewhere (e.g. acct$alice, or alice@host).
            if (state.SanitizeNextNonKeywordToken && i > start && !IsIfNotOrExists(sql.Slice(start, i - start)))
            {
                while (i < sql.Length && IsWordChar(sql[i]) && !(state.UseBackslashEscapes && sql[i] == HashChar))
                {
                    i++;
                }
            }

            var length = i - start;
            var captureNextPartOfName = false;
            var keepsNextNameRedaction = false;
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

                // IF [NOT] EXISTS can come between LOGIN or USER and its name (e.g. DROP USER IF EXISTS name).
                keepsNextNameRedaction = state.SanitizeNextNonKeywordToken && IsIfNotOrExists(sql.Slice(start, length));
                var redactsName = state.SanitizeNextNonKeywordToken && !keepsNextNameRedaction;

                if (redactsName)
                {
                    buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
                    state.RedactedLoginOrUserEndPosition = i;
                }
                else
                {
                    sql.Slice(start, length).CopyTo(buffer.Slice(state.SanitizedPosition));
                    state.SanitizedPosition += length;
                }

                // Keep identifier context even after summary collection stops, so quotes in later name parts
                // are not mistaken for the start of a string literal.
                if (state.CaptureNextNonKeywordTokenAsIdentifier)
                {
                    // A period at the end of the identifier (e.g. dbo.) is only included if another part of the name follows.
                    var endsWithDot = sql[i - 1] == DotChar;
                    var periodCount = endsWithDot
                        ? (IsNamePart(sql, SkipWhitespaceAndComments(sql, i, state.UseBackslashEscapes, state.AllowNestedComments)) ? 1 : 0)
                        : GetFollowingPeriodCount(sql, i, state.UseBackslashEscapes, state.AllowNestedComments);
                    captureNextPartOfName = periodCount > 0;

                    // The name of a login or user is left out of the summary as well as the sanitized SQL.
                    if (!state.SummaryIsComplete && !redactsName)
                    {
                        AppendSummaryNamePart(sql.Slice(start, endsWithDot ? length - 1 : length), ref state);

                        if (captureNextPartOfName)
                        {
                            // Keep one separator beyond the limit so an unfinished name is removed when trimming.
                            for (var period = 0; period < periodCount && state.SummaryPosition <= MaxSummaryLength; period++)
                            {
                                AppendSummaryChar(DotChar, ref state);
                            }
                        }
                        else
                        {
                            // Add a space after the identifier. The trailing space will be trimmed later.
                            AppendSummaryChar(SpaceChar, ref state);
                        }
                    }
                }
            }

            // The previous token has not been updated yet, as the words which precede a password are not keywords.
            if (length == ByWord.Length || length == ReplaceWord.Length)
            {
                SanitizeNextTokenAsPasswordIfPrecedesPassword(sql, start, length, ref state);
            }

            // Another statement starts, which does not have the names of the statement which precedes it.
            if ((state.InLoginOrUserStatement || state.InAlterTable) &&
                !state.SanitizeNextNonKeywordToken &&
                IsAnyWord(sql.Slice(start, length), StatementStartWords))
            {
                EndStatementContext(sql.Slice(start, length), ref state);
            }

            // LOGIN and USER are only matched as keywords along the keyword chain which builds the summary, which does not
            // reach every statement (e.g. one which follows another statement), so the words are also recognized here.
            var sanitizeNextToken = keepsNextNameRedaction ||
                (!state.SanitizeNextNonKeywordToken &&
                 length >= ToWord.Length &&
                 length <= LoginWord.Length &&
                 (sql[start] | 0x20) is 't' or 'l' or 'u' &&
                 StartsLoginOrUserName(sql, start, length, ref state));

            state.ParsePosition = i;
            state.CaptureNextNonKeywordTokenAsIdentifier = captureNextPartOfName;
            state.SanitizeNextNonKeywordToken = sanitizeNextToken;
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
            // Periods may occur when using schema-qualified identifiers, which are also captured
            // outside a FROM clause when the part before the period was captured (e.g. INSERT INTO [dbo].[Orders]).
            state.CaptureNextNonKeywordTokenAsIdentifier =
                (state.InFromClause && (currentChar is CommaChar or DotChar)) ||
                (state.CaptureNextNonKeywordTokenAsIdentifier && currentChar == DotChar);

            // A password is the next token, unless this is the start of a quoted one which is not terminated.
            if (currentChar is not (DoubleQuoteChar or BacktickChar or OpenSquareBracketChar))
            {
                state.SanitizeNextTokenAsPassword = false;
            }

            if (state.InLoginOrUserStatement)
            {
                ParseLoginOrUserStatementCharacter(sql, currentChar, ref state);
            }

            if (currentChar == SemicolonChar)
            {
                state.InAlterTable = false;
            }

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

    // Whether the word is one which can come between LOGIN or USER and its name (e.g. DROP USER IF EXISTS name).
    private static bool IsIfNotOrExists(ReadOnlySpan<char> word) =>
        IsWord(word, IfWord) || IsWord(word, NotWord) || IsWord(word, ExistsWord);

    private static bool IsWord(ReadOnlySpan<char> word, string expected) =>
        word.Length == expected.Length && word.Equals(expected.AsSpan(), StringComparison.OrdinalIgnoreCase);

    private static bool IsAnyWord(ReadOnlySpan<char> word, string[] expected)
    {
        for (var i = 0; i < expected.Length; i++)
        {
            if (IsWord(word, expected[i]))
            {
                return true;
            }
        }

        return false;
    }

    // Whether the LOGIN or USER which ends at the position, after ALTER or DROP, is the name of a column
    // (e.g. ALTER TABLE t ALTER login TYPE text or ALTER TABLE t DROP login, ADD c int), as it is followed by what follows
    // a column, instead of by the name of a login or user.
    private static bool IsColumnOperation(ReadOnlySpan<char> sql, int position, bool useBackslashEscapes, bool allowNestedComments)
    {
        position = SkipWhitespaceAndComments(sql, position, useBackslashEscapes, allowNestedComments);

        if (position >= sql.Length || sql[position] is CommaChar or SemicolonChar or CloseParenChar)
        {
            return true;
        }

        var end = position;
        while (end < sql.Length && IsWordChar(sql[end]))
        {
            end++;
        }

        return IsAnyWord(sql.Slice(position, end - position), ColumnOperationWords);
    }

    private static bool IsPreviousToken(ReadOnlySpan<char> sql, string expected, in ParseState state) =>
        IsWord(sql.Slice(state.PreviousTokenStartPosition, state.PreviousTokenEndPosition - state.PreviousTokenStartPosition), expected);

    // Whether the word at the position is one which is followed by the name of a login or user:
    // - LOGIN or USER after CREATE [OR REPLACE], ALTER or DROP (e.g. DROP USER name), unless it is the name of a column in ALTER TABLE.
    // - LOGIN after FOR, FROM or WITH in such a statement (e.g. CREATE USER name FOR LOGIN name).
    // - TO after RENAME in such a statement (e.g. ALTER USER name RENAME TO name).
    // It also remembers that the statement is an ALTER TABLE.
    private static bool StartsLoginOrUserName(ReadOnlySpan<char> sql, int start, int length, ref ParseState state)
    {
        var word = sql.Slice(start, length);

        if (IsWord(word, ToWord))
        {
            return state.InLoginOrUserStatement && IsPreviousToken(sql, RenameWord, in state);
        }

        if (IsWord(word, TableWord))
        {
            state.InAlterTable |= IsPreviousToken(sql, AlterWord, in state);
            return false;
        }

        var isLogin = IsWord(word, LoginWord);

        if (!isLogin && !IsWord(word, UserWord))
        {
            return false;
        }

        var isDrop = IsPreviousToken(sql, DropWord, in state);

        if (IsPreviousToken(sql, CreateWord, in state) ||
            IsCreateOrReplaceBefore(sql, in state) ||
            ((isDrop || IsPreviousToken(sql, AlterWord, in state)) &&
             !(state.InAlterTable && IsColumnOperation(sql, start + length, state.UseBackslashEscapes, state.AllowNestedComments))))
        {
            state.InLoginOrUserStatement = true;
            state.InDropLoginOrUserStatement |= isDrop;
            return true;
        }

        return isLogin &&
               state.InLoginOrUserStatement &&
               (IsPreviousToken(sql, ForWord, in state) || IsPreviousToken(sql, FromWord, in state) || IsPreviousToken(sql, WithWord, in state));
    }

    // Whether the previous token is the REPLACE of CREATE OR REPLACE.
    private static bool IsCreateOrReplaceBefore(ReadOnlySpan<char> sql, in ParseState state)
    {
        if (!IsPreviousToken(sql, ReplaceWord, in state))
        {
            return false;
        }

        var position = SkipWhitespaceBackwards(sql, state.PreviousTokenStartPosition);

        if (!EndsWithWord(sql, position, OrWord))
        {
            return false;
        }

        return EndsWithWord(sql, SkipWhitespaceBackwards(sql, position - OrWord.Length), CreateWord);
    }

    private static bool EndsWithWord(ReadOnlySpan<char> sql, int end, string expected) =>
        end >= expected.Length && IsWord(sql.Slice(end - expected.Length, expected.Length), expected);

    private static int SkipWhitespaceBackwards(ReadOnlySpan<char> sql, int position)
    {
        while (position > 0 && char.IsWhiteSpace(sql[position - 1]))
        {
            position--;
        }

        return position;
    }

    // The statement ends when another statement starts, which is not always marked with a semicolon.
    private static void EndStatementContext(ReadOnlySpan<char> word, ref ParseState state)
    {
        state.InLoginOrUserStatement = false;
        state.InDropLoginOrUserStatement = false;

        // ALTER and DROP are also clauses of ALTER TABLE.
        if (!IsWord(word, AlterWord) && !IsWord(word, DropWord))
        {
            state.InAlterTable = false;
        }
    }

    // Updates the statement which creates, alters or drops a login or user for a character which is a token on its own.
    private static void ParseLoginOrUserStatementCharacter(ReadOnlySpan<char> sql, char currentChar, ref ParseState state)
    {
        switch (currentChar)
        {
            case EqualsChar:
                // NAME = new_name and LOGIN = login_name (e.g. ALTER LOGIN name WITH NAME = new_name)
                state.SanitizeNextNonKeywordToken |= IsPreviousToken(sql, NameWord, in state) || IsPreviousToken(sql, LoginWord, in state);
                break;

            case CommaChar:
                // DROP USER name, name, but not a comma which is not after a name (e.g. in a statement which follows)
                state.SanitizeNextNonKeywordToken |= state.InDropLoginOrUserStatement &&
                    SkipWhitespaceBackwards(sql, state.ParsePosition) == state.RedactedLoginOrUserEndPosition;
                break;

            case SemicolonChar:
                state.InLoginOrUserStatement = false;
                state.InDropLoginOrUserStatement = false;
                state.SanitizeNextNonKeywordToken = false;
                break;

            default:
                break;
        }
    }

    // Redacts the word which is a password (e.g. the Tr0ub4dor of IDENTIFIED BY Tr0ub4dor), which can contain
    // the characters of an unquoted Oracle password ($ and #) as well as letters, digits and underscores.
    // Returns false if the token is not a word, in which case it is parsed as usual (e.g. as a quoted identifier).
    private static bool ParsePassword(ReadOnlySpan<char> sql, Span<char> buffer, ref ParseState state)
    {
        var start = state.ParsePosition;

        // A variable is not a password.
        if (!IsWordChar(sql[start]) || sql[start] == AtChar)
        {
            return false;
        }

        var end = start + 1;
        while (end < sql.Length && IsWordChar(sql[end]) && sql[end] != AtChar)
        {
            end++;
        }

        // A word directly followed by a quote is the prefix of a literal (e.g. N'password'), which is sanitized as one.
        if (end < sql.Length && sql[end] == SingleQuoteChar)
        {
            return false;
        }

        // IDENTIFIED BY VALUES 'hash' is followed by the hash of a password, which is a literal.
        if (end - start == ValuesWord.Length &&
            sql.Slice(start, ValuesWord.Length).Equals(ValuesWord.AsSpan(), StringComparison.OrdinalIgnoreCase) &&
            SkipWhitespaceAndComments(sql, end, state.UseBackslashEscapes, state.AllowNestedComments) is var next &&
            next < sql.Length &&
            sql[next] == SingleQuoteChar)
        {
            sql.Slice(start, ValuesWord.Length).CopyTo(buffer.Slice(state.SanitizedPosition));
            state.SanitizedPosition += ValuesWord.Length;
        }
        else
        {
            buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
            state.PasswordEndPosition = end;
        }

        state.ParsePosition = end;
        state.CaptureNextNonKeywordTokenAsIdentifier = false;
        state.SanitizeNextNonKeywordToken = false;
        state.SanitizeNextTokenAsPassword = false;
        state.PreviousTokenStartPosition = start;
        state.PreviousTokenEndPosition = end;

        return true;
    }

    // Whether the word at the position is one which is followed by a password:
    // - BY in IDENTIFIED BY (Oracle and MySQL), e.g. CREATE USER name IDENTIFIED BY password.
    // - REPLACE after the password of ALTER USER (Oracle), which is followed by the password which is replaced.
    // If the password is a literal (e.g. IDENTIFIED BY 'password'), it is sanitized as one. The passwords of MySQL
    // and MariaDB are always literals, and a word which follows IDENTIFIED BY is a modifier (e.g. RANDOM PASSWORD).
    private static void SanitizeNextTokenAsPasswordIfPrecedesPassword(ReadOnlySpan<char> sql, int start, int length, ref ParseState state)
    {
        if (state.UseBackslashEscapes)
        {
            return;
        }

        var word = sql.Slice(start, length);

        if (length == ByWord.Length)
        {
            // The previous token is the one which precedes BY, as it has not been updated for BY yet.
            var previousLength = state.PreviousTokenEndPosition - state.PreviousTokenStartPosition;

            if (word.Equals(ByWord.AsSpan(), StringComparison.OrdinalIgnoreCase) &&
                previousLength == IdentifiedWord.Length &&
                sql.Slice(state.PreviousTokenStartPosition, previousLength).Equals(IdentifiedWord.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                state.SanitizeNextTokenAsPassword = true;
            }
        }
        else if (state.PasswordEndPosition > 0 &&
                 word.Equals(ReplaceWord.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            // Only the first such word after a password can follow it, so it is not looked for again,
            // which would otherwise read the same comments again for each of them.
            var passwordEnd = state.PasswordEndPosition;
            state.PasswordEndPosition = 0;

            if (SkipWhitespaceAndComments(sql, passwordEnd, state.UseBackslashEscapes, state.AllowNestedComments) == start)
            {
                state.SanitizeNextTokenAsPassword = true;
            }
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

        if (state.RedactRemainder)
        {
            // Whether the bracket is a name or a list of values was not able to be worked out, so there is no way to tell
            // where it ends. Everything which follows could be in a string, so the rest of the statement is redacted.
            buffer[state.SanitizedPosition++] = sql[start];
            buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
            state.RedactRemainder = false;
            state.ParsePosition = sql.Length;
            state.CaptureNextNonKeywordTokenAsIdentifier = false;
            state.SanitizeNextNonKeywordToken = false;
            state.SanitizeNextTokenAsPassword = false;
            state.PreviousTokenStartPosition = start;
            state.PreviousTokenEndPosition = sql.Length;
            return true;
        }

        var length = end - start + 1;
        var captureNextPartOfName = false;

        // Redact the whole name after LOGIN or USER (e.g. CREATE LOGIN [DOMAIN\user]), or a password
        // (e.g. IDENTIFIED BY "pass word"), keeping its delimiters. A double-quoted value which starts
        // with a number is also redacted, because it is more likely to be a string literal (e.g. in
        // GoogleSQL, or SQL Server with QUOTED_IDENTIFIER OFF) than an identifier. An empty name has
        // nothing to redact, and the sanitized SQL must not be longer than the input.
        if (length > 2 &&
            (state.SanitizeNextNonKeywordToken ||
             state.SanitizeNextTokenAsPassword ||
             (sql[start] == DoubleQuoteChar && StartsWithNumber(sql.Slice(start + 1, length - 2).TrimStart()))))
        {
            buffer[state.SanitizedPosition++] = sql[start];
            buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
            buffer[state.SanitizedPosition++] = sql[end];

            if (state.SanitizeNextNonKeywordToken)
            {
                state.RedactedLoginOrUserEndPosition = end + 1;
            }

            if (state.SanitizeNextTokenAsPassword)
            {
                state.PasswordEndPosition = end + 1;
            }
        }
        else
        {
            sql.Slice(start, length).CopyTo(buffer.Slice(state.SanitizedPosition));
            state.SanitizedPosition += length;

            // Only bracketed identifiers are included in the summary, as they were before quoted
            // identifiers were parsed as a single token. They are included wherever an unquoted
            // identifier would be (e.g. after INTO or EXEC), as well as anywhere in a FROM clause.
            if ((state.InFromClause || state.CaptureNextNonKeywordTokenAsIdentifier) &&
                sql[start] == OpenSquareBracketChar)
            {
                // Name classification must continue independently of the summary length limit.
                var periodCount = GetFollowingPeriodCount(sql, end + 1, state.UseBackslashEscapes, state.AllowNestedComments);
                captureNextPartOfName = periodCount > 0;

                if (!state.SummaryIsComplete)
                {
                    AppendSummaryNamePart(sql.Slice(start, length), ref state);

                    if (captureNextPartOfName)
                    {
                        // Preserve omitted name parts, with one separator beyond the limit for incomplete-name trimming.
                        for (var period = 0; period < periodCount && state.SummaryPosition <= MaxSummaryLength; period++)
                        {
                            AppendSummaryChar(DotChar, ref state);
                        }
                    }
                    else
                    {
                        AppendSummaryChar(SpaceChar, ref state);
                    }
                }
            }
        }

        state.ParsePosition = end + 1;
        state.CaptureNextNonKeywordTokenAsIdentifier = captureNextPartOfName;
        state.SanitizeNextNonKeywordToken = false;
        state.SanitizeNextTokenAsPassword = false;
        state.PreviousTokenStartPosition = start;
        state.PreviousTokenEndPosition = end + 1;

        return true;
    }

    // The number of periods at the position, ignoring any whitespace and comments, if they are followed by another
    // part of a multi-part name which can be captured (e.g. the [Orders] in [dbo] . [Orders] or [dbo]./* c */[Orders]).
    // Consecutive periods allow omitted database and schema parts (e.g. [Server]...[Orders]). Longer runs in malformed
    // queries must also preserve identifier context so later literals are sanitized. Periods which are not followed
    // by a part of the name are left out of the summary, in which case zero is returned.
    private static int GetFollowingPeriodCount(ReadOnlySpan<char> sql, int position, bool useBackslashEscapes, bool allowNestedComments)
    {
        position = SkipWhitespaceAndComments(sql, position, useBackslashEscapes, allowNestedComments);

        if (position >= sql.Length || sql[position] != DotChar)
        {
            return 0;
        }

        var periodCount = 1;
        position = SkipWhitespaceAndComments(sql, position + 1, useBackslashEscapes, allowNestedComments);

        while (position < sql.Length && sql[position] == DotChar)
        {
            periodCount++;
            position = SkipWhitespaceAndComments(sql, position + 1, useBackslashEscapes, allowNestedComments);
        }

        return IsNamePart(sql, position) ? periodCount : 0;
    }

    // Whether the position is the start of a part of a name which can be captured.
    private static bool IsNamePart(ReadOnlySpan<char> sql, int position) =>
        position < sql.Length &&
        (sql[position] is OpenSquareBracketChar or UnderscoreChar || char.IsLetter(sql[position]));

    private static int SkipWhitespaceAndComments(ReadOnlySpan<char> sql, int position, bool useBackslashEscapes, bool allowNestedComments)
    {
        while (position < sql.Length)
        {
            if (sql[position] is SpaceChar or TabChar or CarriageReturnChar or NewLineChar)
            {
                position++;
                continue;
            }

            var commentEnd = FindCommentEnd(sql, position, useBackslashEscapes, allowNestedComments);

            if (commentEnd < 0)
            {
                break;
            }

            position = commentEnd;
        }

        return position;
    }

    /// <summary>
    /// Finds the end of the quoted identifier (<c>[...]</c>, <c>`...`</c>, or <c>"..."</c> unless the
    /// dialect uses it for a string literal) which starts at <paramref name="start"/>. GoogleSQL has no
    /// <c>[...]</c> identifiers, so there <c>[...]</c> is a list of values, which can contain <c>"..."</c> strings.
    /// </summary>
    /// <returns>
    /// The index of the closing delimiter, or -1 if there is no quoted identifier at <paramref name="start"/>,
    /// in which case the delimiter is parsed as a single character and what follows is parsed as usual.
    /// </returns>
    private static int FindQuotedIdentifierEnd(ReadOnlySpan<char> sql, int start, ref ParseState state) => sql[start] switch
    {
        OpenSquareBracketChar when !state.UseTripleQuotedLiterals => FindBracketedIdentifierEnd(sql, start, ref state),
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

        if (i >= searchEnd)
        {
            // SQLite has no limit to the length of an identifier, so a closing bracket which is further away could end one
            // if brackets only delimit identifiers. It is one if what is in it has no apostrophe, as that has no string
            // literal in it. If it has, where it ends is not known, as the bracket could be in a string. Any of the rest of
            // the statement could be part of it, so none of it is copied.
            if (state.BracketsDelimitIdentifiersOnly && i < sql.Length)
            {
                state.RedactRemainder = sql.Slice(start + 1, i - start - 1).IndexOf(SingleQuoteChar) >= 0;
                return i;
            }

            return -1;
        }

        // A bracket is never a subscript or the start of a list of values if the dialect only uses it for identifiers.
        if (!state.BracketsDelimitIdentifiersOnly && IsArraySubscriptOrConstructor(sql, start, in state))
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

            // A name which is going to be redacted is always treated as an identifier, as none of it is copied. A bracket
            // where a name is expected (e.g. after FROM) cannot be a list of values, so it is always a name, which ends at
            // the first closing bracket, rather than the content of it and the SQL after it being used to guess what it is.
            if (state.SanitizeNextNonKeywordToken || state.BracketsDelimitIdentifiersOnly || IsNameExpected(sql, start, in state))
            {
                return i;
            }

            if (IsBracketedIdentifier(sql, start + 1, i - start - 1, state.UseBackslashEscapes, state.AllowNestedComments, ref state.BracketLookaheadLength, out var unresolved))
            {
                // If it is not known what the content is, where it ends is not known either, as the bracket could be in a
                // string. Any of the rest of the statement could be part of it, so none of it is copied.
                state.RedactRemainder = unresolved;
                return i;
            }

            return -1;
        }

        return -1;

        static bool IsNameExpected(ReadOnlySpan<char> sql, int start, in ParseState state)
        {
            if (state.CaptureNextNonKeywordTokenAsIdentifier)
            {
                return true;
            }

            // The previous token is a word which is followed by the name of something (e.g. FROM, UPDATE or INTO), with only
            // whitespace between it and the bracket. A comment, a literal or any other character is not part of a token which
            // is the previous one, so a word in a comment (e.g. -- from) is never mistaken for the one before the bracket.
            // The tokens which have been parsed are used, as not all of these words are parsed as keywords (e.g. FROM after DELETE).
            var wordStart = state.PreviousTokenStartPosition;
            var wordEnd = state.PreviousTokenEndPosition;

            if (wordEnd <= wordStart || wordEnd > start)
            {
                return false;
            }

            for (var i = wordEnd; i < start; i++)
            {
                if (!char.IsWhiteSpace(sql[i]))
                {
                    return false;
                }
            }

            return IsNameKeyword(sql.Slice(wordStart, wordEnd - wordStart));
        }

        static bool IsBracketedIdentifier(ReadOnlySpan<char> sql, int contentStart, int contentLength, bool useBackslashEscapes, bool allowNestedComments, ref int lookaheadLength, out bool unresolved)
        {
            unresolved = false;

            // Brackets can also delimit a list of values in some dialects (e.g. ['a', 'b'] or [1, 2] in
            // DuckDB, ClickHouse and GoogleSQL). An odd number of quotes cannot all be part of string
            // literals, so that content is an identifier (e.g. [Manager's Approval]), unless it starts with
            // a quote, when the bracket which ends it is inside a string literal (e.g. ['a]b', 'c']).
            // Content without quotes is an identifier unless it starts with a number (e.g. [1, 2]).
            // Anything else is parsed as SQL, which redacts any literals and at worst over-redacts an
            // identifier (e.g. [2024] or [It's Bob's]).
            var content = sql.Slice(contentStart, contentLength).TrimStart();

            // A string in dollar quotes (e.g. [$$a$$, $$b$$] or [Email, $tag$a$tag$]) is a value, which would be copied
            // if the content were an identifier. The first bracket which is found can be in a comment which follows it.
            if (content.IsEmpty)
            {
                return false;
            }

            // Content which starts with a quote is never an identifier (a string in dollar quotes in it is a value too), as
            // the bracket which ends it can be inside a string literal (e.g. ['a]b', 'c']), so none of the checks are needed.
            if (content[0] == SingleQuoteChar)
            {
                return false;
            }

            // Content with no quote, dollar sign or comment marker in it (e.g. [Orders] or [o].[Id]) is a name unless it starts
            // with a number, so none of the checks which follow are needed, and most brackets are of this kind.
            if (!HasQuoteDollarOrCommentChar(content))
            {
                return !StartsWithNumber(content);
            }

            if (ContainsDollarQuotedString(content))
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

            // Content which starts with a comment, a prefixed string (e.g. E'a', N'a', B'1' or X'1F') or a number which has
            // quotes after it could be the start of a name (e.g. [--Orders], [N'Souza] or [2024's Reports]), or it could
            // be the start of a list in which the first bracket which is found is in the comment or string (e.g. [ /* ] */ 'a'],
            // [E'a]b', 'c'] or [1, 'a]b']). The bracket which ends a list is the first one which is not in a comment or
            // string, so it is a list if there is one, and otherwise the rest of the SQL is not part of the content.
            if (StartsWithComment(content) ||
                (singleQuotes > 0 && StartsWithNumberLiteral(content)) ||
                ((singleQuotes & 1) == 1 && StartsWithPrefixedString(content)))
            {
                var hasClosingBracket = HasClosingBracketOutsideLiteralsAndComments(sql, contentStart, useBackslashEscapes, allowNestedComments, ref lookaheadLength);

                // If the limit on how much is read is reached, it is not known if it is a name or a list of values.
                unresolved = hasClosingBracket is null;
                return hasClosingBracket != true;
            }

            // The content is also an identifier if a comment in it would contain the closing bracket, which is not
            // valid for a list of values, unless the content has a value which would be copied if it were an identifier.
            // The first bracket which is found can be in a comment which ends later in a list (e.g. ['a' /* ] */, 'b']).
            return singleQuotes == 0
                ? !StartsWithNumber(content)
                : content[0] != SingleQuoteChar &&
                  ((singleQuotes & 1) == 1 || (CommentContainsClosingBracket(content) && !ContainsValue(content)));
        }

        static bool StartsWithComment(ReadOnlySpan<char> content)
        {
            return content.Length >= 2 &&
                ((content[0] == ForwardSlashChar && content[1] == AsteriskChar) || (content[0] == DashChar && content[1] == DashChar));
        }

        static bool ContainsDollarQuotedString(ReadOnlySpan<char> content)
        {
            // Most content has no dollar sign, which is much cheaper to find than looking at each character.
            if (content.IndexOf(DollarChar) < 0)
            {
                return false;
            }

            // A string in dollar quotes is a value when it starts the content, or follows a comma, an opening parenthesis or an
            // operator (e.g. [Email, fn($$a$$)]), with any comments before it skipped. A dollar sign which follows a word, a
            // quote or a closing parenthesis is part of a name (e.g. [Price $$x$$ O'Brien], [Orders'$$] or [Price$USD$]),
            // so that the text which follows it is not parsed as a string or a comment.
            var previous = '\0';

            for (var i = 0; i < content.Length; i++)
            {
                var c = content[i];

                if (char.IsWhiteSpace(c))
                {
                    continue;
                }

                if (c == DashChar && i + 1 < content.Length && content[i + 1] == DashChar)
                {
                    var lineEnd = content.Slice(i + 2).IndexOfAny(CarriageReturnChar, NewLineChar);
                    if (lineEnd < 0)
                    {
                        return false;
                    }

                    i += 2 + lineEnd;
                }
                else if (c == ForwardSlashChar && i + 1 < content.Length && content[i + 1] == AsteriskChar)
                {
                    var commentEnd = content.Slice(i + 2).IndexOf("*/".AsSpan(), StringComparison.Ordinal);
                    if (commentEnd < 0)
                    {
                        return false;
                    }

                    i += 2 + commentEnd + 1;
                }
                else
                {
                    if (c == DollarChar &&
                        !IsWordChar(previous) &&
                        previous is not (SingleQuoteChar or DoubleQuoteChar or BacktickChar or DollarChar or CloseParenChar) &&
                        TryFindDollarQuotedLiteralEnd(content, i, out _))
                    {
                        return true;
                    }

                    previous = c;
                }
            }

            return false;
        }

        static bool? HasClosingBracketOutsideLiteralsAndComments(ReadOnlySpan<char> sql, int start, bool useBackslashEscapes, bool allowNestedComments, ref int lookaheadLength)
        {
            // Reading the rest of the statement for each bracket could take time which grows with the square of its length,
            // so there is a limit on the total number of characters read. If it is reached it is not known whether there is
            // a bracket, which is the result of null.
            var limit = Math.Min(sql.Length, start + Math.Max(MaxBracketLookaheadLength - lookaheadLength, 0));
            var i = start;
            var found = false;

            for (; i < limit; i++)
            {
                int end;

                switch (sql[i])
                {
                    case CloseSquareBracketChar:
                        found = true;
                        break;

                    case SingleQuoteChar:
                        end = FindStringLiteralEnd(sql, i, useBackslashEscapes, useTripleQuotedLiterals: false, out _);
                        if (end < 0)
                        {
                            // The string is not terminated, so nothing after it can be outside a string.
                            lookaheadLength += sql.Length - start;
                            return false;
                        }

                        i = end;
                        continue;

                    case DashChar:
                    case ForwardSlashChar:
                    case HashChar:
                        end = FindCommentEnd(sql, i, useBackslashEscapes, allowNestedComments);
                        if (end > i)
                        {
                            i = end - 1;
                        }

                        continue;

                    case DollarChar:
                        if (TryFindDollarQuotedLiteralEnd(sql, i, out end))
                        {
                            i = end - 1;
                        }

                        continue;

                    default:
                        continue;
                }

                break;
            }

            lookaheadLength += Math.Min(i, sql.Length) - start;
            return found ? true : (limit < sql.Length && i >= limit) ? null : false;
        }

        static bool StartsWithNumberLiteral(ReadOnlySpan<char> content)
        {
            // A number (e.g. 1234, -1.5, 1e5 or 0x1F) that ends where the content does, or at a character which is not part
            // of a word, which tells it from a name that starts with a number (e.g. [1st Manager's Requests]). The first
            // bracket which is found can be in a string after the number (e.g. [1, 'a]b']).
            var i = content[0] is '+' or DashChar ? 1 : 0;

            if (i < content.Length && content[i] == DotChar)
            {
                i++;
            }

            if (i >= content.Length || !char.IsAsciiDigit(content[i]))
            {
                return false;
            }

            while (i < content.Length && (char.IsAsciiHexDigit(content[i]) || content[i] is DotChar or 'x' or 'X'))
            {
                i++;
            }

            return i == content.Length || !IsWordChar(content[i]);
        }

        static bool StartsWithPrefixedString(ReadOnlySpan<char> content)
        {
            // A string with a prefix (e.g. E'a', N'a', B'1' or X'1F'), where the first bracket which is found can be in the string.
            return content.Length >= 2 && content[1] == SingleQuoteChar && content[0] is 'E' or 'e' or 'N' or 'n' or 'B' or 'b' or 'X' or 'x';
        }

        static bool ContainsValue(ReadOnlySpan<char> content)
        {
            // Whether the content has a string literal which is not empty, or a number. An identifier such as
            // [Orders''--] has quotes, but only ones which are empty, while a list of values has the values.
            for (var i = 0; i < content.Length; i++)
            {
                var c = content[i];

                if (c == SingleQuoteChar)
                {
                    // A quote which follows a word is part of a name (e.g. [Orders''--]), unless the word is the prefix of a literal (e.g. N'a').
                    if (i > 0 &&
                        IsWordChar(content[i - 1]) &&
                        !(content[i - 1] is 'E' or 'e' or 'N' or 'n' or 'B' or 'b' or 'X' or 'x' && (i == 1 || !IsWordChar(content[i - 2]))))
                    {
                        while (i + 1 < content.Length && content[i + 1] == SingleQuoteChar)
                        {
                            i++;
                        }

                        continue;
                    }

                    var start = i + 1;
                    var end = start;

                    while (end < content.Length)
                    {
                        if (content[end] == SingleQuoteChar)
                        {
                            // A doubled quote is a quote in the literal.
                            if (end + 1 < content.Length && content[end + 1] == SingleQuoteChar)
                            {
                                end += 2;
                                continue;
                            }

                            break;
                        }

                        end++;
                    }

                    if (end > start)
                    {
                        return true;
                    }

                    i = end;
                }
                else if (c is DashChar or ForwardSlashChar && i + 1 < content.Length)
                {
                    // Anything in a comment (e.g. [Orders/*'']) is not a value.
                    if (c == DashChar && content[i + 1] == DashChar)
                    {
                        var lineEnd = content.Slice(i).IndexOfAny(CarriageReturnChar, NewLineChar);
                        i = lineEnd < 0 ? content.Length : i + lineEnd;
                    }
                    else if (c == ForwardSlashChar && content[i + 1] == AsteriskChar)
                    {
                        var blockEnd = content.Slice(i + 2).IndexOf("*/".AsSpan());
                        i = blockEnd < 0 ? content.Length : i + 2 + blockEnd + 1;
                    }
                }
                else if (char.IsAsciiDigit(c) && (i == 0 || !IsWordChar(content[i - 1])))
                {
                    return true;
                }
            }

            return false;
        }

        static bool CommentContainsClosingBracket(ReadOnlySpan<char> content)
        {
            // A list of values has to end with its closing bracket, so a comment which has not ended by the
            // time the content does would contain it, which is not valid. The content must therefore be an
            // identifier (e.g. [Orders''--] or [Orders''/*]), which otherwise would be parsed as SQL with
            // the rest of the statement, including any literal in it, swallowed by the comment.
            // A comment starts with two dashes or a slash and an asterisk, so content which has no dash and
            // no slash cannot contain one, which is much cheaper to find than looking at each character.
            if (content.IndexOfAny(DashChar, ForwardSlashChar) < 0)
            {
                return false;
            }

            for (var i = 0; i < content.Length; i++)
            {
                var c = content[i];

                if (c == SingleQuoteChar)
                {
                    // Skip a string literal, in which a comment marker is only text.
                    for (i++; i < content.Length; i++)
                    {
                        if (content[i] != SingleQuoteChar)
                        {
                            continue;
                        }

                        if (i + 1 < content.Length && content[i + 1] == SingleQuoteChar)
                        {
                            i++;
                            continue;
                        }

                        break;
                    }
                }
                else if (c == DashChar && i + 1 < content.Length && content[i + 1] == DashChar)
                {
                    // A line comment ends at the end of the line, if the line does end within the content.
                    var lineEnd = content.Slice(i + 2).IndexOfAny(CarriageReturnChar, NewLineChar);
                    if (lineEnd < 0)
                    {
                        return true;
                    }

                    i += 2 + lineEnd;
                }
                else if (c == ForwardSlashChar && i + 1 < content.Length && content[i + 1] == AsteriskChar)
                {
                    var commentEnd = content.Slice(i + 2).IndexOf("*/".AsSpan(), StringComparison.Ordinal);
                    if (commentEnd < 0)
                    {
                        return true;
                    }

                    i += 2 + commentEnd + 1;
                }
            }

            return false;
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

    private static bool IsNameKeyword(ReadOnlySpan<char> word)
    {
        // The words which are followed by the name of something, so are not followed by a list of values.
        // They are found by length and first letter, as most of the words which come before a bracket are none of them.
        if (word.IsEmpty)
        {
            return false;
        }

        var keyword = (word.Length, word[0] | 0x20) switch
        {
            (4, 'f') => "from",
            (4, 'i') => "into",
            (4, 'j') => "join",
            (4, 'v') => "view",
            (4, 'e') => "exec",
            (5, 't') => "table",
            (5, 'i') => "index",
            (6, 'u') => "update",
            (6, 'd') => "delete",
            (6, 's') => "schema",
            (7, 'e') => "execute",
            (7, 't') => "trigger",
            (8, 't') => "truncate",
            (8, 'f') => "function",
            (8, 'd') => "database",
            (8, 's') => "sequence",
            (9, 'p') => "procedure",
            _ => null,
        };

        return keyword is not null && word.Equals(keyword.AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasQuoteDollarOrCommentChar(ReadOnlySpan<char> content)
    {
        // Whether the content has any character which a quote, a dollar quoted string or a comment starts with.
#if NET
        return content.IndexOfAny(QuoteDollarOrCommentSearchValues) >= 0;
#else
        foreach (var c in content)
        {
            if (c is SingleQuoteChar or DollarChar or DashChar or ForwardSlashChar)
            {
                return true;
            }
        }

        return false;
#endif
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
        var commentEnd = FindCommentEnd(sql, state.ParsePosition, state.UseBackslashEscapes, state.AllowNestedComments);

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
    private static int FindCommentEnd(ReadOnlySpan<char> sql, int start, bool useBackslashEscapes, bool allowNestedComments)
    {
        var ch = sql[start];
        var next = start + 1;

        // Scan past multi-line comment
        if (ch == ForwardSlashChar && next < sql.Length && sql[next] == AsteriskChar)
        {
            // SQL Server, PostgreSQL and Db2 allow block comments to be nested, so a comment such as
            // /* /* */ don't */ only ends at the second "*/". MySQL/MariaDB, SQLite, Oracle and others do not,
            // so the comment would end at the first "*/", and treating it as still open would read the
            // literals which follow it as part of the comment, or as if they were outside of it.
            return FindMultiLineCommentEnd(sql, start, allowNesting: allowNestedComments && !useBackslashEscapes);
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

        // MySQL/MariaDB and GoogleSQL (the same dialects for which useBackslashEscapes is set) also treat a
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
            ? FindStringLiteralEnd(sql, state.ParsePosition, state.UseBackslashEscapes, state.UseTripleQuotedLiterals, out prefixLength)
            : FindQuotedLiteralEnd(sql, state.ParsePosition, delimiter, state.UseBackslashEscapes, state.UseTripleQuotedLiterals);

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
    /// <param name="useTripleQuotedLiterals">Whether the dialect has triple-quoted string literals.</param>
    /// <param name="prefixLength">
    /// The number of characters which precede the opening quote and are part of the literal
    /// (e.g. the <c>N</c> of the Unicode literal <c>N'foo'</c>).
    /// </param>
    /// <returns>The index of the closing quote, or -1 if the literal is not terminated.</returns>
    private static int FindStringLiteralEnd(ReadOnlySpan<char> sql, int quotePosition, bool useBackslashEscapes, bool useTripleQuotedLiterals, out int prefixLength)
    {
        prefixLength = 0;

        if (quotePosition == 0)
        {
            return FindQuotedLiteralEnd(sql, quotePosition, SingleQuoteChar, useBackslashEscapes, useTripleQuotedLiterals);
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
                return FindQuotedLiteralEnd(sql, quotePosition, SingleQuoteChar, useBackslashEscapes: true, useTripleQuotedLiterals: false);
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

        return FindQuotedLiteralEnd(sql, quotePosition, SingleQuoteChar, useBackslashEscapes, useTripleQuotedLiterals);

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
    private static int FindQuotedLiteralEnd(ReadOnlySpan<char> sql, int quotePosition, char delimiter, bool useBackslashEscapes, bool useTripleQuotedLiterals)
    {
        // GoogleSQL also has triple-quoted literals ('''...''' and """..."""), in which a quote does not
        // have to be escaped (e.g. '''it's'''). Reading the opening quotes as a quote followed by an escaped
        // quote would end the literal at the first quote inside it, and the rest of the value would be copied
        // into the sanitized SQL verbatim. This is gated on the dialect because in the other dialects ''' is
        // the start of a literal whose first character is an escaped quote.
        if (useTripleQuotedLiterals &&
            quotePosition + 2 < sql.Length &&
            sql[quotePosition + 1] == delimiter &&
            sql[quotePosition + 2] == delimiter)
        {
            return FindTripleQuotedLiteralEnd(sql, quotePosition, delimiter, useBackslashEscapes);
        }

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

    /// <summary>
    /// Finds the end of the triple-quoted literal whose opening delimiters start at <paramref name="quotePosition"/>,
    /// which is the next three delimiters in a row of which the first is not escaped.
    /// </summary>
    /// <returns>The index of the last closing delimiter, or -1 if the literal is not terminated.</returns>
    private static int FindTripleQuotedLiteralEnd(ReadOnlySpan<char> sql, int quotePosition, char delimiter, bool useBackslashEscapes)
    {
        var searchPos = quotePosition + 3;
        while (searchPos < sql.Length)
        {
            var quoteIndex = sql.Slice(searchPos).IndexOf(delimiter);
            if (quoteIndex < 0)
            {
                break;
            }

            searchPos += quoteIndex;

            if (searchPos + 2 < sql.Length &&
                sql[searchPos + 1] == delimiter &&
                sql[searchPos + 2] == delimiter &&
                !(useBackslashEscapes && IsBackslashEscaped(sql, searchPos, quotePosition)))
            {
                return searchPos + 2;
            }

            searchPos += 1;
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
        // A string in dollar quotes which directly follows a literal (e.g. $$a$$$$b$$) is not part of a name.
        if (sql[state.ParsePosition] != DollarChar ||
            !TryFindDollarQuotedLiteralEnd(sql, state.ParsePosition, out var literalEnd, ignoreLookBehind: state.ParsePosition == state.LastLiteralEnd))
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
    /// <param name="sql">The SQL statement.</param>
    /// <param name="start">The position of the opening dollar sign.</param>
    /// <param name="literalEnd">The index after the literal, if there is one.</param>
    /// <param name="ignoreLookBehind">
    /// <see langword="true"/> if the character before <paramref name="start"/> is known to be the end of a literal,
    /// and so is not part of the name of an identifier or variable.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a dollar-quoted string literal starts at <paramref name="start"/>, in which
    /// case <paramref name="literalEnd"/> is the index after it, or the length of the input if it is not
    /// terminated; otherwise <see langword="false"/>.
    /// </returns>
    private static bool TryFindDollarQuotedLiteralEnd(ReadOnlySpan<char> sql, int start, out int literalEnd, bool ignoreLookBehind = false)
    {
        // PostgreSQL dollar-quoted string: $tag$...$tag$ (the tag is optional, so $$...$$ is valid).
        // The body between the delimiters is a literal with no escaping, so it must be redacted.
        literalEnd = -1;

        if (sql[start] != DollarChar)
        {
            return false;
        }

        // A '$' which directly follows a letter, a digit, an underscore or another '$' is part of an identifier
        // or variable name (e.g. price$gbp$ or DR$idx$I), in PostgreSQL as well as in the other dialects handled here,
        // so it does not open a dollar-quoted string. Treating it as one would skip to the next occurrence of the
        // same delimiter, which may be inside a string literal, after which literals would be read as code.
        // Other characters can precede a dollar-quoted string, as '@' and '#' are operators in PostgreSQL.
        // A surrogate is part of a letter which is not in the basic plane, which PostgreSQL also allows in a name.
        if (!ignoreLookBehind &&
            start > 0 &&
            (char.IsLetterOrDigit(sql[start - 1]) || char.IsSurrogate(sql[start - 1]) || sql[start - 1] is UnderscoreChar or DollarChar))
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
            // A scan which found no closing parenthesis has looked at everything up to the end of the input, so
            // any later list is within the same text and would not find one either. Without remembering that, a
            // statement with many lists which are not terminated would scan to the end of the input for each of them.
            if (state.InClauseEndNotFoundFrom != 0 && parsePosition + 1 >= state.InClauseEndNotFoundFrom)
            {
                return false;
            }

            if (TryFindEndOfInClause(sql, parsePosition, in state, out var closeParenIndex))
            {
                state.ParsePosition = closeParenIndex;
                buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
                return true;
            }

            if (state.InClauseEndNotFoundFrom == 0)
            {
                state.InClauseEndNotFoundFrom = parsePosition + 1;
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
        var literalEnd = -1;

        while (i < length)
        {
#if NET
            var offset = sql.Slice(i).IndexOfAny(InClauseScanSearchValues);
#else
            var offset = IndexOfInClauseScanChar(sql.Slice(i));
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
                    end = FindStringLiteralEnd(sql, i, state.UseBackslashEscapes, state.UseTripleQuotedLiterals, out _);
                    i = end < 0 ? length : end + 1;
                    literalEnd = i;
                    break;

                case DoubleQuoteChar when state.UseBackslashEscapes:
                    end = FindQuotedLiteralEnd(sql, i, DoubleQuoteChar, useBackslashEscapes: true, state.UseTripleQuotedLiterals);
                    i = end < 0 ? length : end + 1;
                    literalEnd = i;
                    break;

                case DoubleQuoteChar:
                case BacktickChar:
                case OpenSquareBracketChar:
                    // A delimiter which does not start a quoted identifier is parsed as a single character.
                    end = FindQuotedIdentifierEnd(sql, i, ref scanState);

                    // It is not known where an identifier ends if the rest of the statement is to be redacted (as the main
                    // parser does), so no closing parenthesis can be relied on, which is what is searched for.
                    if (scanState.RedactRemainder)
                    {
                        closeParenIndex = -1;
                        return false;
                    }

                    i = end < 0 ? i + 1 : end + 1;
                    break;

                case DollarChar:
                    i = TryFindDollarQuotedLiteralEnd(sql, i, out end, ignoreLookBehind: i == literalEnd) ? end : i + 1;
                    literalEnd = i;
                    break;

                // DashChar, ForwardSlashChar or HashChar
                default:
                    end = FindCommentEnd(sql, i, state.UseBackslashEscapes, state.AllowNestedComments);
                    i = end < 0 ? i + 1 : end;
                    break;
            }
        }

        closeParenIndex = -1;
        return false;
    }

#if !NET
    /// <summary>
    /// Finds the first character which can start a construct that a <c>)</c> may legitimately appear inside, or which is
    /// <c>)</c> itself (see <see cref="TryFindEndOfInClause"/>).
    /// </summary>
    /// <remarks>
    /// This is not <c>IndexOfAny</c> as that searches the whole of the span once for each value on .NET Framework, so for
    /// a value which does not occur it takes time in proportion to the length of the span rather than to the distance to the
    /// first match, and calling it for each of a number of literals in a long statement takes time which grows with its square.
    /// </remarks>
    private static int IndexOfInClauseScanChar(ReadOnlySpan<char> span)
    {
        for (var i = 0; i < span.Length; i++)
        {
            if (span[i] is CloseParenChar or SingleQuoteChar or DoubleQuoteChar or BacktickChar or OpenSquareBracketChar
                or DollarChar or DashChar or ForwardSlashChar or HashChar)
            {
                return i;
            }
        }

        return -1;
    }
#endif

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

        // These track where the last name (which may have several parts) captured in the summary starts and ends.
        public int LastNameStartPosition; // 4 bytes
        public int LastNameEndPosition; // 4 bytes

        // The first ']' found at or after CloseSquareBracketSearchStart, or the length of the input if there is none.
        public int CloseSquareBracketSearchStart; // 4 bytes
        public int NextCloseSquareBracket; // 4 bytes

        // One more than the position from which the end of an IN clause was searched for and not found, or zero if there was no such search.
        public int InClauseEndNotFoundFrom; // 4 bytes

        // The number of characters which have been read in total looking for the bracket which ends a list of values.
        public int BracketLookaheadLength; // 4 bytes

        // The position after the most recent password which was sanitized, or zero if there was none.
        public int PasswordEndPosition; // 4 bytes

        // The position after the most recent literal which was sanitized in the main loop, or zero if there was none.
        public int LastLiteralEnd; // 4 bytes

        // NOTE: If the number of bool fields increases significantly, consider combining into a bitfield.

        public bool CaptureNextNonKeywordTokenAsIdentifier; // 1 byte

        // Set while finding the end of a bracket which could be a name or a list of values, if which could not be worked out.
        public bool RedactRemainder; // 1 byte

        public bool SanitizeNextNonKeywordToken; // 1 byte

        // Set after a word such as IDENTIFIED BY, which is followed by a password which is not a literal.
        public bool SanitizeNextTokenAsPassword; // 1 byte

        // Set from the name of a login or user which is redacted until the end of the statement, so that its other names are too.
        public bool InLoginOrUserStatement; // 1 byte

        // Set if the statement which drops a login or user can list more than one of them.
        public bool InDropLoginOrUserStatement; // 1 byte

        // The position after the most recent name of a login or user which was redacted, or zero if there was none.
        public int RedactedLoginOrUserEndPosition; // 4 bytes

        // Set from ALTER TABLE until the end of the statement, in which ALTER and DROP can be followed by the name of a column.
        public bool InAlterTable; // 1 byte

        /// <summary>
        /// Whether the source dialect treats a backslash as a string-literal escape character
        /// (MySQL/MariaDB and GoogleSQL). Controls whether <c>\'</c> is recognized as an escaped quote, and the
        /// other dialect differences described by <see cref="GetSanitizedSql(string?, bool, bool, bool, bool)"/>.
        /// </summary>
        public bool UseBackslashEscapes; // 1 byte

        /// <summary>
        /// Whether the source dialect has triple-quoted string literals (GoogleSQL).
        /// </summary>
        public bool UseTripleQuotedLiterals; // 1 byte

        /// <summary>
        /// Whether the source dialect allows block comments to be nested (SQL Server, PostgreSQL and Db2).
        /// </summary>
        public bool AllowNestedComments; // 1 byte

        /// <summary>
        /// Whether the source dialect only uses <c>[...]</c> to delimit an identifier (SQL Server and SQLite).
        /// </summary>
        public bool BracketsDelimitIdentifiersOnly; // 1 byte

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
        public SqlDialect Dialect;
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
            SqlKeyword.Truncate => state.FirstSummaryKeyword is SqlKeyword.Truncate,
            SqlKeyword.Table => state.FirstSummaryKeyword is SqlKeyword.Create or SqlKeyword.Alter or SqlKeyword.Drop or SqlKeyword.Truncate,
            SqlKeyword.Database or
            SqlKeyword.Function or
            SqlKeyword.Index or
            SqlKeyword.Procedure or
            SqlKeyword.Role or
            SqlKeyword.Schema or
            SqlKeyword.Sequence or
            SqlKeyword.View => state.FirstSummaryKeyword is SqlKeyword.Create or SqlKeyword.Alter or SqlKeyword.Drop,
            _ => false,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SanitizeNextToken(in ParseState state, SqlKeyword currentKeyword) => currentKeyword switch
        {
            SqlKeyword.Login or SqlKeyword.User => state.PreviousParsedKeyword?.SqlKeyword is SqlKeyword.Create or SqlKeyword.Alter or SqlKeyword.Drop,

            // IF [NOT] EXISTS can come between LOGIN or USER and its name.
            SqlKeyword.If or SqlKeyword.Not or SqlKeyword.Exists => state.SanitizeNextNonKeywordToken,
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
