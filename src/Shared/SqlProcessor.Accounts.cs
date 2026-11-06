// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace OpenTelemetry.Instrumentation;

// The sanitization of the names of logins and users, and of the passwords which are not literals, which is the part
// of SqlProcessor which looks at the words around them. The rest of it is in SqlProcessor.cs.
internal static partial class SqlProcessor
{
    /// <summary>
    /// The words which are looked for around the names of logins and users, and the passwords which are not literals.
    /// Each word is found once, and the words before it are remembered, instead of being compared with text again.
    /// </summary>
    private enum SqlWord : byte
    {
        // Any other word, or a token which is not a word (e.g. a quoted identifier).
        Other = 0,

        // The words which start a statement, which has none of the names of the one before it.
        Create,
        Alter,
        Drop,
        Select,
        Insert,
        Update,
        Delete,
        Merge,
        Truncate,
        Exec,
        Execute,
        Grant,
        Revoke,
        Deny,
        Use,
        Backup,
        Restore,

        // The words which follow the name of a column in a clause of ALTER TABLE (as do Alter and Drop).
        Type,
        Set,
        Add,
        Reset,
        Collate,
        Using,
        Cascade,
        Restrict,

        User,
        Login,
        For,
        From,
        With,
        Name,
        Rename,
        To,
        If,
        Not,
        Exists,
        Table,
        Or,
        Replace,
        Identified,
        By,
        Values,
        PasswordKeyword,
        Modify,
        Default,
        Role,
        Uescape,

        // Not words, but what a token was: the CREATE OR REPLACE, the name of a login or user which was redacted, a
        // password which was redacted, and the semicolon which ends a statement (as does the start of the text).
        StatementEnd,
        CreateOrReplace,
        AccountName,
        Password,
    }

    /// <summary>
    /// What the next token is expected to be, which is set by the token or the character before it. A name or a password
    /// which is redacted is more important than a name which is only captured for the summary, which are not both expected.
    /// </summary>
    private enum PendingToken : byte
    {
        // Nothing in particular.
        None = 0,

        // The next part of a name which is in the summary (e.g. the Orders of FROM dbo.Orders).
        SummaryName,

        // The name of a login or user, which is redacted.
        AccountName,

        // A password which is not a literal, which is redacted.
        Password,
    }

    /// <summary>
    /// The kinds of statement which the parser is in, until it ends with a semicolon or another statement starts.
    /// </summary>
    [Flags]
    private enum StatementContext : byte
    {
        None = 0,

        // The statement creates, alters or drops a login or user, so it has more of their names (e.g. FOR LOGIN name).
        LoginOrUser = 1,

        // The statement drops a login or user, so it can list more than one of them (e.g. DROP USER name, name).
        DropLoginOrUser = 2,

        // The statement is ALTER TABLE, in which ALTER and DROP can be followed by the name of a column.
        AlterTable = 4,

        // MySQL CREATE, ALTER and RENAME USER can have a comma-separated list of accounts.
        AccountList = 8,

        // RENAME USER uses TO between each old and new account name.
        RenameUser = 16,
    }

