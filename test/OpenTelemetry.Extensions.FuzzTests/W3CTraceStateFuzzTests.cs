// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using FsCheck.Xunit;

namespace OpenTelemetry.Trace;

/// <summary>
/// Property-based (fuzz) tests that assert the parsing and the updating of a <c>tracestate</c> header follow
/// the W3C Trace Context grammar whatever the header contains. The grammar used is the one of later levels of
/// the specification, in which a key is a lower case letter or a digit followed by up to 255 lower case letters,
/// digits and <c>_ - * / @</c>, and a value is up to 256 printable ASCII characters without <c>,</c> and <c>=</c>
/// which do not end in a space.
/// </summary>
public static class W3CTraceStateFuzzTests
{
    private const int MaxTest = 1_000;
    private const int MaxMembers = 32;

    private static readonly Regex KeyPattern = new(@"\A[a-z0-9][a-z0-9_\-*/@]{0,255}\z", RegexOptions.CultureInvariant);
    private static readonly Regex ValuePattern = new(@"\A[\x20-\x2B\x2D-\x3C\x3E-\x7E]{0,255}[\x21-\x2B\x2D-\x3C\x3E-\x7E]\z", RegexOptions.CultureInvariant);

    [Property(MaxTest = MaxTest)]
    public static void Parse_Arbitrary_Input_Does_Not_Throw_And_Agrees_With_TryParse(string? input)
    {
        var parsed = W3CTraceState.Parse(input);
        var isValid = W3CTraceState.TryParse(input, out var state);

        Assert.Equal(parsed.ToString(), state.ToString());

        // A header which is entirely valid is read back as it was written.
        if (isValid && input is { Length: > 0 } && input.IndexOfAny([' ', '\t', ',']) < 0)
        {
            Assert.Equal(input, state.ToString());
        }
    }

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(TraceStateGenerators)])]
    public static void Parse_Keeps_Exactly_The_Valid_Members_In_Order(TraceStateText input)
    {
        var (expectedMembers, expectedIsValid) = Reference(input.Text);

        var actualIsValid = W3CTraceState.TryParse(input.Text, out var state);

        Assert.Equal(Serialize(expectedMembers), state.ToString());
        Assert.Equal(expectedIsValid, actualIsValid);
    }

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(TraceStateGenerators)])]
    public static void Parse_Result_Always_Satisfies_The_Grammar(TraceStateText input)
    {
        var members = Members(W3CTraceState.Parse(input.Text));

        Assert.True(members.Count <= MaxMembers);
        Assert.Equal(members.Count, members.Select(m => m.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.All(members, m =>
        {
            Assert.Matches(KeyPattern, m.Key);
            Assert.Matches(ValuePattern, m.Value);
        });
    }

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(TraceStateGenerators)])]
    public static void Parse_Of_ToString_Is_Valid_And_Unchanged(TraceStateText input)
    {
        var first = W3CTraceState.Parse(input.Text).ToString();

        var isValid = W3CTraceState.TryParse(first, out var second);

        Assert.True(isValid, first);
        Assert.Equal(first, second.ToString());
    }

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(TraceStateGenerators)])]
    public static void Parse_Reads_Back_A_Valid_Header_Exactly(TraceStateMembers input)
    {
        var isValid = W3CTraceState.TryParse(input.Header, out var state);

        Assert.True(isValid, input.ToString());
        Assert.Equal(input.Header, state.ToString());

        foreach (var member in input.Members)
        {
            Assert.True(state.TryGetValue(member.Key, out var value), member.Key);
            Assert.Equal(member.Value, value);
        }
    }

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(TraceStateGenerators)])]
    public static void TryGetValue_Finds_Exactly_The_Members(TraceStateText input, TraceStateMember probe)
    {
        var state = W3CTraceState.Parse(input.Text);
        var members = Members(state);

        var found = state.TryGetValue(probe.Key, out var value);

        var expected = members.FirstOrDefault(m => string.Equals(m.Key, probe.Key, StringComparison.Ordinal));

        Assert.Equal(expected.Key is not null, found);
        Assert.Equal(expected.Value, value);
    }

    [Property(MaxTest = MaxTest)]
    public static void TryGetValue_Of_A_Null_Key_Is_Not_Found(string? input)
    {
        Assert.False(W3CTraceState.Parse(input).TryGetValue(null!, out var value));
        Assert.Null(value);
    }

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(TraceStateGenerators)])]
    public static void Set_Puts_A_Valid_Member_First_And_Leaves_An_Invalid_One_Out(TraceStateText input, TraceStateMember candidate)
    {
        var state = W3CTraceState.Parse(input.Text);
        var before = state.ToString();
        var members = Members(state);

        var updated = state.Set(candidate.Key, candidate.Value);

        List<KeyValuePair<string, string>> expected;
        if (IsValidKey(candidate.Key) && IsValidValue(candidate.Value))
        {
            // The updated member moves to the front and, if the list is full, the last member makes way for it.
            expected =
            [
                new KeyValuePair<string, string>(candidate.Key, candidate.Value),
                .. members.Where(m => !string.Equals(m.Key, candidate.Key, StringComparison.Ordinal)),
            ];

            expected = [.. expected.Take(MaxMembers)];
        }
        else
        {
            expected = members;
        }

        Assert.Equal(Serialize(expected), updated.ToString());

        // A tracestate is immutable, so the one which was updated is as it was.
        Assert.Equal(before, state.ToString());
    }

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(TraceStateGenerators)])]
    public static void Set_Result_Always_Satisfies_The_Grammar(TraceStateText input, TraceStateMember candidate)
    {
        var updated = W3CTraceState.Parse(input.Text).Set(candidate.Key, candidate.Value);

        var members = Members(updated);

        Assert.True(members.Count <= MaxMembers);
        Assert.Equal(members.Count, members.Select(m => m.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.True(W3CTraceState.TryParse(updated.ToString(), out _));
    }

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(TraceStateGenerators)])]
    public static void Remove_Drops_Only_The_Member_With_That_Key(TraceStateText input, TraceStateMember candidate)
    {
        var state = W3CTraceState.Parse(input.Text);
        var before = state.ToString();
        var members = Members(state);

        var updated = state.Remove(candidate.Key);

        var expected = members.Where(m => !string.Equals(m.Key, candidate.Key, StringComparison.Ordinal)).ToList();

        Assert.Equal(Serialize(expected), updated.ToString());
        Assert.False(updated.TryGetValue(candidate.Key, out _));
        Assert.Equal(before, state.ToString());
    }

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(TraceStateGenerators)])]
    public static void Set_Then_Remove_Leaves_The_Other_Members(TraceStateMembers input, TraceStateMember candidate)
    {
        if (!IsValidKey(candidate.Key) || !IsValidValue(candidate.Value) || input.Members.Count >= MaxMembers)
        {
            return;
        }

        var state = W3CTraceState.Parse(input.Header);

        var actual = state.Set(candidate.Key, candidate.Value).Remove(candidate.Key);

        var expected = input.Members.Where(m => !string.Equals(m.Key, candidate.Key, StringComparison.Ordinal)).ToList();

        Assert.Equal(Serialize(expected), actual.ToString());
    }

    private static bool IsValidKey(string key) => KeyPattern.IsMatch(key);

    private static bool IsValidValue(string value) => ValuePattern.IsMatch(value);

    private static string Serialize(IEnumerable<KeyValuePair<string, string>> members) =>
        string.Join(",", members.Select(m => $"{m.Key}={m.Value}"));

    private static List<KeyValuePair<string, string>> Members(W3CTraceState state)
    {
        // The only way to read every member of a tracestate is to read it as it is written.
        var text = state.ToString();

        var members = new List<KeyValuePair<string, string>>();

        if (!string.IsNullOrEmpty(text))
        {
            foreach (var member in text.Split(','))
            {
                var separator = member.IndexOf('=');

                members.Add(new KeyValuePair<string, string>(member.Substring(0, separator), member.Substring(separator + 1)));
            }
        }

        return members;
    }

    private static (List<KeyValuePair<string, string>> Members, bool IsValid) Reference(string text)
    {
        var members = new List<KeyValuePair<string, string>>();
        var parts = text.Split(',');

        // A list has at most 32 members, and the empty ones count.
        var isValid = parts.Length <= MaxMembers;

        foreach (var part in parts)
        {
            if (members.Count == MaxMembers)
            {
                break;
            }

            var member = part.Trim(' ', '\t');
            if (member.Length == 0)
            {
                continue;
            }

            var separator = member.IndexOf('=');
            if (separator <= 0)
            {
                isValid = false;
                continue;
            }

            var key = member.Substring(0, separator);
            var value = member.Substring(separator + 1);

            if (!IsValidKey(key) || !IsValidValue(value) || members.Any(m => string.Equals(m.Key, key, StringComparison.Ordinal)))
            {
                isValid = false;
                continue;
            }

            members.Add(new KeyValuePair<string, string>(key, value));
        }

        return (members, isValid);
    }
}
