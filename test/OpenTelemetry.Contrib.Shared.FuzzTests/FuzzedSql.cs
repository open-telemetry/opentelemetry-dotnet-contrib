// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation;

/// <summary>
/// The shape of a SQL statement, built from a seed so that a failure can be replayed. The shape is fixed by the
/// seed, but the content of every literal and of every comment is chosen anew each time the statement is rendered, and
/// contains a secret which must never be found in the sanitized statement. The statements are well formed for the
/// dialect they are in, but the literals and the comments contain the characters which delimit other things (quotes,
/// brackets, parentheses, comment markers), and the identifiers around them contain them too.
/// </summary>
public sealed class FuzzedSql(int seed)
{
    public int Seed { get; } = seed;

    /// <summary>
    /// Gets a value indicating whether the statement is for MySQL or MariaDB rather than for SQL Server, PostgreSQL and the like.
    /// </summary>
    public bool UseBackslashEscapes { get; } = seed % 3 == 0;

    public override string ToString() => $"Seed: {this.Seed}, UseBackslashEscapes: {this.UseBackslashEscapes}, Sql: {this.Render(0).Sql}";

    /// <summary>
    /// Renders the statement.
    /// </summary>
    /// <param name="contentSeed">The seed of the content of the literals and the comments.</param>
    /// <returns>The statement and the secrets in the literals and the comments of it.</returns>
    public (string Sql, IReadOnlyList<string> Secrets) Render(int contentSeed)
    {
        var builder = new Builder(new Random(this.Seed), new Random(contentSeed), this.UseBackslashEscapes);

        return (builder.BuildStatements(), builder.Secrets);
    }

    private sealed class Builder(Random structure, Random content, bool backslash)
    {
        private static readonly string[] BodyFragments =
        [
            "abc", " ", ")", "(", ",", "--", "/*", "*/", ";", "[", "]", "\"", "`", "$$", "''", "\\", "@x", " IN (", " OR 1=1 -- ", "\n", "%", "\u00e9", "#",
        ];

        private static readonly string[] IdentifierFragments = ["a", "b b", "'", "(", ")", ",", "--", "/*", "-", ".", "x", "''"];

        private static readonly string[] Whitespace = [" ", " ", " ", "  ", "\n", "\t", "\r\n"];

        private readonly List<string> secrets = [];

        public IReadOnlyList<string> Secrets => this.secrets;

        public string BuildStatements()
        {
            var statement = this.Statement();

            return structure.Next(5) == 0 ? statement + ";" + this.Ws() + this.Statement() : statement;
        }

        private static string Escape(string fragment, string kind) => kind switch
        {
            "single-backslash" => fragment == "\\" ? "\\\\" : fragment == "'" ? "\\'" : fragment,
            "double-backslash" => fragment == "\\" ? "\\\\" : fragment == "\"" ? "\\\"" : fragment,
            "dollar" => fragment == "$$" ? "$x" : fragment,
            _ => fragment,
        };

        private string Statement() => structure.Next(5) switch
        {
            0 => this.Select(),
            1 => this.Insert(),
            2 => this.Update(),
            3 => this.Delete(),
            _ => this.Exec(),
        };

        private string Select()
        {
            var columns = structure.Next(4) switch
            {
                0 => "*",
                1 => "COUNT(*)",
                2 => this.Literal() + " AS " + this.Identifier(),
                _ => this.List(1, 3, this.Identifier),
            };

            var sql = $"SELECT{this.Ws()}{columns}{this.Ws()}FROM{this.Ws()}{this.List(1, 3, this.Identifier)}";

            if (structure.Next(4) != 0)
            {
                sql += $"{this.Ws()}WHERE{this.Ws()}{this.Conditions()}";
            }

            if (structure.Next(3) == 0)
            {
                sql += $"{this.Ws()}ORDER BY{this.Ws()}{this.Identifier()}";
            }

            return sql;
        }