    // Finds which of the words which are looked for a word is, by its length and first letter as most words are none of them.
    private static SqlWord ClassifyWord(ReadOnlySpan<char> word)
    {
        if (word.IsEmpty)
        {
            return SqlWord.Other;
        }

        return (word.Length, word[0] | 0x20) switch
        {
            (2, 'b') => Match(word, "by", SqlWord.By),
            (2, 'i') => Match(word, "if", SqlWord.If),
            (2, 'o') => Match(word, "or", SqlWord.Or),
            (2, 't') => Match(word, "to", SqlWord.To),
            (3, 'a') => Match(word, "add", SqlWord.Add),
            (3, 'f') => Match(word, "for", SqlWord.For),
            (3, 'n') => Match(word, "not", SqlWord.Not),
            (3, 's') => Match(word, "set", SqlWord.Set),
            (3, 'u') => Match(word, "use", SqlWord.Use),
            (4, 'd') => Match(word, "drop", SqlWord.Drop, "deny", SqlWord.Deny),
            (4, 'e') => Match(word, "exec", SqlWord.Exec),
            (4, 'f') => Match(word, "from", SqlWord.From),
            (4, 'n') => Match(word, "name", SqlWord.Name),
            (4, 'r') => Match(word, "role", SqlWord.Role),
            (4, 't') => Match(word, "type", SqlWord.Type),
            (4, 'u') => Match(word, "user", SqlWord.User),
            (4, 'w') => Match(word, "with", SqlWord.With),
            (5, 'a') => Match(word, "alter", SqlWord.Alter),
            (5, 'g') => Match(word, "grant", SqlWord.Grant),
            (5, 'l') => Match(word, "login", SqlWord.Login),
            (5, 'm') => Match(word, "merge", SqlWord.Merge),
            (5, 'r') => Match(word, "reset", SqlWord.Reset),
            (5, 't') => Match(word, "table", SqlWord.Table),
            (5, 'u') => Match(word, "using", SqlWord.Using),
            (6, 'b') => Match(word, "backup", SqlWord.Backup),
            (6, 'c') => Match(word, "create", SqlWord.Create),
            (6, 'd') => Match(word, "delete", SqlWord.Delete),
            (6, 'm') => Match(word, "modify", SqlWord.Modify),
            (6, 'e') => Match(word, "exists", SqlWord.Exists),
            (6, 'i') => Match(word, "insert", SqlWord.Insert),
            (6, 'r') => Match(word, "rename", SqlWord.Rename, "revoke", SqlWord.Revoke),
            (6, 's') => Match(word, "select", SqlWord.Select),
            (6, 'u') => Match(word, "update", SqlWord.Update),
            (6, 'v') => Match(word, "values", SqlWord.Values),
            (7, 'c') => Match(word, "collate", SqlWord.Collate, "cascade", SqlWord.Cascade),
            (7, 'd') => Match(word, "default", SqlWord.Default),
            (7, 'e') => Match(word, "execute", SqlWord.Execute),
            (7, 'r') => Match(word, "replace", SqlWord.Replace, "restore", SqlWord.Restore),
            (7, 'u') => Match(word, "uescape", SqlWord.Uescape),
            (8, 'p') => Match(word, "password", SqlWord.PasswordKeyword),
            (8, 'r') => Match(word, "restrict", SqlWord.Restrict),
            (8, 't') => Match(word, "truncate", SqlWord.Truncate),
            (10, 'i') => Match(word, "identified", SqlWord.Identified),
            _ => SqlWord.Other,
        };
    }

    private static SqlWord Match(ReadOnlySpan<char> word, string text, SqlWord result) =>
        word.Equals(text.AsSpan(), StringComparison.OrdinalIgnoreCase) ? result : SqlWord.Other;

    private static SqlWord Match(ReadOnlySpan<char> word, string text, SqlWord result, string otherText, SqlWord otherResult) =>
        word.Equals(text.AsSpan(), StringComparison.OrdinalIgnoreCase) ? result : Match(word, otherText, otherResult);

    // Whether the word can come between LOGIN or USER and its name (e.g. DROP USER IF EXISTS name).
    private static bool IsIfNotOrExists(SqlWord word) => word is SqlWord.If or SqlWord.Not or SqlWord.Exists;

    // Whether the word, which ends at the position, is one of the IF [NOT] EXISTS between LOGIN or USER and its name, rather than
    // the name, which can be any word (e.g. ALTER USER if IDENTIFIED BY password). IF is only the clause if EXISTS or NOT
    // follows, and NOT and EXISTS are only if IF or NOT is before.
    private static bool IsConditionalClauseWord(SqlWord word, ReadOnlySpan<char> sql, int end, in ParseState state)
    {
        switch (word)
        {
            case SqlWord.If:
                var position = SkipWhitespaceAndComments(sql, end, state.Dialect);
                var nextEnd = position;
                while (nextEnd < sql.Length && IsWordChar(sql[nextEnd], state.Dialect))
                {
                    nextEnd++;
                }

                return ClassifyWord(sql.Slice(position, nextEnd - position)) is SqlWord.Not or SqlWord.Exists;

            case SqlWord.Not:
                return state.PreviousWord == SqlWord.If;

            case SqlWord.Exists:
                return state.PreviousWord is SqlWord.If or SqlWord.Not;

            default:
                return false;
        }
    }

