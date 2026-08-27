// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using FsCheck;
using FsCheck.Fluent;
using ArbMap = FsCheck.FSharp.ArbMap;

namespace OpenTelemetry.DynamicControl.FuzzTests;

public static class Generators
{
    private const int MaxObjectMembers = 4;

    // Includes known, unknown, and case-mismatched names; repeats exercise duplicate members.
    private static readonly string[] MemberNames = ["probability", "level", "future", "Probability"];

    public static Arbitrary<NumericLookingText> NumericLookingTextArbitrary() =>
        ArbMap.defaults.ArbFor<string?>().Generator
            .Select(seed => new NumericLookingText(FuzzInput.MapIntoNumericPool(seed)))
            .ToArbitrary();

    public static Arbitrary<AcceptedLogLevelToken> AcceptedLogLevelTokenArbitrary()
    {
        var gen =
            from selector in ArbMap.defaults.ArbFor<byte>().Generator
            from caseMask in ArbMap.defaults.ArbFor<int>().Generator
            select new AcceptedLogLevelToken(FuzzInput.MutateCase(FuzzInput.AcceptedLogLevelToken(selector), caseMask));

        return gen.ToArbitrary();
    }

    public static Arbitrary<FuzzedProbability> FuzzedProbabilityArbitrary() =>
        ArbMap.defaults.ArbFor<double>().Generator
            .Select(value => new FuzzedProbability(FuzzInput.ToProbability(value)))
            .ToArbitrary();

    public static Arbitrary<NonNegativeFiniteDouble> NonNegativeFiniteDoubleArbitrary() =>
        ArbMap.defaults.ArbFor<double>().Generator
            .Select(value => new NonNegativeFiniteDouble(Math.Abs(FuzzInput.ToFinite(value))))
            .ToArbitrary();

    public static Arbitrary<FuzzedJsonValue> FuzzedJsonValueArbitrary()
    {
        var numberGen = ArbMap.defaults.ArbFor<double>().Generator
            .Select(value => JsonSerializer.Serialize(FuzzInput.ToFinite(value)));

        var stringGen = ScalarTextGen().Select(text => JsonSerializer.Serialize(text));
        var boolGen = Gen.Elements("true", "false");
        var nullGen = Gen.Constant("null");
        var arrayGen = stringGen.Select(json => $"[{json}]");

        var memberGen =
            from name in Gen.Elements(MemberNames)
            from value in Gen.OneOf(numberGen, stringGen, boolGen, nullGen, Gen.Constant("{}"), Gen.Constant("[]"))
            select $"\"{name}\":{value}";

        var objectGen =
            from count in Gen.Choose(0, MaxObjectMembers)
            from members in memberGen.ListOf(count)
            select $"{{{string.Join(",", members)}}}";

        // Readers only inspect one object level; object values are weighted for member lookup coverage.
        return Gen.Frequency(
                (1, numberGen),
                (1, stringGen),
                (1, boolGen),
                (1, nullGen),
                (1, arrayGen),
                (3, objectGen))
            .Select(json => new FuzzedJsonValue(json))
            .ToArbitrary();
    }

    private static Gen<string> ScalarTextGen()
    {
        var acceptedTokenGen = ArbMap.defaults.ArbFor<byte>().Generator
            .Select(FuzzInput.AcceptedLogLevelToken);

        var numericTextGen = ArbMap.defaults.ArbFor<double>().Generator
            .Select(value => value.ToString("R", CultureInfo.InvariantCulture));

        var numericLookingGen = ArbMap.defaults.ArbFor<string?>().Generator
            .Select(FuzzInput.MapIntoNumericPool);

        var arbitraryTextGen = ArbMap.defaults.ArbFor<string?>().Generator
            .Select(text => text ?? string.Empty);

        return Gen.OneOf(acceptedTokenGen, numericTextGen, numericLookingGen, arbitraryTextGen);
    }
}
