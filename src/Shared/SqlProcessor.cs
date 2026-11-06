// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Instrumentation;

internal static partial class SqlProcessor
{
    private const int MaxSummaryLength = 255;
    private const int MaxBracketedIdentifierLength = 128;

    // The most characters which are read in total, looking for the bracket which ends a list of values, so that
    // many brackets which could start one (e.g. [--a] or [N'a]) do not each read to the end of the statement.
    private const int MaxBracketLookaheadLength = 16 * 1024;
    private const int CacheCapacity = 1000;

    // The number of combinations of the values of SqlDialect, which is one more than the one which has all of them.
    private const int DialectCount = (int)SqlDialect.All + 1;

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

    // The sanitized statements of each dialect, which are the index of the cache. A cache is created when it is first used, as
    // there are many combinations of the dialects but a process uses few of them.
    private static readonly ConcurrentDictionary<string, SqlStatementInfo>?[] Caches = new ConcurrentDictionary<string, SqlStatementInfo>?[DialectCount];

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

#if NET9_0_OR_GREATER
    private static readonly SearchValues<string> AccountWords = SearchValues.Create(["user", "login", "identified", "password"], StringComparison.OrdinalIgnoreCase);
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

    /// <summary>
    /// The differences between the dialects of SQL which are sanitized. A statement is cached per combination of them.
    /// <para/>
    /// The default is the dialect of PostgreSQL and Db2, and of the databases which are not known: block comments nest,
    /// a double-quoted value is an identifier, <c>[...]</c> can be an identifier, a list of values or the subscript of an
    /// array, and the words around a string with a prefix (e.g. <c>E'...'</c> or <c>q'[...]'</c>), a string in dollar quotes
    /// and a password which is a word are all recognized. Each flag is a difference from it, and the dialects of the
    /// databases which are known have the combinations of them which they need, so a preset is used rather than a flag on
    /// its own (e.g. <see cref="BackslashEscapes"/> does not make comments end at the first <c>*/</c>, as MySQL does).
    /// </summary>
    [Flags]
    internal enum SqlDialect
    {
        /// <summary>
        /// The dialect of PostgreSQL and Db2, and of the databases which are not known.
        /// </summary>
        Default = 0,

        /// <summary>
        /// A backslash is a string-literal escape character (e.g. <c>'it\'s'</c>).
        /// </summary>
        BackslashEscapes = 1,

        /// <summary>
        /// A double-quoted (<c>"..."</c>) value is a string literal rather than a quoted identifier.
        /// </summary>
        DoubleQuotedStrings = 2,

        /// <summary>
        /// <c>#</c> starts a comment.
        /// </summary>
        HashComments = 4,

        /// <summary>
        /// <c>'''...'''</c> and <c>"""..."""</c> are also string literals, in which a quote does not have to be escaped.
        /// </summary>
        TripleQuotedLiterals = 8,

        /// <summary>
        /// A block comment ends at the first <c>*/</c>, instead of being allowed to nest.
        /// </summary>
        NonNestedComments = 16,

        /// <summary>
        /// <c>[...]</c> always delimits an identifier (even if it has an apostrophe, such as <c>['Tis]</c>), so that it is never
        /// a list of values or the subscript of an array as in some other dialects (e.g. <c>['a', 'b']</c> or <c>items[1]</c>).
        /// </summary>
        BracketsDelimitIdentifiersOnly = 32,

        /// <summary>
        /// A closing bracket in an identifier in <c>[...]</c> is not escaped by another one (<c>]]</c>).
        /// </summary>
        NoBracketEscape = 64,

        /// <summary>
        /// There are no strings in dollar quotes, so that <c>$$</c> is not the start of one (e.g. <c>$$DEBUG</c> of Oracle).
        /// </summary>
        NoDollarQuotedStrings = 128,

        /// <summary>
        /// There are no strings with the prefixes of PostgreSQL (<c>E'...'</c>) and Oracle (<c>q'[...]'</c>).
        /// </summary>
        NoStringPrefixes = 256,

        /// <summary>
        /// A password is always a literal, so that a word which follows <c>IDENTIFIED BY</c> or <c>PASSWORD =</c> is not one.
        /// </summary>
        NoBareWordPasswords = 512,

        /// <summary>
        /// A backslash escapes a backtick within a quoted identifier.
        /// </summary>
        BackslashEscapedIdentifiers = 1024,

        /// <summary>
        /// An alternative quoted string can use whitespace or an apostrophe as its delimiter.
        /// </summary>
        UnrestrictedAlternativeQuoteDelimiters = 2048,

        /// <summary>
        /// Comments follow MySQL rules: <c>--</c> requires following whitespace or a control character,
        /// only a line feed terminates a line comment, and executable comments have version-dependent boundaries.
        /// </summary>
        MySqlComments = 4096,

        /// <summary>
        /// MySQL and MariaDB.
        /// </summary>
        MySql = BackslashEscapes | DoubleQuotedStrings | HashComments | NonNestedComments |
                NoDollarQuotedStrings | NoStringPrefixes | NoBareWordPasswords | MySqlComments,

        /// <summary>
        /// GoogleSQL (Spanner).
        /// </summary>
        GoogleSql = TripleQuotedLiterals | BackslashEscapedIdentifiers | (MySql & ~MySqlComments),

        /// <summary>
        /// SQL Server.
        /// </summary>
        SqlServer = BracketsDelimitIdentifiersOnly | NoDollarQuotedStrings,

        /// <summary>
        /// SQLite.
        /// </summary>
        Sqlite = BracketsDelimitIdentifiersOnly | NonNestedComments | NoBracketEscape | NoDollarQuotedStrings,

        /// <summary>
        /// Oracle.
        /// </summary>
        Oracle = NonNestedComments | NoDollarQuotedStrings,