    private static bool IsStatementStart(SqlWord word) =>
        word is
            SqlWord.Create or
            SqlWord.Alter or
            SqlWord.Drop or
            SqlWord.Select or
            SqlWord.Insert or
            SqlWord.Update or
            SqlWord.Delete or
            SqlWord.Merge or
            SqlWord.Truncate or
            SqlWord.Exec or
            SqlWord.Execute or
            SqlWord.Grant or
            SqlWord.Revoke or
            SqlWord.Deny or
            SqlWord.Use or
            SqlWord.Backup or
            SqlWord.Restore;

    private static void PushWord(SqlWord word, ref ParseState state)
    {
        state.PreviousPreviousWord = state.PreviousWord;
        state.PreviousWord = word;
    }

    private static bool IsFollowedByEquals(ReadOnlySpan<char> sql, int position, SqlDialect dialect)
    {
        position = SkipWhitespaceAndComments(sql, position, dialect);

        return position < sql.Length && sql[position] == EqualsChar;
    }

    // Whether the LOGIN or USER which ends at the position, after ALTER or DROP, is the name of a column
    // (e.g. ALTER TABLE t ALTER login TYPE text or ALTER TABLE t DROP login, ADD c int), as it is followed by what follows
    // a column, instead of by the name of a login or user.
    private static bool IsColumnOperation(ReadOnlySpan<char> sql, int position, SqlDialect dialect, bool isDrop)
    {
        position = SkipWhitespaceAndComments(sql, position, dialect);

        if (position >= sql.Length || sql[position] is CommaChar or SemicolonChar or CloseParenChar)
        {
            return true;
        }

        var end = position;
        while (end < sql.Length && IsWordChar(sql[end], dialect))
        {
            end++;
        }

        var word = ClassifyWord(sql.Slice(position, end - position));

        // The name of a column which is dropped can only be followed by CASCADE or RESTRICT, or by another clause.
        var followsColumn = isDrop
            ? word is SqlWord.Alter or SqlWord.Drop or SqlWord.Cascade or SqlWord.Restrict
            : word is
                SqlWord.Alter or
                SqlWord.Drop or
                SqlWord.Type or
                SqlWord.Set or
                SqlWord.Add or
                SqlWord.Reset or
                SqlWord.Collate or
                SqlWord.Using or
                SqlWord.Cascade or
                SqlWord.Restrict;

        if (!followsColumn)
        {
            return false;
        }

        // The name of a login or user can be one of those words (e.g. ALTER USER type WITH NAME = name, which is a statement
        // after ALTER TABLE t ADD c int and not a clause of it), when it is followed by what follows the name of a login or
        // user, which is never what follows a column.
        var next = SkipWhitespaceAndComments(sql, end, dialect);
        var nextEnd = next;
        while (nextEnd < sql.Length && IsWordChar(sql[nextEnd], dialect))
        {
            nextEnd++;
        }

        return ClassifyWord(sql.Slice(next, nextEnd - next)) is not
            (SqlWord.With or SqlWord.Rename or SqlWord.Identified or SqlWord.PasswordKeyword);
    }

    // Chooses what the next token is expected to be: what the word before says (a name or a password to redact), or else a password
    // which is still expected after the prefix of its literal (e.g. the N of N'password'), or else a name for the summary.
    private static PendingToken ChoosePending(PendingToken fromWord, bool capturesSummaryName, in ParseState state)
    {
        if (fromWord != PendingToken.None)
        {
            return fromWord;
        }

        if (state.NextIsPassword)
        {
            return PendingToken.Password;
        }

        return capturesSummaryName ? PendingToken.SummaryName : PendingToken.None;
    }

