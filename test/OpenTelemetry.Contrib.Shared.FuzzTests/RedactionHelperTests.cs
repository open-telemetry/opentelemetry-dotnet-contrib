// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck.Xunit;

namespace OpenTelemetry.Internal;

public static class RedactionHelperTests
{
    private const int MaxValue = 1_000;

    [Property(MaxTest = MaxValue)]
    public static void GetRedactedQueryString_Arbitrary_Input_Matches_Reference(string? input)
    {
        var query = input ?? string.Empty;

        Assert.Equal(Reference(query), RedactionHelper.GetRedactedQueryString(query));
    }

    [Property(MaxTest = MaxValue, Arbitrary = [typeof(RedactionGenerators)])]
    public static void GetRedactedQueryString_Structured_Input_Matches_Reference(QueryText input) =>
        Assert.Equal(Reference(input.Text), RedactionHelper.GetRedactedQueryString(input.Text));

    [Property(MaxTest = MaxValue, Arbitrary = [typeof(RedactionGenerators)])]
    public static void GetRedactedQueryString_Without_A_Value_Returns_The_Input(QueryText input)
    {
        var query = input.Text.Replace("=", string.Empty);

        Assert.Same(query, RedactionHelper.GetRedactedQueryString(query));
    }

    [Property(MaxTest = MaxValue, Arbitrary = [typeof(RedactionGenerators)])]
    public static void GetRedactedQueryString_Is_Idempotent(QueryText input)
    {
        var once = RedactionHelper.GetRedactedQueryString(input.Text);

        Assert.Equal(once, RedactionHelper.GetRedactedQueryString(once!));
    }

    [Property(MaxTest = MaxValue, Arbitrary = [typeof(RedactionGenerators)])]
    public static void GetRedactedQueryString_Preserves_The_Parameter_Separators(QueryText input)
    {
        var actual = RedactionHelper.GetRedactedQueryString(input.Text)!;

        Assert.Equal(input.Text.Count(c => c == '&'), actual.Count(c => c == '&'));
    }

    [Property(MaxTest = MaxValue, Arbitrary = [typeof(RedactionGenerators)])]
    public static void GetRedactedQueryString_Never_Reveals_A_Value(SecretQuery input)
    {
        var actual = RedactionHelper.GetRedactedQueryString(input.Query)!;

        Assert.DoesNotContain(SecretQuery.Secret, actual, StringComparison.Ordinal);
    }

    [Property(MaxTest = MaxValue, Arbitrary = [typeof(RedactionGenerators)])]
    public static void GetRedactedQueryString_With_Bounds_Matches_Reference(EmbeddedQuery input)
    {
        var expected = input.Prefix + Reference(input.Query) + input.Suffix;

        var actual = RedactionHelper.GetRedactedQueryString(
            input.Value,
            input.Prefix.Length,
            input.Prefix.Length + input.Query.Length);

        Assert.Equal(expected, actual);
    }

    [Property(MaxTest = MaxValue, Arbitrary = [typeof(RedactionGenerators)])]
    public static void GetRedactedQueryString_With_Bounds_Agrees_With_Query_Only_Overload(EmbeddedQuery input)
    {
        var start = input.Prefix.Length;
        var end = start + input.Query.Length;

        var withBounds = RedactionHelper.GetRedactedQueryString(input.Value, start, end);
        var queryOnly = RedactionHelper.GetRedactedQueryString(input.Query)!;

        Assert.Equal(input.Prefix + queryOnly + input.Suffix, withBounds);
    }

    [Property(MaxTest = MaxValue, Arbitrary = [typeof(RedactionGenerators)])]
    public static void GetRedactedQueryString_With_Bounds_Without_A_Value_Returns_The_Input(EmbeddedQuery input)
    {
        var query = input.Query.Replace("=", string.Empty);
        var value = input.Prefix + query + input.Suffix;

        Assert.Same(value, RedactionHelper.GetRedactedQueryString(value, input.Prefix.Length, input.Prefix.Length + query.Length));
    }

    [Property(MaxTest = MaxValue, Arbitrary = [typeof(RedactionGenerators)])]
    public static void GetRedactedQueryString_With_Bounds_Never_Reveals_A_Value(SecretQuery input)
    {
        var value = $"https://example.com/path?{input.Query}#fragment";
        var start = value.IndexOf('?') + 1;
        var end = start + input.Query.Length;

        var actual = RedactionHelper.GetRedactedQueryString(value, start, end);

        Assert.DoesNotContain(SecretQuery.Secret, actual, StringComparison.Ordinal);
        Assert.StartsWith("https://example.com/path?", actual, StringComparison.Ordinal);
        Assert.EndsWith("#fragment", actual, StringComparison.Ordinal);
    }

    private static string Reference(string query) =>
        string.Join(
            "&",
            query.Split('&').Select(parameter =>
            {
                var index = parameter.IndexOf('=');

                return index < 0 ? parameter : parameter.Remove(index) + "=Redacted";
            }));
}
