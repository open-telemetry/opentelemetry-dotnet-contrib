// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using FsCheck;
using FsCheck.Xunit;
using OpenTelemetry.Instrumentation.Kusto.Implementation;

namespace OpenTelemetry.Instrumentation.Kusto.FuzzTests;

public static class KustoProcessorTests
{
    private const int MaxTest = 500;

    private static readonly string[] Shapes = ["parens", "pipes", "calls", "binary"];

    [Property(MaxTest = MaxTest)]
    public static void Process_Arbitrary_Input_Does_Not_Throw(NonEmptyString input)
    {
        // The summarize path runs full semantic analysis and the sanitize path walks the syntax tree, so
        // arbitrary (often malformed) KQL is the interesting fuzz target.
        var exception = Record.Exception(() => KustoProcessor.Process(shouldSummarize: true, shouldSanitize: true, input.Get));

        Assert.Null(exception);
    }

    [Property(MaxTest = MaxTest)]
    public static void Process_Empty_Input_Does_Not_Throw()
    {
        var exception = Record.Exception(() => KustoProcessor.Process(shouldSummarize: true, shouldSanitize: true, string.Empty));

        Assert.Null(exception);
    }

    [Property(MaxTest = MaxTest)]
    public static void Process_Neither_Option_Returns_Nulls(NonEmptyString input)
    {
        var info = KustoProcessor.Process(shouldSummarize: false, shouldSanitize: false, input.Get);

        Assert.Null(info.Summarized);
        Assert.Null(info.Sanitized);
    }

    [Property(MaxTest = MaxTest)]
    public static void Process_Only_Summarize_Leaves_Sanitized_Null(NonEmptyString input)
    {
        var info = KustoProcessor.Process(shouldSummarize: true, shouldSanitize: false, input.Get);

        Assert.Null(info.Sanitized);
    }

    [Property(MaxTest = MaxTest)]
    public static void Process_Is_Deterministic(NonEmptyString input)
    {
        var first = KustoProcessor.Process(shouldSummarize: true, shouldSanitize: true, input.Get);
        var second = KustoProcessor.Process(shouldSummarize: true, shouldSanitize: true, input.Get);

        Assert.Equal(first.Summarized, second.Summarized);
        Assert.Equal(first.Sanitized, second.Sanitized);
    }

    [Property(MaxTest = MaxTest)]
    public static void Process_Summary_Is_Length_Bounded(NonEmptyString input)
    {
        var query = $"StormEvents | where State == \"{Alphanumeric(input.Get)}\" | project {Alphanumeric(input.Get)}";

        var info = KustoProcessor.Process(shouldSummarize: true, shouldSanitize: false, query);

        Assert.True(info.Summarized is null || info.Summarized.Length <= 255);
    }

    [Property(MaxTest = MaxTest)]
    public static void Process_Numeric_Literal_Is_Sanitized_Or_Omitted(PositiveInt number)
    {
        // Offset to make the literal large and unlikely to collide with anything else in the text.
        var literal = (number.Get + 100_000).ToString(CultureInfo.InvariantCulture);
        var query = $"StormEvents | where EventId == {literal}";

        var info = KustoProcessor.Process(shouldSummarize: false, shouldSanitize: true, query);

        // A non-parameterized literal is either replaced with the placeholder or the whole text is omitted.
        if (info.Sanitized is not null)
        {
            Assert.DoesNotContain(literal, info.Sanitized);
            Assert.Contains("?", info.Sanitized);
        }
    }

    [Property(MaxTest = MaxTest)]
    public static void Process_String_Literal_Is_Sanitized_Or_Omitted(NonEmptyString input)
    {
        var secret = "secret_" + Alphanumeric(input.Get);
        var query = $"StormEvents | where State == \"{secret}\"";

        var info = KustoProcessor.Process(shouldSummarize: false, shouldSanitize: true, query);

        if (info.Sanitized is not null)
        {
            Assert.DoesNotContain(secret, info.Sanitized);
        }
    }

    [Property(MaxTest = MaxTest)]
    public static void Process_Compound_String_Literal_Is_Sanitized_Or_Omitted(NonEmptyString input)
    {
        var secret = "secret_" + Alphanumeric(input.Get);

        // Adjacent string literals form a single CompoundStringLiteralExpression.
        var query = $"StormEvents | where State == \"{secret}\" \"_suffix\"";

        var info = KustoProcessor.Process(shouldSummarize: false, shouldSanitize: true, query);

        if (info.Sanitized is not null)
        {
            Assert.DoesNotContain(secret, info.Sanitized);
        }
    }