        private string Insert() =>
            $"INSERT INTO{this.Ws()}{this.Identifier()}{this.Ws()}({this.List(1, 3, this.Identifier)}){this.Ws()}VALUES{this.Ws()}" +
            this.List(1, 2, () => $"({this.List(1, 3, this.Literal)})");

        private string Update()
        {
            var sql = $"UPDATE{this.Ws()}{this.Identifier()}{this.Ws()}SET{this.Ws()}" +
                this.List(1, 3, () => $"{this.Identifier()}{this.Operator()}{this.Literal()}");

            return structure.Next(4) != 0 ? $"{sql}{this.Ws()}WHERE{this.Ws()}{this.Conditions()}" : sql;
        }

        private string Delete() =>
            $"DELETE FROM{this.Ws()}{this.Identifier()}{this.Ws()}WHERE{this.Ws()}{this.Conditions()}";

        private string Exec() =>
            $"EXEC{this.Ws()}{this.Identifier()}{this.Ws()}" +
            this.List(1, 3, () => $"@p{structure.Next(10)}{this.Operator()}{this.Literal()}");

        private string Conditions() => this.List(1, 3, this.Condition, () => $"{this.Ws()}{(structure.Next(2) == 0 ? "AND" : "OR")}{this.Ws()}");

        private string Condition() => structure.Next(12) switch
        {
            0 => $"{this.Identifier()}{this.Operator()}{this.Literal()}",
            1 => $"{this.Identifier()}{this.Ws()}IN{this.Ws()}({this.List(1, 4, this.Literal)})",
            2 => $"{this.Identifier()}{this.Ws()}IN({this.List(1, 4, this.Literal)})",
            3 => $"{this.Identifier()}{this.Ws()}BETWEEN{this.Ws()}{this.Literal()}{this.Ws()}AND{this.Ws()}{this.Literal()}",
            4 => $"{this.Identifier()}{this.Ws()}LIKE{this.Ws()}{this.Literal()}",
            5 => $"fn({this.Identifier()}, {this.Literal()}){this.Operator()}{this.Literal()}",
            6 => $"({this.Identifier()}{this.Operator()}{this.Literal()})",
            7 => $"NOT{this.Ws()}{this.Identifier()}{this.Operator()}{this.Literal()}",
            8 => $"{this.Identifier()}{this.Ws()}IN{this.Ws()}(SELECT{this.Ws()}{this.Identifier()}{this.Ws()}FROM{this.Ws()}{this.Identifier()}{this.Ws()}WHERE{this.Ws()}{this.Identifier()}{this.Operator()}{this.Literal()})",
            10 when !backslash => $"{this.Identifier()}{this.Operator()}{this.ArrayOfValues()}",
            11 when !backslash => $"{this.Identifier()}{this.Ws()}IN{this.Ws()}UNNEST({this.ArrayOfValues()})",
            _ => $"{this.Identifier()}{this.Ws()}IN{this.Ws()}({this.List(2, 6, this.Literal)}){this.Ws()}OR{this.Ws()}{this.Identifier()}{this.Operator()}{this.Literal()}",
        };

        private string ArrayOfValues()
        {
            // A list of values in brackets, which some dialects have (e.g. GoogleSQL and DuckDB). It has comments between
            // its values which have a closing bracket as text, which is then the first one which is found.
            var items = this.List(1, 4, this.Literal, () => this.ArrayWs() + "," + this.ArrayWs());

            return "[" + this.ArrayWs() + items + this.ArrayWs() + "]";
        }

        private string ArrayWs() => structure.Next(3) switch
        {
            0 => $" /* {this.Secret()} ] {this.Body("block")} */ ",
            1 => $" -- {this.Secret()} ] {this.Body("line")}\n",
            _ => this.Ws(),
        };

        private string Operator() => structure.Next(3) switch
        {
            0 => "=",
            1 => " = ",
            _ => this.Ws() + "=" + this.Ws(),
        };