    // Looks at a word which has been parsed, with the words before it, for the names of logins and users and for the
    // passwords which are not literals. Returns what is expected next, which is the name of a login or user if it is:
    // - LOGIN or USER after CREATE [OR REPLACE], ALTER or DROP (e.g. DROP USER name), unless it is the name of a column in ALTER TABLE.
    //   It is also after MODIFY (Teradata) which starts a statement, as it is not in other places (e.g. a column in
    //   SELECT modify user FROM t, or ALTER TABLE t MODIFY user int).
    // - LOGIN after FOR, FROM or WITH in such a statement (e.g. CREATE USER name FOR LOGIN name).
    // - TO after RENAME in such a statement (e.g. ALTER USER name RENAME TO name).
    // It is a password after IDENTIFIED BY, and after REPLACE if it follows a password, unless it is a literal,
    // which is sanitized as one. The passwords of MySQL and MariaDB are always literals, and a word which follows IDENTIFIED BY
    // is a modifier (e.g. RANDOM PASSWORD).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PendingToken Observe(ReadOnlySpan<char> sql, int end, SqlWord word, ref ParseState state)
    {
        // Most words are not looked for, and only need to be remembered as the word before the next one.
        if (!state.TracksAccounts)
        {
            return PendingToken.None;
        }

        if (word == SqlWord.Other)
        {
            // A string prefix (e.g. E of UESCAPE E'!') is part of the escape clause, not another word after it.
            if (state.PreviousWord != SqlWord.Uescape || end >= sql.Length || sql[end] != SingleQuoteChar)
            {
                PushWord(SqlWord.Other, ref state);
            }

            return PendingToken.None;
        }

        return ObserveWord(sql, end, word, ref state);
    }

    private static PendingToken ObserveWord(ReadOnlySpan<char> sql, int end, SqlWord word, ref ParseState state)
    {
        var pendingName = state.NextIsAccountName;
        var previous = state.PreviousWord;
        var previousPrevious = state.PreviousPreviousWord;

        if (word == SqlWord.Replace && previous == SqlWord.Or && previousPrevious == SqlWord.Create)
        {
            word = SqlWord.CreateOrReplace;
        }

        PushWord(word, ref state);

        // The name of a login or user which has been redacted does not start anything.
        if (word == SqlWord.AccountName)
        {
            return PendingToken.None;
        }

        // IF [NOT] EXISTS can come between LOGIN or USER and its name (e.g. DROP USER IF EXISTS name).
        if (pendingName && IsIfNotOrExists(word))
        {
            return PendingToken.AccountName;
        }

        // Another statement starts, which does not have the names of the statement which precedes it.
        if (state.Statement != StatementContext.None && !pendingName && IsStatementStart(word))
        {
            EndStatementContext(word, ref state);
        }

        if (word == SqlWord.Table)
        {
            if (previous == SqlWord.Alter)
            {
                state.Statement |= StatementContext.AlterTable;
            }

            return PendingToken.None;
        }

        if (!Has(state.Dialect, SqlDialect.NoBareWordPasswords))
        {
            // The password of IDENTIFIED BY password, of REPLACE old_password after it, and of PASSWORD = password (Teradata).
            if ((word == SqlWord.By && previous == SqlWord.Identified) ||
                (word == SqlWord.Replace && previous == SqlWord.Password) ||
                (word == SqlWord.PasswordKeyword && state.InLoginOrUserStatement && IsFollowedByEquals(sql, end, state.Dialect)))
            {
                return PendingToken.Password;
            }
        }

        switch (word)
        {
            case SqlWord.Role:
                // DEFAULT ROLE ends MySQL's account list; its commas separate role names.
                if (previous == SqlWord.Default)
                {
                    state.Statement &= ~StatementContext.AccountList;
                }

                return PendingToken.None;

            case SqlWord.To:
                if (state.InLoginOrUserStatement &&
                    (previous == SqlWord.Rename || (state.Statement & StatementContext.RenameUser) != 0))
                {
                    return PendingToken.AccountName;
                }

                return PendingToken.None;

            case SqlWord.User:
            case SqlWord.Login:
                var isDrop = previous == SqlWord.Drop;

                if (previous is SqlWord.Create or SqlWord.CreateOrReplace ||
                    (previous == SqlWord.Modify && previousPrevious == SqlWord.StatementEnd) ||
                    ((isDrop || previous == SqlWord.Alter) && !(state.InAlterTable && IsColumnOperation(sql, end, state.Dialect, isDrop))) ||
                    (word == SqlWord.User && previous == SqlWord.Rename && state.Dialect == SqlDialect.MySql))
                {
                    state.Statement |= StatementContext.LoginOrUser;

                    if (state.Dialect == SqlDialect.MySql)
                    {
                        state.Statement |= StatementContext.AccountList;

                        if (previous == SqlWord.Rename)
                        {
                            state.Statement |= StatementContext.RenameUser;
                        }
                    }

                    if (isDrop)
                    {
                        state.Statement |= StatementContext.DropLoginOrUser;
                    }

                    return PendingToken.AccountName;
                }

                if (word == SqlWord.Login &&
                    state.InLoginOrUserStatement &&
                    previous is SqlWord.For or SqlWord.From or SqlWord.With)
                {
                    return PendingToken.AccountName;
                }

                return PendingToken.None;

            default:
                return PendingToken.None;
        }
    }

