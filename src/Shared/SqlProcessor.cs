// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Text;

namespace OpenTelemetry.Instrumentation;

internal static class SqlProcessor
{
    private const int CacheCapacity = 1000;
    private static readonly Hashtable Cache = [];

    public static SqlStatementInfo GetSanitizedSql(string? sql)
    {
        if (sql == null)
        {
            return default;
        }

        if (Cache[sql] is not SqlStatementInfo sqlStatementInfo)
        {
            sqlStatementInfo = SanitizeSql(sql);

            if (Cache.Count == CacheCapacity)
            {
                return sqlStatementInfo;
            }

            lock (Cache)
            {
                if ((Cache[sql] as string) == null)
                {
                    if (Cache.Count < CacheCapacity)
                    {
                        Cache[sql] = sqlStatementInfo;
                    }
                }
            }
        }

        return sqlStatementInfo;
    }

    private static SqlStatementInfo SanitizeSql(string sql)
    {
        var state = new SqlProcessorState();

        for (var i = 0; i < sql.Length; ++i)
        {
            if (SkipComment(sql, ref i))
            {
                continue;
            }

            if (SanitizeStringLiteral(sql, ref i) ||
                SanitizeHexLiteral(sql, ref i) ||
                SanitizeNumericLiteral(sql, ref i))
            {
                state.SanitizedSql.Append('?');
                continue;
            }

            WriteToken(sql, ref i, state);
        }

        return new SqlStatementInfo(
            state.SanitizedSql.ToString(),
            state.DbQuerySummary.ToString());
    }

    private static bool SkipComment(string sql, ref int index)
    {
        var i = index;
        var ch = sql[i];
        var length = sql.Length;

        // Scan past multi-line comment
        if (ch == '/' && i + 1 < length && sql[i + 1] == '*')
        {
            for (i += 2; i < length; ++i)
            {
                ch = sql[i];
                if (ch == '*' && i + 1 < length && sql[i + 1] == '/')
                {
                    i += 1;
                    break;
                }
            }

            index = i;
            return true;
        }

        // Scan past single-line comment
        if (ch == '-' && i + 1 < length && sql[i + 1] == '-')
        {
            for (i += 2; i < length; ++i)
            {
                ch = sql[i];
                if (ch is '\r' or '\n')
                {
                    i -= 1;
                    break;
                }
            }

            index = i;
            return true;
        }

        return false;
    }

    private static bool SanitizeStringLiteral(string sql, ref int index)
    {
        var ch = sql[index];
        if (ch == '\'')
        {
            var i = index + 1;
            var length = sql.Length;
            for (; i < length; ++i)
            {
                ch = sql[i];
                if (ch == '\'' && i + 1 < length && sql[i + 1] == '\'')
                {
                    ++i;
                    continue;
                }

                if (ch == '\'')
                {
                    break;
                }
            }

            index = i;
            return true;
        }

        return false;
    }

    private static bool SanitizeHexLiteral(string sql, ref int index)
    {
        var i = index;
        var ch = sql[i];
        var length = sql.Length;

        if (ch == '0' && i + 1 < length && (sql[i + 1] == 'x' || sql[i + 1] == 'X'))
        {
            for (i += 2; i < length; ++i)
            {
                ch = sql[i];
                if (char.IsDigit(ch) ||
                    ch == 'A' || ch == 'a' ||
                    ch == 'B' || ch == 'b' ||
                    ch == 'C' || ch == 'c' ||
                    ch == 'D' || ch == 'd' ||
                    ch == 'E' || ch == 'e' ||
                    ch == 'F' || ch == 'f')
                {
                    continue;
                }

                i -= 1;
                break;
            }

            index = i;
            return true;
        }

        return false;
    }