        private string List(int min, int max, Func<string> item, Func<string>? separator = null)
        {
            var count = structure.Next(min, max + 1);
            var items = new List<string>(count);

            for (var i = 0; i < count; i++)
            {
                items.Add(item());
            }

            return string.Join(separator is null ? structure.Next(2) == 0 ? ", " : "," : separator(), items);
        }

        private string Ws()
        {
            // Mostly whitespace, and sometimes a comment as well, which a statement may have anywhere.
            if (structure.Next(8) == 0)
            {
                return structure.Next(backslash ? 3 : 2) switch
                {
                    0 => $" /* {this.Secret()}{this.Body("block")} */ ",
                    1 => $" -- {this.Secret()}{this.Body("line")}\n",
                    _ => $" # {this.Secret()}{this.Body("line")}\n",
                };
            }

            return Whitespace[structure.Next(Whitespace.Length)];
        }

        private string Identifier()
        {
            var kind = structure.Next(backslash ? 3 : 4);
            var name = $"name{structure.Next(100)}";

            if (kind == 0)
            {
                return name;
            }

            var inner = new List<string>();
            for (var i = structure.Next(1, 4); i > 0; i--)
            {
                inner.Add(IdentifierFragments[structure.Next(IdentifierFragments.Length)]);
            }

            var text = $"{name}{string.Concat(inner)}";

            if (backslash)
            {
                // A backtick cannot be in the name without being doubled, and none of the fragments contain one.
                return kind == 1 ? $"`{text}`" : $"{name}.`{text}`";
            }

            return kind switch
            {
                1 => $"[{text}]",
                2 => $"\"{text.Replace("\"", "\"\"")}\"",
                _ => $"[{name}].[{text}]",
            };
        }

        private string Literal()
        {
            var kind = structure.Next(backslash ? 7 : 10);

            if (backslash)
            {
                return kind switch
                {
                    0 or 1 => $"'{this.Body("single-backslash")}'",
                    2 or 3 => $"\"{this.Body("double-backslash")}\"",
                    4 => this.Number(),
                    5 => this.Number("0x"),
                    _ => "''",
                };
            }

            return kind switch
            {
                0 or 1 => $"'{this.Body("single")}'",
                2 => $"N'{this.Body("single")}'",
                3 => $"E'{this.Body("single-backslash")}'",
                4 => $"$${this.Body("dollar")}$$",
                5 => $"$tag${this.Body("dollar")}$tag$",
                6 => this.Number(),
                7 => this.Number("0x"),
                8 => this.Number("-"),
                _ => "''",
            };
        }

        private string Number(string prefix = "")
        {
            var secret = prefix == "0x" ? $"7391ABCDEF{this.secrets.Count}" : $"7391846205{this.secrets.Count}";
            this.secrets.Add(secret);

            return prefix + secret + (prefix.Length == 0 && structure.Next(3) == 0 ? ".5" : string.Empty);
        }

        private string Secret()
        {
            var secret = $"zqsecret{this.secrets.Count}x";
            this.secrets.Add(secret);

            return secret;
        }

        private string Body(string kind)
        {
            var parts = new List<string>();

            for (var i = content.Next(0, 6); i > 0; i--)
            {
                parts.Add(Escape(BodyFragments[content.Next(BodyFragments.Length)], kind));
            }

            // The secret is anywhere in the text, so it may be right after a delimiter or right before one.
            parts.Insert(content.Next(parts.Count + 1), this.Secret());

            var body = string.Concat(parts);

            // A comment ends at its end marker and a line comment at the end of the line, so neither can contain them,
            // and a block comment which contains the start of another is nested in some dialects and not in others.
            return kind switch
            {
                "block" => body.Replace("*/", "* /").Replace("/*", "/ *"),
                "line" => body.Replace("\n", " "),
                _ => body,
            };
        }
    }
}
