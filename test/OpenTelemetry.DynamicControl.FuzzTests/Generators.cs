// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;
using ArbMap = FsCheck.FSharp.ArbMap;

namespace OpenTelemetry.DynamicControl.FuzzTests;

public static class Generators
{
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
}