        /// <summary>
        /// Teradata, which has no strings in dollar quotes, nor the prefixes of PostgreSQL and Oracle.
        /// </summary>
        Teradata = NonNestedComments | NoDollarQuotedStrings | NoStringPrefixes,

        /// <summary>
        /// Firebird, which supports alternative quoted strings, but has no dollar-quoted strings or nested comments.
        /// </summary>
        Firebird = Oracle | UnrestrictedAlternativeQuoteDelimiters,

        /// <summary>
        /// All of the flags, which is the largest value a dialect has.
        /// </summary>
        All = BackslashEscapes |
              DoubleQuotedStrings |
              HashComments |
              TripleQuotedLiterals |
              NonNestedComments |
              BracketsDelimitIdentifiersOnly |
              NoBracketEscape |
              NoDollarQuotedStrings |
              NoStringPrefixes |
              NoBareWordPasswords |
              BackslashEscapedIdentifiers |
              UnrestrictedAlternativeQuoteDelimiters |
              MySqlComments,
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
    /// <param name="dialect">The dialect of the source database.</param>
    /// <returns>The sanitized SQL and query summary.</returns>
    public static SqlStatementInfo GetSanitizedSql(string? sql, SqlDialect dialect = SqlDialect.Default)
    {
        if (sql == null)
        {
            return default;
        }

        return SanitizeUsingCache(sql, dialect);
    }

    // Whether the dialect has any of the flags. This is not Enum.HasFlag, which needs all of them, and which boxes on .NET Framework.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Has(SqlDialect dialect, SqlDialect flag) => (dialect & flag) != 0;

    private static ConcurrentDictionary<string, SqlStatementInfo> GetCache(SqlDialect dialect)
    {
        var cache = Caches[(int)dialect];

        if (cache != null)
        {
            return cache;
        }

        var created = new ConcurrentDictionary<string, SqlStatementInfo>();

        return Interlocked.CompareExchange(ref Caches[(int)dialect], created, null) ?? created;
    }

    private static SqlStatementInfo SanitizeUsingCache(string sql, SqlDialect dialect)
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
        var cache = GetCache(dialect);

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
        char.IsAsciiLetterOrDigit(c) || c >= '\u0080' || c is UnderscoreChar or DollarChar or HashChar or AtChar;

    // Whether the character can be part of a word in the dialect, in which # starts a comment in MySQL, MariaDB and GoogleSQL.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWordChar(char c, SqlDialect dialect) =>
        IsWordChar(c) && !(c == HashChar && Has(dialect, SqlDialect.HashComments));

    // Whether the character goes on with a word which has letters before it, so that they are not a whole word.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWordContinuation(char c) =>
        char.IsLetterOrDigit(c) || c is UnderscoreChar or DollarChar;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidTokenCharacter(char currentChar, int indexInToken, in ParseState state)
    {
        // PostgreSQL permits any non-ASCII character in an identifier. Splitting one before a digit would turn
        // that digit into a literal and incorrectly allow a following dollar sign to start a dollar-quoted string.
        if (currentChar >= '\u0080' &&
            (state.NextIsAccountName || !Has(state.Dialect, SqlDialect.NoDollarQuotedStrings)))
        {
            return true;
        }

        // If we are not capturing the next token as an identifier, we only accept unescaped identifier characters.
        // The name of a login or user is the same, so that a period before it does not use it up (e.g. .[name]).
        if (state.Pending is not (PendingToken.SummaryName or PendingToken.AccountName))
        {
            return IsUnescapedIdentifierChar(currentChar);
        }

        // In unescaped identifiers, periods are invalid at the start but valid in the middle (for schema-qualified names).
        return (currentChar != DotChar || indexInToken != 0) && IsUnescapedIdentifierChar(currentChar);
    }

