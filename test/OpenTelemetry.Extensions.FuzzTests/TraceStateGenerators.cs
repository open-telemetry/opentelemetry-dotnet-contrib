// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;

namespace OpenTelemetry.Trace;

public static class TraceStateGenerators
{
    private static readonly char[] KeyStartCharacters = [.. "abcxyz0189"];
    private static readonly char[] KeyCharacters = [.. "abcxyz0189_-*/@"];
    private static readonly char[] ValueCharacters = [.. "abcXYZ0189 :;/._-!\"#~@*'()"];
    private static readonly char[] NonBlankValueCharacters = [.. "abcXYZ0189:;/._-!\"#~@*'()"];

    // A mixture of things that break the key grammar.
    private static readonly string[] BadKeys =
    [
        string.Empty, "A", "aB", "a b", "a=b", "a,b", "_a", "-a", "aé", "a\tb", "a.b", " ", "UPPER",
    ];

    // A mixture of things that break the value grammar.
    private static readonly string[] BadValues =
    [
        string.Empty, " ", "a ", "a,b", "a=b", "=", ",", "é", "a\tb", "a\nb", "a\u007fb", "éé",
    ];

    public static Arbitrary<TraceStateText> TraceStateTextArbitrary()
    {
        var memberGen = Gen.Frequency(
            (10, ValidMember().Select(m => $"{m.Key}={m.Value}")),
            (2, from key in Gen.Elements(BadKeys) from value in ValidValue() select $"{key}={value}"),
            (2, from key in ValidKey() from value in Gen.Elements(BadValues) select $"{key}={value}"),
            (1, ValidKey()),
            (1, Gen.Constant(string.Empty)),
            (1, Gen.Constant("=")),
            (1, Gen.Constant("a=1")));

        var paddingGen = Gen.Elements(string.Empty, string.Empty, " ", "\t", " \t ");

        var gen =
            from count in Gen.Frequency((6, Gen.Choose(0, 8)), (3, Gen.Choose(28, 36)), (1, Gen.Choose(0, 70)))
            from members in memberGen.ListOf(count)
            from leading in paddingGen.ListOf(count)
            from trailing in paddingGen.ListOf(count)
            select new TraceStateText(
                string.Join(",", members.Select((m, i) => leading[i] + m + trailing[i])));

        return gen.ToArbitrary();
    }

    public static Arbitrary<TraceStateMembers> TraceStateMembersArbitrary()
    {
        var gen =
            from count in Gen.Frequency((8, Gen.Choose(0, 8)), (2, Gen.Choose(30, 32)))
            from members in ValidMember().ListOf(count)
            select new TraceStateMembers(
                [.. members.GroupBy(m => m.Key).Select(g => new KeyValuePair<string, string>(g.Key, g.First().Value))]);

        return gen.ToArbitrary();
    }

    public static Arbitrary<TraceStateMember> TraceStateMemberArbitrary()
    {
        var gen = Gen.Frequency(
            (6, ValidMember()),
            (2, from key in Gen.Elements(BadKeys) from value in ValidValue() select new TraceStateMember(key, value)),
            (2, from key in ValidKey() from value in Gen.Elements(BadValues) select new TraceStateMember(key, value)),
            (1, from value in ValidValue() select new TraceStateMember(new string('a', 256), value)),
            (1, from value in ValidValue() select new TraceStateMember(new string('a', 257), value)),
            (1, from key in ValidKey() select new TraceStateMember(key, new string('v', 256))),
            (1, from key in ValidKey() select new TraceStateMember(key, new string('v', 257))));

        return gen.ToArbitrary();
    }

    private static Gen<TraceStateMember> ValidMember() =>
        from key in ValidKey()
        from value in ValidValue()
        select new TraceStateMember(key, value);

    private static Gen<string> ValidKey() =>
        from first in Gen.Elements(KeyStartCharacters)
        from length in Gen.Choose(0, 6)
        from rest in Gen.Elements(KeyCharacters).ListOf(length)
        select first + new string([.. rest]);

    private static Gen<string> ValidValue() =>
        from length in Gen.Choose(0, 6)
        from body in Gen.Elements(ValueCharacters).ListOf(length)
        from last in Gen.Elements(NonBlankValueCharacters)
        select new string([.. body]) + last;
}
