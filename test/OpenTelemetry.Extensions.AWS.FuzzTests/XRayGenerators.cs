// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;

namespace OpenTelemetry.Extensions.AWS.FuzzTests;

public static class XRayGenerators
{
    private static readonly char[] HexDigits = "0123456789abcdef".ToCharArray();

    private static readonly char[] HeaderCharacters = [.. "Root=Parent;Sampled01-fFgG \t", .. HexDigits];

    // Other fields of the X-Ray header which the propagator is not interested in.
    private static readonly string[] ExtraFields =
    [
        "Self=1-67891233-abcdef012345678912345678",
        "Lineage=a87bd80c:1|68fd508a:5|c512fbe3:2",
        "Foo=bar",
        "Baz=",
    ];

    // Names which start with the name of a field the propagator reads, but are not that field.
    private static readonly string[] CollidingKeys = ["RootX", "Root2", "Parent2", "Parentage", "SampledBy", "Sampled_"];

    private static readonly string[] CollidingValues = ["x", "1-zz", "ffffffffffffffff", string.Empty, "0"];

    public static Arbitrary<XRayHeaderCase> XRayHeaderCaseArbitrary()
    {
        var gen = Gen.Frequency(
            (4, ValidHeader()),
            (4, MutatedHeader()),
            (2, CollidingHeader()),
            (1, ArbitraryHeader()));

        return gen.ToArbitrary();
    }

    private static Gen<XRayHeaderCase> ValidHeader() =>
        from trace in Hex(32).Where(NonZero)
        from span in Hex(16).Where(NonZero)
        from sampled in Gen.Elements(true, false)
        from extraCount in Gen.Choose(0, 3)
        from extras in Gen.Elements(ExtraFields).ListOf(extraCount)
        from separator in Gen.Elements(";", "; ")
        from orderKeys in Gen.Choose(0, 1000).ListOf(3 + extraCount)
        select new XRayHeaderCase(
            Join([Root(trace), Parent(span), Sampled(sampled), .. extras], orderKeys, separator),
            trace,
            span,
            sampled);

    private static Gen<XRayHeaderCase> MutatedHeader() =>
        from trace in Hex(32)
        from span in Hex(16)
        from sampled in Gen.Elements("0", "1", "?", "2", string.Empty, "01", " 1", "1 ")
        from rootMutation in Gen.Choose(0, 11)
        from parentMutation in Gen.Choose(0, 11)
        from rootPosition in Gen.Choose(0, 1000)
        from parentPosition in Gen.Choose(0, 1000)
        from orderKeys in Gen.Choose(0, 1000).ListOf(3)
        from separator in Gen.Elements(";", "; ", " ; ", ";;")
        let root = $"Root={Mutate($"1-{trace.Substring(0, 8)}-{trace.Substring(8)}", rootMutation, rootPosition)}"
        let parent = $"Parent={Mutate(span, parentMutation, parentPosition)}"
        select new XRayHeaderCase(Join([root, parent, $"Sampled={sampled}"], orderKeys, separator), null, null, false);

    private static Gen<XRayHeaderCase> CollidingHeader() =>
        from trace in Hex(32).Where(NonZero)
        from span in Hex(16).Where(NonZero)
        from sampled in Gen.Elements(true, false)
        from key in Gen.Elements(CollidingKeys)
        from value in Gen.Elements(CollidingValues)
        from first in Gen.Elements(true, false)
        let parts = first
            ? new[] { $"{key}={value}", Root(trace), Parent(span), Sampled(sampled) }
            : [Root(trace), Parent(span), Sampled(sampled), $"{key}={value}"]
        select new XRayHeaderCase(string.Join(";", parts), trace, span, sampled);

    private static Gen<XRayHeaderCase> ArbitraryHeader() =>
        from length in Gen.Choose(0, 80)
        from chars in Gen.Elements(HeaderCharacters).ListOf(length)
        select new XRayHeaderCase(new string([.. chars]), null, null, false);

    private static Gen<string> Hex(int length) =>
        Gen.Elements(HexDigits).ListOf(length).Select(chars => new string([.. chars]));

    private static bool NonZero(string hex) => hex.Any(c => c != '0');

    private static string Root(string trace) => $"Root=1-{trace.Substring(0, 8)}-{trace.Substring(8)}";

    private static string Parent(string span) => $"Parent={span}";

    private static string Sampled(bool sampled) => $"Sampled={(sampled ? '1' : '0')}";

    private static string Join(string[] parts, List<int> orderKeys, string separator) =>
        string.Join(
            separator,
            parts.Select((part, index) => (Part: part, Key: orderKeys[index])).OrderBy(p => p.Key).Select(p => p.Part));

    private static string Mutate(string value, int mutation, int position)
    {
        if (value.Length == 0)
        {
            return mutation is 5 or 9 or 10 ? value + "0" : value;
        }

        var chars = value.ToCharArray();
        var index = position % chars.Length;

        switch (mutation)
        {
            case 1:
                return value.ToUpperInvariant();
            case 2:
                chars[0] = ' ';
                return new string(chars);
            case 3:
                chars[chars.Length - 1] = ' ';
                return new string(chars);
            case 4:
                return new string(chars, 0, chars.Length - 1);
            case 5:
                return value + "0";
            case 6:
                chars[index] = 'g';
                return new string(chars);
            case 7:
                return string.Empty;
            case 8:
                return new string(' ', chars.Length);
            case 9:
                return " " + value;
            case 10:
                return value + " ";
            case 11:
                chars[index] = char.ToUpperInvariant(chars[index]);
                return new string(chars);
            default:
                return value;
        }
    }
}
