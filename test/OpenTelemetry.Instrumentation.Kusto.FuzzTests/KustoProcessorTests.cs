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

    private static string Alphanumeric(string value)
    {
        var result = new string([.. value.Where(char.IsLetterOrDigit).Take(32)]);
        return string.IsNullOrEmpty(result) ? "value" : result;
    }
}