    [Property(MaxTest = MaxTest)]
    public static void Process_Inline_Data_Island_Is_Sanitized_Or_Omitted(NonEmptyString input)
    {
        var secret = "secret_" + Alphanumeric(input.Get);

        // Raw rows after "<|" in a control command are an InputTextToken, not parsed literals.
        var query = $".ingest inline into table T <| 1,{secret},3";

        var info = KustoProcessor.Process(shouldSummarize: false, shouldSanitize: true, query);

        if (info.Sanitized is not null)
        {
            Assert.DoesNotContain(secret, info.Sanitized);
        }
    }

    [Property(MaxTest = MaxTest)]
    public static void Process_Parameterized_Query_Is_Returned_Verbatim(PositiveInt number)
    {
        var literal = (number.Get + 100_000).ToString(CultureInfo.InvariantCulture);
        var query = $"declare query_parameters(Threshold:long);\nStormEvents | where EventId == Threshold and Count == {literal}";

        var info = KustoProcessor.Process(shouldSummarize: false, shouldSanitize: true, query);

        // Parameterized queries are not sanitized per the semantic conventions, so the text is returned
        // unchanged, including its literals.
        Assert.Equal(query, info.Sanitized);
        Assert.Contains(literal, info.Sanitized);
    }

    [Property(MaxTest = MaxTest)]
    public static void Process_Very_Long_Input_Does_Not_Throw(PositiveInt length)
    {
        var count = Math.Min(length.Get, 10_000);
        var query = "StormEvents | where Data == \"" + new string('a', count) + "\"";

        var exception = Record.Exception(() => KustoProcessor.Process(shouldSummarize: true, shouldSanitize: true, query));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("parens")]
    [InlineData("pipes")]
    [InlineData("calls")]
    [InlineData("binary")]
    public static void Process_Deeply_Nested_Query_Fails_Safe(string shape)
    {
        // Deeply nested KQL must not overflow the stack while the recursive visitors walk the syntax tree.
        // The depth is well past the visitors' stack limit, so the guard must engage and the call must
        // return a safe result instead of crashing. (The Kusto parser itself tolerates this depth.)
        const int depth = 5_000;
        const string secret = "secret_value_0xDEADBEEF";
        var query = BuildNested(shape, depth, secret);

        var info = KustoProcessor.Process(shouldSummarize: true, shouldSanitize: true, query);

        // Fail-safe: the query text is either omitted or fully redacted, never emitted with the literal intact.
        if (info.Sanitized is not null)
        {
            Assert.DoesNotContain(secret, info.Sanitized);
        }

        // The summary is always length-bounded and never crashes.
        Assert.True(info.Summarized is null || info.Summarized.Length <= 255);
    }

    [Property(MaxTest = 10)]
    public static void Process_Randomly_Deep_Nesting_Does_Not_Crash(PositiveInt depth, NonNegativeInt shapeIndex)
    {
        // Vary the shape and a depth that is always past the visitors' stack limit.
        var d = 1_500 + (depth.Get % 3_500);
        var shape = Shapes[shapeIndex.Get % Shapes.Length];
        var query = BuildNested(shape, d, "secret_literal");

        var exception = Record.Exception(() => KustoProcessor.Process(shouldSummarize: true, shouldSanitize: true, query));

        Assert.Null(exception);
    }

    [Property(MaxTest = MaxTest)]
    public static void Process_Unicode_Literal_Is_Sanitized_Or_Omitted(NonEmptyString input)
    {
        // Surrogate pairs (the emoji) exercise the char-offset edits the sanitizer applies to string literals.
        var secret = "secret_\uD83D\uDE00_" + Alphanumeric(input.Get);
        var query = $"StormEvents | where State == \"{secret}\"";

        var info = KustoProcessor.Process(shouldSummarize: false, shouldSanitize: true, query);

        if (info.Sanitized is not null)
        {
            Assert.DoesNotContain(secret, info.Sanitized);
        }
    }

    private static string BuildNested(string shape, int depth, string literal) => shape switch
    {
        "parens" => "print x = " + new string('(', depth) + $"\"{literal}\"" + new string(')', depth),
        "pipes" => "StormEvents" + string.Concat(Enumerable.Repeat($"\n| where State == \"{literal}\"", depth)),
        "calls" => "print x = " + string.Concat(Enumerable.Repeat("tostring(", depth)) + $"\"{literal}\"" + new string(')', depth),
        "binary" => $"print x = \"{literal}\"" + string.Concat(Enumerable.Repeat($" and \"{literal}\"", depth)),
        _ => $"print x = \"{literal}\"",
    };

    private static string Alphanumeric(string value)
    {
        var result = new string([.. value.Where(char.IsLetterOrDigit).Take(32)]);
        return string.IsNullOrEmpty(result) ? "value" : result;
    }
}