    private static bool SanitizeNumericLiteral(string sql, ref int index)
    {
        var i = index;
        var ch = sql[i];
        var length = sql.Length;

        // Scan past leading sign
        if ((ch == '-' || ch == '+') && i + 1 < length && (char.IsDigit(sql[i + 1]) || sql[i + 1] == '.'))
        {
            i += 1;
            ch = sql[i];
        }

        // Scan past leading decimal point
        var periodMatched = false;
        if (ch == '.' && i + 1 < length && char.IsDigit(sql[i + 1]))
        {
            periodMatched = true;
            i += 1;
            ch = sql[i];
        }

        if (char.IsDigit(ch))
        {
            var exponentMatched = false;
            for (i += 1; i < length; ++i)
            {
                ch = sql[i];
                if (char.IsDigit(ch))
                {
                    continue;
                }

                if (!periodMatched && ch == '.')
                {
                    periodMatched = true;
                    continue;
                }

                if (!exponentMatched && (ch == 'e' || ch == 'E'))
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

            index = i;
            return true;
        }

        return false;
    }

    private static void WriteToken(string sql, ref int index, SqlProcessorState state)
    {
        var i = index;
        var ch = sql[i];

        if (LookAhead("SELECT", sql, ref i, state) ||
            LookAhead("UPDATE", sql, ref i, state) ||
            LookAhead("INSERT", sql, ref i, state) ||
            LookAhead("DELETE", sql, ref i, state) ||
            LookAheadDdl("CREATE", sql, ref i, state) ||
            LookAheadDdl("ALTER", sql, ref i, state) ||
            LookAheadDdl("DROP", sql, ref i, state) ||
            LookAhead("INTO", sql, ref i, state, false, true) ||
            LookAhead("FROM", sql, ref i, state, false, true) ||
            LookAhead("JOIN", sql, ref i, state, false, true))
        {
            i -= 1;
        }
        else if (char.IsLetter(ch) || ch == '_')
        {
            for (; i < sql.Length; i++)
            {
                ch = sql[i];
                if (char.IsLetter(ch) || ch == '_' || ch == '.' || char.IsDigit(ch))
                {
                    state.SanitizedSql.Append(ch);
                    continue;
                }

                break;
            }

            if (state.CaptureNextTokenAsTarget)
            {
                state.CaptureNextTokenAsTarget = false;
                var collection = sql.Substring(index, i - index);
                state.DbQuerySummary.Append(' ').Append(collection);
            }

            i -= 1;
        }
        else
        {
            state.SanitizedSql.Append(ch);
        }

        index = i;
    }

    private static bool LookAheadDdl(string operation, string sql, ref int index, SqlProcessorState state)
    {
        var initialIndex = index;

        if (LookAhead(operation, sql, ref index, state, false, false))
        {
            for (; index < sql.Length && char.IsWhiteSpace(sql[index]); ++index)
            {
                state.SanitizedSql.Append(sql[index]);
            }

            if (LookAhead("TABLE", sql, ref index, state, false, false) ||
                LookAhead("INDEX", sql, ref index, state, false, false) ||
                LookAhead("PROCEDURE", sql, ref index, state, false, false) ||
                LookAhead("VIEW", sql, ref index, state, false, false) ||
                LookAhead("DATABASE", sql, ref index, state, false, false))
            {
                state.CaptureNextTokenAsTarget = true;
            }

            for (var i = initialIndex; i < index; ++i)
            {
                state.DbQuerySummary.Append(sql[i]);
            }

            return true;
        }

        return false;
    }

    private static bool LookAhead(string compare, string sql, ref int index, SqlProcessorState state, bool isOperation = true, bool captureNextTokenAsTarget = false)
    {
        int i = index;
        var sqlLength = sql.Length;
        var compareLength = compare.Length;

        for (var j = 0; i < sqlLength && j < compareLength; ++i, ++j)
        {
            if (char.ToUpperInvariant(sql[i]) != compare[j])
            {
                return false;
            }
        }

        if (i >= sqlLength)
        {
            // when the sql ends with the beginning of the compare string
            return false;
        }

        var ch = sql[i];
        if (char.IsLetter(ch) || ch == '_' || char.IsDigit(ch))
        {
            return false;
        }

        if (isOperation)
        {
            if (state.DbQuerySummary.Length > 0)
            {
                state.DbQuerySummary.Append(' ');
            }

            for (var k = index; k < i; ++k)
            {
                state.DbQuerySummary.Append(sql[k]);
                state.SanitizedSql.Append(sql[k]);
            }
        }
        else
        {
            for (var k = index; k < i; ++k)
            {
                state.SanitizedSql.Append(sql[k]);
            }
        }

        index = i;
        state.CaptureNextTokenAsTarget = captureNextTokenAsTarget;
        return true;
    }

    internal class SqlProcessorState
    {
        public StringBuilder SanitizedSql { get; set; } = new StringBuilder();

        public StringBuilder DbQuerySummary { get; set; } = new StringBuilder();

        public bool CaptureNextTokenAsTarget { get; set; }
    }
}