    // The statement ends when another statement starts, which is not always marked with a semicolon.
    private static void EndStatementContext(SqlWord word, ref ParseState state)
    {
        // ALTER and DROP are also clauses of ALTER TABLE.
        state.Statement = word is SqlWord.Alter or SqlWord.Drop
            ? state.Statement & StatementContext.AlterTable
            : StatementContext.None;
    }

    // Updates the statement which creates, alters or drops a login or user for a character which is a token on its own.
    private static void ParseLoginOrUserStatementCharacter(char currentChar, ref ParseState state)
    {
        switch (currentChar)
        {
            case AtChar:
                // MySQL account names can quote the user separately from a bare host.
                if (state.Dialect == SqlDialect.MySql && state.PreviousWord == SqlWord.AccountName)
                {
                    state.Pending = PendingToken.AccountName;
                }

                break;

            case EqualsChar:
                // NAME = new_name and LOGIN = login_name (e.g. ALTER LOGIN name WITH NAME = new_name)
                if (state.PreviousWord is SqlWord.Name or SqlWord.Login)
                {
                    state.Pending = PendingToken.AccountName;
                }

                break;

            case CommaChar:
                // MySQL account definitions can have authentication/options before the next account.
                // In other dialects only DROP USER lists accounts directly after a name.
                if ((state.Statement & StatementContext.AccountList) != 0 ||
                    (state.InDropLoginOrUserStatement && state.PreviousWord is SqlWord.AccountName or SqlWord.Uescape))
                {
                    state.Pending = PendingToken.AccountName;
                }

                break;

            case SemicolonChar:
                if (state.NextIsAccountName)
                {
                    state.Pending = PendingToken.None;
                }

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

        // A variable is not a password, nor is a string in dollar quotes (e.g. $$password$$), which is a literal.
        if (!IsWordChar(sql[start]) ||
            sql[start] == AtChar ||
            TryFindDollarQuotedLiteralEnd(sql, start, state.Dialect, out _))
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
        var isValues = ClassifyWord(sql.Slice(start, end - start)) == SqlWord.Values &&
            SkipWhitespaceAndComments(sql, end, state.Dialect) is var next &&
            next < sql.Length &&
            sql[next] == SingleQuoteChar;

        if (isValues)
        {
            sql.Slice(start, end - start).CopyTo(buffer.Slice(state.SanitizedPosition));
            state.SanitizedPosition += end - start;
        }
        else
        {
            buffer[state.SanitizedPosition++] = SanitizationPlaceholder;
        }

        PushWord(isValues ? SqlWord.Other : SqlWord.Password, ref state);

        state.ParsePosition = end;
        state.Pending = PendingToken.None;
        state.PreviousTokenStartPosition = start;
        state.PreviousTokenEndPosition = end;

        return true;
    }
}
