// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation;

/// <summary>
/// Text made of the pieces of SQL which the sanitizer has to tell apart, in any order and without regard for
/// whether it is well formed SQL, built from a seed so that a failure can be replayed.
/// </summary>
public sealed class SqlSoup
{
    private static readonly string[] Pieces =
    [
        "'", "''", "\"", "`", "[", "]", "(", ")", ",", ";", "-", "--", "/", "*", "/*", "*/", "\\", "$", "$$", "$a$", "@", "#", "?", "=", ".", "%", ":",
        "N'", "E'", "X'", "0x", "0xFF", "1", "42", "-1", "1.5", "1e5", " ", "  ", "\n", "\r\n", "\t", "a", "b1", "_", "\u00e9", "\ud83d", "\0",
        "SELECT ", "INSERT INTO ", "UPDATE ", "DELETE FROM ", "FROM ", "WHERE ", "IN ", "IN (", "AS ", "JOIN ", "VALUES ", "SET ", "AND ", "OR ", "ARRAY[", "ARRAY ", "CREATE LOGIN ", "CREATE USER ", "EXEC ",
    ];

    public SqlSoup(int seed)
    {
        this.Seed = seed;

        var random = new Random(seed);
        var count = random.Next(0, 60);
        var text = new System.Text.StringBuilder();

        for (var i = 0; i < count; i++)
        {
            text.Append(Pieces[random.Next(Pieces.Length)]);
        }

        this.Text = text.ToString();
    }

    public int Seed { get; }

    public string Text { get; }

    public override string ToString() => $"Seed: {this.Seed}, Text: \"{this.Text}\"";
}