    // Whether the statement has a word which the names of logins and users, and the passwords which are words, follow
    // (e.g. CREATE USER name IDENTIFIED BY password). Without any of them there is nothing to find, so the words
    // around them are not looked at. The words can be part of longer ones (e.g. Users), which is not a problem.
    private static bool MayHaveAccounts(ReadOnlySpan<char> sql) =>
#if NET9_0_OR_GREATER
        sql.ContainsAny(AccountWords);
#else
        sql.IndexOf("user".AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0 ||
        sql.IndexOf("login".AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0 ||
        sql.IndexOf("identified".AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0 ||
        sql.IndexOf("password".AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0;
#endif

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
        state.Dialect = dialect;

        // The start of the text is where a statement starts, as it is after a semicolon.
        state.PreviousWord = SqlWord.StatementEnd;

        state.TracksAccounts = MayHaveAccounts(sqlSpan);

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
                if (state.NextIsPassword && ParsePassword(sqlSpan, buffer, ref state))
                {
                    continue;
                }

                var literalStart = state.ParsePosition;

                if (SanitizeStringLiteral(sqlSpan, buffer, ref state) ||
                    SanitizeDollarQuotedLiteral(sqlSpan, buffer, ref state) ||
                    SanitizeBasePrefixedLiteral(sqlSpan, buffer, ref state) ||
                    SanitizeNumericLiteral(sqlSpan, buffer, ref state))
                {
                    state.LastLiteralEnd = state.ParsePosition;

                    // The name of a login or user which is a quoted literal (e.g. DROP USER 'name') has been sanitized,
                    // unlike a number which starts a name (e.g. 1name). So has a password which is a literal, so the token
                    // which follows it is not one, unless it is the word which precedes the password which is replaced
                    // (e.g. REPLACE old_password). What the literal was is remembered, as it is between the words around it.
                    var literalWord = SqlWord.Other;

                    if (state.NextIsAccountName && sqlSpan[literalStart] is SingleQuoteChar or DoubleQuoteChar)
                    {
                        literalWord = SqlWord.AccountName;
                        state.Pending = PendingToken.None;
                    }
                    else if (state.NextIsPassword)
                    {
                        literalWord = SqlWord.Password;
                        state.Pending = PendingToken.None;
                    }
                    else if (state.PreviousWord == SqlWord.Uescape)
                    {
                        // The escape character of a name (e.g. the ! of U&"d!0061ta" UESCAPE '!') is part of it, so that
                        // the comma after it still separates the name from the next one in DROP USER. Keep the clause
                        // through subsequent literal chunks, which PostgreSQL concatenates across a newline.
                        literalWord = SqlWord.Uescape;
                    }

                    if (state.TracksAccounts)
                    {
                        PushWord(literalWord, ref state);
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

        // U&"..." is one Unicode-escaped PostgreSQL identifier. Keep its prefix without
        // consuming it as the account name so the quoted part is redacted on the next iteration.
        if (state.NextIsAccountName &&
            currentChar is 'U' or 'u' &&
            start + 2 < sql.Length &&
            sql[start + 1] == '&' &&
            sql[start + 2] == DoubleQuoteChar)
        {
            sql.Slice(start, 2).CopyTo(buffer.Slice(state.SanitizedPosition));
            state.SanitizedPosition += 2;
            state.ParsePosition += 2;
            return;
        }

        // The word which is the password in a statement such as ALTER USER ... IDENTIFIED BY password is not a literal,
        // so it is not otherwise sanitized, and it must not be parsed as a keyword (e.g. Select1) or be split (e.g. a#b).
        if (state.NextIsPassword && ParsePassword(sql, buffer, ref state))
        {
            return;
        }

        // Quick first-character filter: only attempt keyword matching if the current char is an ASCII letter.
        // NOTE: We don't check whether a name is the next token here because we want to capture and handle keywords
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

            // A word which goes on after its letters (e.g. if123 or from_date) is an identifier, not a keyword which is followed by
            // something else, which would split the name of a login or user and put what follows it in the summary.
            if (state.TracksAccounts && state.NextIsAccountName &&
                     !IsConditionalClauseWord(ClassifyWord(sql.Slice(start, asciiLetterLength)), sql, start + asciiLetterLength, in state))
            {
                // The name of a login or user is next, which is not a keyword (e.g. ALTER USER if IDENTIFIED BY password).
                keywordsToCheck = default;
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
                    // A word which goes on after its letters (e.g. if123 or from_date) is an identifier, not a keyword which is followed by
                    // something else, which would split the name of a login or user and put what follows it in the summary.
                    if (start + keywordLength < sql.Length && IsWordContinuation(sql[start + keywordLength]))
                    {
                        break;
                    }

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

                    state.Pending = ChoosePending(
                        Observe(sql, start + keywordLength, potentialKeywordInfo.Word, ref state),
                        SqlKeywordInfo.CaptureNextTokenInSummary(in state, potentialKeywordInfo.SqlKeyword),
                        in state);

                    state.InFromClause = potentialKeywordInfo.SqlKeyword == SqlKeyword.From || (state.PreviousParsedKeyword?.SqlKeyword == SqlKeyword.From && state.NextIsSummaryName);
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

            var word = state.TracksAccounts && i > start ? ClassifyWord(sql.Slice(start, i - start)) : SqlWord.Other;

            // The whole name of a login or user is redacted, which can have characters which are not part of an
            // identifier elsewhere (e.g. acct$alice, or alice@host).
            if (state.NextIsAccountName && i > start && !IsConditionalClauseWord(word, sql, i, in state))
            {
                var nameEnd = i;
                while (i < sql.Length && IsWordChar(sql[i], state.Dialect))
                {
                    // MySQL can quote the host separately from the user (e.g. alice@`host`), and have whitespace or comments
                    // before it, so it is left to be the next token, which is the next name to sanitize.
                    if (sql[i] == AtChar && state.Dialect == SqlDialect.MySql && HasSeparateHost(sql, i))
                    {
                        break;
                    }

                    i++;
                }

                if (i != nameEnd)
                {
                    word = SqlWord.Other;
                }
            }

            var length = i - start;
            var captureNextPartOfName = false;
            var redactsName = false;
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
                redactsName = state.NextIsAccountName && !IsConditionalClauseWord(word, sql, i, in state);

                if (redactsName)
                {
                    buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
                }
                else
                {
                    sql.Slice(start, length).CopyTo(buffer.Slice(state.SanitizedPosition));
                    state.SanitizedPosition += length;
                }

                // Keep identifier context even after summary collection stops, so quotes in later name parts
                // are not mistaken for the start of a string literal.
                if (state.NextIsSummaryName)
                {
                    // A period at the end of the identifier (e.g. dbo.) is only included if another part of the name follows.
                    var endsWithDot = sql[i - 1] == DotChar;
                    var periodCount = endsWithDot
                        ? (IsNamePart(sql, SkipWhitespaceAndComments(sql, i, state.Dialect)) ? 1 : 0)
                        : GetFollowingPeriodCount(sql, i, state.Dialect);
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

            state.ParsePosition = i;
            state.Pending = ChoosePending(Observe(sql, i, redactsName ? SqlWord.AccountName : word, ref state), captureNextPartOfName, in state);
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
            var capturesNextName =
                (state.InFromClause && (currentChar is CommaChar or DotChar)) ||
                (state.NextIsSummaryName && currentChar == DotChar);

            // A password is the next token, unless this is the start of a quoted one which is not terminated,
            // or the equals sign of PASSWORD = password.
            if (state.NextIsPassword &&
                currentChar is not (DoubleQuoteChar or BacktickChar or OpenSquareBracketChar) &&
                !(currentChar == EqualsChar && state.PreviousWord == SqlWord.PasswordKeyword))
            {
                state.Pending = PendingToken.None;
            }

            // The name of a login or user, or a password, is still the next token, which a name for the summary is not.
            if (!state.NextIsAccountName && !state.NextIsPassword)
            {
                state.Pending = capturesNextName ? PendingToken.SummaryName : PendingToken.None;
            }

            if (state.TracksAccounts && state.InLoginOrUserStatement)
            {
                ParseLoginOrUserStatementCharacter(currentChar, ref state);
            }

            if (state.TracksAccounts && currentChar == SemicolonChar)
            {
                state.Statement = StatementContext.None;
            }

            // Punctuation is between the words around it, which are then not next to each other (e.g. alter, user).
            if (state.TracksAccounts)
            {
                PushWord(currentChar == SemicolonChar ? SqlWord.StatementEnd : SqlWord.Other, ref state);
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
            state.Pending = PendingToken.None;
            state.PreviousTokenStartPosition = start;
            state.PreviousTokenEndPosition = sql.Length;
            PushWord(SqlWord.Other, ref state);
            return true;
        }

        var length = end - start + 1;
        var captureNextPartOfName = false;
        var word = SqlWord.Other;

        // Redact the whole name after LOGIN or USER (e.g. CREATE LOGIN [DOMAIN\user]), or a password
        // (e.g. IDENTIFIED BY "pass word"), keeping its delimiters. A double-quoted value which starts
        // with a number is also redacted, because it is more likely to be a string literal (e.g. in
        // GoogleSQL, or SQL Server with QUOTED_IDENTIFIER OFF) than an identifier. An empty name has
        // nothing to redact, and the sanitized SQL must not be longer than the input.
        if (length > 2 &&
            (state.Pending is PendingToken.AccountName or PendingToken.Password ||
             (sql[start] == DoubleQuoteChar && StartsWithNumber(sql.Slice(start + 1, length - 2).TrimStart()))))
        {
            buffer[state.SanitizedPosition++] = sql[start];
            buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
            buffer[state.SanitizedPosition++] = sql[end];

            if (state.NextIsAccountName)
            {
                word = SqlWord.AccountName;
            }
            else if (state.NextIsPassword)
            {
                word = SqlWord.Password;
            }
        }
        else
        {
            sql.Slice(start, length).CopyTo(buffer.Slice(state.SanitizedPosition));
            state.SanitizedPosition += length;

            // Only bracketed identifiers are included in the summary, as they were before quoted
            // identifiers were parsed as a single token. They are included wherever an unquoted
            // identifier would be (e.g. after INTO or EXEC), as well as anywhere in a FROM clause.
            if ((state.InFromClause || state.NextIsSummaryName) &&
                sql[start] == OpenSquareBracketChar)
            {
                // Name classification must continue independently of the summary length limit.
                var periodCount = GetFollowingPeriodCount(sql, end + 1, state.Dialect);
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
        state.Pending = captureNextPartOfName ? PendingToken.SummaryName : PendingToken.None;
        state.PreviousTokenStartPosition = start;
        state.PreviousTokenEndPosition = end + 1;
        PushWord(word, ref state);

        return true;
    }

    // The number of periods at the position, ignoring any whitespace and comments, if they are followed by another
    // part of a multi-part name which can be captured (e.g. the [Orders] in [dbo] . [Orders] or [dbo]./* c */[Orders]).
    // Consecutive periods allow omitted database and schema parts (e.g. [Server]...[Orders]). Longer runs in malformed
    // queries must also preserve identifier context so later literals are sanitized. Periods which are not followed
    // by a part of the name are left out of the summary, in which case zero is returned.
    private static int GetFollowingPeriodCount(ReadOnlySpan<char> sql, int position, SqlDialect dialect)
    {
        position = SkipWhitespaceAndComments(sql, position, dialect);

        if (position >= sql.Length || sql[position] != DotChar)
        {
            return 0;
        }

        var periodCount = 1;
        position = SkipWhitespaceAndComments(sql, position + 1, dialect);

        while (position < sql.Length && sql[position] == DotChar)
        {
            periodCount++;
            position = SkipWhitespaceAndComments(sql, position + 1, dialect);
        }

        return IsNamePart(sql, position) ? periodCount : 0;
    }

    // Whether the position is the start of a part of a name which can be captured.
    private static bool IsNamePart(ReadOnlySpan<char> sql, int position) =>
        position < sql.Length &&
        (sql[position] is OpenSquareBracketChar or UnderscoreChar || char.IsLetter(sql[position]));

    private static int SkipWhitespaceAndComments(ReadOnlySpan<char> sql, int position, SqlDialect dialect)
    {
        while (position < sql.Length)
        {
            if (sql[position] is SpaceChar or TabChar or CarriageReturnChar or NewLineChar)
            {
                position++;
                continue;
            }

            var commentEnd = FindCommentEnd(sql, position, dialect);

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
        BacktickChar => FindDelimitedIdentifierEnd(sql, start, BacktickChar, Has(state.Dialect, SqlDialect.BackslashEscapedIdentifiers), ref state.NoTerminatingBacktickQuotedIdentifierAhead),
        DoubleQuoteChar when !Has(state.Dialect, SqlDialect.DoubleQuotedStrings) => FindDelimitedIdentifierEnd(sql, start, DoubleQuoteChar, false, ref state.NoTerminatingDoubleQuotedIdentifierAhead),
        _ => -1,
    };

    private static int FindDelimitedIdentifierEnd(ReadOnlySpan<char> sql, int start, char delimiter, bool backslashEscapes, ref bool noTerminatingIdentifierAhead)
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

            // GoogleSQL identifiers use the same backslash escapes as string literals. MySQL
            // identifiers only escape a backtick by doubling it, even when string escapes are enabled.
            if (backslashEscapes && IsBackslashEscaped(sql, i, start))
            {
                i++;
                continue;
            }

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
                // An escaped bracket is not the end, and a quote beyond it can still make the boundary ambiguous.
                state.RedactRemainder =
                    (i + 1 < sql.Length && sql[i + 1] == CloseSquareBracketChar && !Has(state.Dialect, SqlDialect.NoBracketEscape)) ||
                    sql.Slice(start + 1, i - start - 1).IndexOf(SingleQuoteChar) >= 0;
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
            // A doubled closing bracket (]]) is an escaped bracket within the identifier, but not in SQLite.
            if (i + 1 < sql.Length && sql[i + 1] == CloseSquareBracketChar && !Has(state.Dialect, SqlDialect.NoBracketEscape))
            {
                searchEnd = Math.Min(sql.Length, searchEnd + 1);
                i = FindNextCloseSquareBracket(sql, i + 2, ref state);
                continue;
            }

            // A name which is going to be redacted is always treated as an identifier, as none of it is copied. A bracket
            // where a name is expected (e.g. after FROM) cannot be a list of values, so it is always a name, which ends at
            // the first closing bracket, rather than the content of it and the SQL after it being used to guess what it is.
            if (state.NextIsAccountName || state.BracketsDelimitIdentifiersOnly || IsNameExpected(sql, start, in state))
            {
                return i;
            }

            if (IsBracketedIdentifier(sql, start + 1, i - start - 1, state.Dialect, ref state.BracketLookaheadLength, out var unresolved))
            {
                // If it is not known what the content is, where it ends is not known either, as the bracket could be in a
                // string. Any of the rest of the statement could be part of it, so none of it is copied.
                state.RedactRemainder = unresolved;
                return i;
            }

            return -1;
        }

        // Escaped brackets can push the closing delimiter beyond the length limit after the initial check.
        // Its boundary is then unreliable, including when a later escaped bracket hides an apostrophe.
        if (state.BracketsDelimitIdentifiersOnly)
        {
            state.RedactRemainder = true;
            return sql.Length - 1;
        }

        return -1;

        static bool IsNameExpected(ReadOnlySpan<char> sql, int start, in ParseState state)
        {
            if (state.NextIsSummaryName)
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

        static bool IsBracketedIdentifier(ReadOnlySpan<char> sql, int contentStart, int contentLength, SqlDialect dialect, ref int lookaheadLength, out bool unresolved)
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

            if (ContainsDollarQuotedString(content, dialect))
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
            if (StartsWithComment(content, dialect) ||
                (singleQuotes > 0 && StartsWithNumberLiteral(content)) ||
                ((singleQuotes & 1) == 1 && StartsWithPrefixedString(content)))
            {
                var hasClosingBracket = HasClosingBracketOutsideLiteralsAndComments(sql, contentStart, dialect, ref lookaheadLength);

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
                  ((singleQuotes & 1) == 1 || (CommentContainsClosingBracket(content, dialect) && !ContainsValue(content, dialect)));
        }

        static bool StartsWithComment(ReadOnlySpan<char> content, SqlDialect dialect)
        {
            return content.Length >= 2 &&
                ((content[0] == ForwardSlashChar && content[1] == AsteriskChar) || IsDashCommentStart(content, 0, dialect));
        }

        static bool ContainsDollarQuotedString(ReadOnlySpan<char> content, SqlDialect dialect)
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

                if (IsDashCommentStart(content, i, dialect))
                {
                    var lineEnd = FindLineCommentEnd(content, i, dialect);
                    if (lineEnd == content.Length)
                    {
                        return false;
                    }

                    i = lineEnd;
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
                        TryFindDollarQuotedLiteralEnd(content, i, dialect, out _))
                    {
                        return true;
                    }

                    previous = c;
                }
            }

            return false;
        }

        static bool? HasClosingBracketOutsideLiteralsAndComments(ReadOnlySpan<char> sql, int start, SqlDialect dialect, ref int lookaheadLength)
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
                        end = FindStringLiteralEnd(sql, i, dialect, out _);
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
                        end = FindCommentEnd(sql, i, dialect);
                        if (end > i)
                        {
                            i = end - 1;
                        }

                        continue;

                    case DollarChar:
                        if (TryFindDollarQuotedLiteralEnd(sql, i, dialect, out end))
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

        static bool ContainsValue(ReadOnlySpan<char> content, SqlDialect dialect)
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
                    if (IsDashCommentStart(content, i, dialect))
                    {
                        i = FindLineCommentEnd(content, i, dialect);
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

        static bool CommentContainsClosingBracket(ReadOnlySpan<char> content, SqlDialect dialect)
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
                else if (IsDashCommentStart(content, i, dialect))
                {
                    // A line comment ends at the end of the line, if the line does end within the content.
                    var lineEnd = FindLineCommentEnd(content, i, dialect);
                    if (lineEnd == content.Length)
                    {
                        return true;
                    }

                    i = lineEnd;
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
        var commentEnd = FindCommentEnd(sql, state.ParsePosition, state.Dialect);

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
    private static int FindCommentEnd(ReadOnlySpan<char> sql, int start, SqlDialect dialect)
    {
        var ch = sql[start];
        var next = start + 1;

        // Scan past multi-line comment
        if (ch == ForwardSlashChar && next < sql.Length && sql[next] == AsteriskChar)
        {
            // MySQL/MariaDB execute /*! ... */ and /*M! ... */ depending on the server and its version.
            // A closing marker inside a literal then need not end the comment. Neither that version nor
            // whether the body is executable is known here, so its boundary cannot safely be inferred.
            if (Has(dialect, SqlDialect.MySqlComments) && start + 2 < sql.Length &&
                (sql[start + 2] == '!' ||
                 (start + 3 < sql.Length && sql[start + 2] == 'M' && sql[start + 3] == '!')))
            {
                return sql.Length;
            }

            // SQL Server, PostgreSQL and Db2 allow block comments to be nested, so a comment such as
            // /* /* */ don't */ only ends at the second "*/". MySQL/MariaDB, SQLite, Oracle and others do not,
            // so the comment would end at the first "*/", and treating it as still open would read the
            // literals which follow it as part of the comment, or as if they were outside of it.
            return FindMultiLineCommentEnd(sql, start, allowNesting: !Has(dialect, SqlDialect.NonNestedComments));
        }

        // Scan past single-line comment. MySQL/MariaDB also start one with '#', which is otherwise
        // part of an identifier in the other dialects (e.g. a SQL Server #temp table).
        if (IsDashCommentStart(sql, start, dialect) || (ch == HashChar && Has(dialect, SqlDialect.HashComments)))
        {
            return FindLineCommentEnd(sql, start, dialect);
        }

        return -1;
    }

    private static bool IsDashCommentStart(ReadOnlySpan<char> sql, int start, SqlDialect dialect)
    {
        return sql[start] == DashChar && start + 1 < sql.Length && sql[start + 1] == DashChar &&
            (!Has(dialect, SqlDialect.MySqlComments) || start + 2 == sql.Length ||
             sql[start + 2] <= SpaceChar || sql[start + 2] == '\u007f');
    }

    private static int FindLineCommentEnd(ReadOnlySpan<char> sql, int start, SqlDialect dialect)
    {
        var next = start + 1;
        var lineBreakIndex = Has(dialect, SqlDialect.MySqlComments)
            ? sql.Slice(next).IndexOf(NewLineChar)
#if NET
            : sql.Slice(next).IndexOfAny(LineBreakSearchValues);
#else
            : sql.Slice(next).IndexOfAny(LineBreakChars);
#endif

        // Position at the newline so ParseWhitespace can copy it.
        return lineBreakIndex < 0 ? sql.Length : next + lineBreakIndex;
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

        // MySQL/MariaDB and GoogleSQL (the same dialects for which DoubleQuotedStrings is set) also treat a
        // double-quoted value as a string literal unless the ANSI_QUOTES sql_mode is enabled.
        // The other dialects supported here use '"' exclusively to delimit a quoted identifier
        // (e.g. "columnName"), so this must remain gated on the dialect flag: otherwise a quoted
        // identifier in those dialects would be misidentified as a literal and redacted, and
        // conversely a MySQL/MariaDB double-quoted literal would leak into the sanitized SQL
        // verbatim (it is never recognized as an identifier or literal by any other check here).
        return currentChar == DoubleQuoteChar &&
               Has(state.Dialect, SqlDialect.DoubleQuotedStrings) &&
               SanitizeQuotedLiteral(sql, buffer, ref state, DoubleQuoteChar);
    }

    private static bool SanitizeQuotedLiteral(ReadOnlySpan<char> sql, Span<char> buffer, ref ParseState state, char delimiter)
    {
        var prefixLength = 0;
        var literalEnd = delimiter == SingleQuoteChar
            ? FindStringLiteralEnd(sql, state.ParsePosition, state.Dialect, out prefixLength)
            : FindQuotedLiteralEnd(sql, state.ParsePosition, delimiter, state.Dialect);

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
    /// <param name="dialect">The dialect of the source database.</param>
    /// <param name="prefixLength">
    /// The number of characters which precede the opening quote and are part of the literal
    /// (e.g. the <c>N</c> of the Unicode literal <c>N'foo'</c>).
    /// </param>
    /// <returns>The index of the closing quote, or -1 if the literal is not terminated.</returns>
    private static int FindStringLiteralEnd(ReadOnlySpan<char> sql, int quotePosition, SqlDialect dialect, out int prefixLength)
    {
        prefixLength = 0;

        if (quotePosition == 0)
        {
            return FindQuotedLiteralEnd(sql, quotePosition, SingleQuoteChar, dialect);
        }

        var previousChar = sql[quotePosition - 1];

        // These prefixes are only recognized when they are a separate token (so not the end of
        // ELSE'a'), and not for MySQL/MariaDB, which do not support them.
        if (!Has(dialect, SqlDialect.NoStringPrefixes))
        {
            // A PostgreSQL escape string (e.g. E'it\'s'), in which a backslash escapes a quote.
            if (previousChar is 'E' or 'e' && IsSeparateToken(sql, quotePosition - 1))
            {
                prefixLength = 1;
                return FindQuotedLiteralEnd(sql, quotePosition, SingleQuoteChar, SqlDialect.BackslashEscapes);
            }

            // An Oracle alternative quoting literal (e.g. q'[it's]', or nq'[it's]' for a national
            // character literal), in which quotes are not escaped. The delimiter which follows the
            // opening quote can be any character other than whitespace or an apostrophe in Oracle,
            // while Firebird also allows those delimiters. The literal ends at the
            // first closing delimiter (']' for '[', '}' for '{', ')' for '(' and '>' for '<',
            // otherwise the same character) which is followed by a quote.
            if (previousChar is 'q' or 'Q' &&
                quotePosition + 1 < sql.Length &&
                (Has(dialect, SqlDialect.UnrestrictedAlternativeQuoteDelimiters) ||
                 sql[quotePosition + 1] is not (SpaceChar or TabChar or CarriageReturnChar or NewLineChar or SingleQuoteChar)))
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

        return FindQuotedLiteralEnd(sql, quotePosition, SingleQuoteChar, dialect);

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
    private static int FindQuotedLiteralEnd(ReadOnlySpan<char> sql, int quotePosition, char delimiter, SqlDialect dialect)
    {
        // GoogleSQL also has triple-quoted literals ('''...''' and """..."""), in which a quote does not
        // have to be escaped (e.g. '''it's'''). Reading the opening quotes as a quote followed by an escaped
        // quote would end the literal at the first quote inside it, and the rest of the value would be copied
        // into the sanitized SQL verbatim. This is gated on the dialect because in the other dialects ''' is
        // the start of a literal whose first character is an escaped quote.
        if (Has(dialect, SqlDialect.TripleQuotedLiterals) &&
            quotePosition + 2 < sql.Length &&
            sql[quotePosition + 1] == delimiter &&
            sql[quotePosition + 2] == delimiter)
        {
            return FindTripleQuotedLiteralEnd(sql, quotePosition, delimiter, dialect);
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
            if (Has(dialect, SqlDialect.BackslashEscapes) && IsBackslashEscaped(sql, searchPos, quotePosition))
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
    private static int FindTripleQuotedLiteralEnd(ReadOnlySpan<char> sql, int quotePosition, char delimiter, SqlDialect dialect)
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
                !(Has(dialect, SqlDialect.BackslashEscapes) && IsBackslashEscaped(sql, searchPos, quotePosition)))
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
            !TryFindDollarQuotedLiteralEnd(sql, state.ParsePosition, state.Dialect, out var literalEnd, ignoreLookBehind: state.ParsePosition == state.LastLiteralEnd))
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
    /// <param name="dialect">The dialect of the source database.</param>
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
    private static bool TryFindDollarQuotedLiteralEnd(ReadOnlySpan<char> sql, int start, SqlDialect dialect, out int literalEnd, bool ignoreLookBehind = false)
    {
        // PostgreSQL dollar-quoted string: $tag$...$tag$ (the tag is optional, so $$...$$ is valid).
        // The body between the delimiters is a literal with no escaping, so it must be redacted.
        literalEnd = -1;

        if (sql[start] != DollarChar || Has(dialect, SqlDialect.NoDollarQuotedStrings))
        {
            return false;
        }

        // A '$' which directly follows a letter, a digit, an underscore or another '$' is part of an identifier
        // or variable name (e.g. price$gbp$ or DR$idx$I), in PostgreSQL as well as in the other dialects handled here,
        // so it does not open a dollar-quoted string. Treating it as one would skip to the next occurrence of the
        // same delimiter, which may be inside a string literal, after which literals would be read as code.
        // Other characters can precede a dollar-quoted string, as '@' and '#' are operators in PostgreSQL.
        // PostgreSQL allows non-ASCII characters in unquoted identifiers and dollar-quote tags, including
        // combining marks and supplementary characters, so the same rules must be used on both sides.
        if (!ignoreLookBehind &&
            start > 0 &&
            (IsDollarQuoteTagChar(sql[start - 1]) || sql[start - 1] == DollarChar))
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
            return char.IsAsciiLetter(c) || c == UnderscoreChar || c >= '\u0080';
        }

        static bool IsDollarQuoteTagChar(char c)
        {
            return IsDollarQuoteTagStartChar(c) || char.IsAsciiDigit(c);
        }
    }

    private static bool SanitizeBasePrefixedLiteral(ReadOnlySpan<char> sql, Span<char> buffer, ref ParseState state)
    {
        if (!TryFindBasePrefixedLiteralEnd(sql, state.ParsePosition, out var prefixIndex, out var end))
        {
            return false;
        }

        if (TrySanitizeLiteralsForInClause(sql, buffer, ref state, prefixIndex))
        {
            return true;
        }

        state.ParsePosition = end;
        buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
        return true;
    }

    // Finds the end of a number with a base prefix (e.g. 0x1F, 0b101 or 0o173), which is at the position or after its sign.
    // This is how the parser finds the number, so it is also used to find where one ends when scanning ahead of it.
    private static bool TryFindBasePrefixedLiteralEnd(ReadOnlySpan<char> sql, int start, out int prefixIndex, out int end)
    {
        prefixIndex = -1;
        end = -1;

        var i = start;
        var length = sql.Length;

        if ((sql[i] is '-' or '+') && i + 1 < length && sql[i + 1] == '0')
        {
            i++;
        }

        if (sql[i] != '0' || i + 1 >= length)
        {
            return false;
        }

        var numberBase = (sql[i + 1] | 0x20) switch
        {
            'x' => 16,
            'b' => 2,
            'o' => 8,
            _ => 0,
        };

        if (numberBase == 0)
        {
            return false;
        }

        // Hexadecimal prefixes were already recognized without digits. For a new binary or octal prefix,
        // require a digit so that a word such as 0bank is not consumed as a numeric literal.
        if (numberBase != 16 &&
            (i + 2 >= length ||
             !(IsBaseDigit(sql[i + 2], numberBase) ||
               (sql[i + 2] == UnderscoreChar && i + 3 < length && IsBaseDigit(sql[i + 3], numberBase)))))
        {
            return false;
        }

        prefixIndex = i;

        for (i += 2; i < length; i++)
        {
            if (!IsBaseDigit(sql[i], numberBase) &&
                !(sql[i] == UnderscoreChar && i + 1 < length && IsBaseDigit(sql[i + 1], numberBase)))
            {
                break;
            }
        }

        end = i;
        return true;

        static bool IsBaseDigit(char value, int numberBase) => numberBase switch
        {
            2 => value is '0' or '1',
            8 => value is >= '0' and <= '7',
            _ => char.IsAsciiHexDigit(value),
        };
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

            state.ParsePosition = FindNumericLiteralEnd(sql, i, periodMatched);

            buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
            return true;
        }

        return false;
    }

    // Finds the end of a number from its first digit, with a decimal point already found if it started with one.
    // This is how the parser finds the number, so it is also used to find where one ends when scanning ahead of it.
    private static int FindNumericLiteralEnd(ReadOnlySpan<char> sql, int firstDigit, bool periodMatched)
    {
        var length = sql.Length;
        var exponentMatched = false;

        for (var i = firstDigit + 1; i < length; ++i)
        {
            var currentChar = sql[i];
            if (char.IsAsciiDigit(currentChar) ||
                (currentChar == UnderscoreChar && i + 1 < length &&
                 char.IsAsciiDigit(sql[i - 1]) && char.IsAsciiDigit(sql[i + 1])))
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

            // Oracle appends F or D to a floating-point literal. Only consume the suffix at the end
            // of its token, so an adjacent word is not partially consumed as a type suffix.
            if ((currentChar is 'f' or 'F' or 'd' or 'D') &&
                (i + 1 >= length || !IsWordChar(sql[i + 1])))
            {
                return i + 1;
            }

            return i;
        }

        return length;
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
                    end = FindStringLiteralEnd(sql, i, state.Dialect, out _);
                    i = end < 0 ? length : end + 1;
                    literalEnd = i;
                    break;

                case DoubleQuoteChar when Has(state.Dialect, SqlDialect.DoubleQuotedStrings):
                    end = FindQuotedLiteralEnd(sql, i, DoubleQuoteChar, state.Dialect);
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
                    if (TryFindDollarQuotedLiteralEnd(sql, i, state.Dialect, out end, ignoreLookBehind: i == literalEnd || EndsWithNumericLiteral(sql, i)))
                    {
                        i = end;
                        literalEnd = i;
                    }
                    else
                    {
                        // A rejected dollar sign is part of an identifier, not the end of a literal.
                        // The next dollar sign must still check its preceding identifier character.
                        i++;
                    }

                    break;

                // DashChar, ForwardSlashChar or HashChar
                default:
                    end = FindCommentEnd(sql, i, state.Dialect);
                    i = end < 0 ? i + 1 : end;
                    break;
            }
        }

        closeParenIndex = -1;
        return false;
    }

    // Whether the host which follows the @ at the position is quoted, or is separated from it by whitespace or comments
    // (e.g. alice@`host` or alice@ /* host of the user */ host), so it is not part of the name before it.
    private static bool HasSeparateHost(ReadOnlySpan<char> sql, int at)
    {
        var next = SkipWhitespaceAndComments(sql, at + 1, SqlDialect.MySql);

        return next != at + 1 || (next < sql.Length && sql[next] is SingleQuoteChar or DoubleQuoteChar or BacktickChar);
    }

    // Whether a number (e.g. 1, 1.5e3, 1_000 or 0x1F) ends at the position, so that what follows it, which is not part of it,
    // is not part of an identifier, as it is not in the main parser (e.g. the dollar quote of 1$$ text $$).
    private static bool EndsWithNumericLiteral(ReadOnlySpan<char> sql, int end)
    {
        var start = end;
        while (start > 0 &&
               (char.IsAsciiLetterOrDigit(sql[start - 1]) ||
                sql[start - 1] is UnderscoreChar or DotChar ||
                (sql[start - 1] is '+' or '-' && start > 1 && (sql[start - 2] | 0x20) == 'e')))
        {
            start--;
        }

        // A word which follows a character of a word (e.g. $1) is an identifier, as is one which starts with a letter (e.g. a1).
        if (start == end || (start > 0 && IsWordChar(sql[start - 1])))
        {
            return false;
        }

        // The parser can find several adjacent numbers in malformed SQL (e.g. 1.2 followed by .3), which is still
        // recorded before rejection. Follow each token, stopping if a word starts instead (e.g. the suffix of 1__000).
        while (start < end)
        {
            if (!TryFindBasePrefixedLiteralEnd(sql, start, out _, out var tokenEnd))
            {
                var periodMatched = sql[start] == DotChar;
                var firstDigit = periodMatched ? start + 1 : start;
                if (firstDigit >= end || !char.IsAsciiDigit(sql[firstDigit]))
                {
                    return false;
                }

                tokenEnd = FindNumericLiteralEnd(sql, firstDigit, periodMatched);
            }

            if (tokenEnd >= end)
            {
                return tokenEnd == end;
            }

            start = tokenEnd;
        }

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
        public SqlWord PreviousWord; // 1 byte (enum, underlying byte)
        public bool TracksAccounts;
        public SqlWord PreviousPreviousWord; // 1 byte (enum, underlying byte)

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

        // The position after the most recent literal which was sanitized in the main loop, or zero if there was none.
        public int LastLiteralEnd; // 4 bytes

        // NOTE: If the number of bool fields increases significantly, consider combining into a bitfield.

        // Set while finding the end of a bracket which could be a name or a list of values, if which could not be worked out.
        public bool RedactRemainder; // 1 byte

        // What the next token is expected to be.
        public PendingToken Pending; // 1 byte (enum, underlying byte)

        // The kinds of statement which the parser is in.
        public StatementContext Statement; // 1 byte (enum, underlying byte)

        /// <summary>
        /// The dialect of the source database.
        /// </summary>
        public SqlDialect Dialect; // 4 bytes (enum, underlying int)

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

        public readonly bool UseTripleQuotedLiterals => Has(this.Dialect, SqlDialect.TripleQuotedLiterals);

        public readonly bool BracketsDelimitIdentifiersOnly => Has(this.Dialect, SqlDialect.BracketsDelimitIdentifiersOnly);

        public readonly bool NextIsSummaryName => this.Pending == PendingToken.SummaryName;

        public readonly bool NextIsAccountName => this.Pending == PendingToken.AccountName;

        public readonly bool NextIsPassword => this.Pending == PendingToken.Password;

        public readonly bool InLoginOrUserStatement => (this.Statement & StatementContext.LoginOrUser) != 0;

        public readonly bool InDropLoginOrUserStatement => (this.Statement & StatementContext.DropLoginOrUser) != 0;

        public readonly bool InAlterTable => (this.Statement & StatementContext.AlterTable) != 0;

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
            this.Word = ClassifyWord(keyword);
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

        public SqlWord Word { get; }

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
