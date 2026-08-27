// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Policies;

namespace OpenTelemetry.DynamicControl.FuzzTests;

internal static class FuzzInput
{
    // Mapping generated strings keeps shrinking while making numeric parser paths reachable.
    private const string NumericCharacterPool = "0123456789.+-eE \t\u00A0abcxyz\u221E,'\"\\\uD800\uDC00";

    public static string MapIntoNumericPool(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var chars = new char[text!.Length];

        for (var i = 0; i < text.Length; i++)
        {
            chars[i] = NumericCharacterPool[text[i] % NumericCharacterPool.Length];
        }

        return new string(chars);
    }

    public static string AcceptedLogLevelToken(byte selector) =>
        DiagnosticLogLevelParser.AcceptedTokenValues[selector % DiagnosticLogLevelParser.AcceptedTokenValues.Length];

    public static string MutateCase(string text, int caseMask)
    {
        var chars = text.ToCharArray();

        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = ((caseMask >> i) & 1) == 0
                ? char.ToLowerInvariant(chars[i])
                : char.ToUpperInvariant(chars[i]);
        }

        return new string(chars);
    }

    public static double ToProbability(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 1;
        }

        value = Math.Abs(value);
        var fraction = value - Math.Floor(value);

        return fraction == 0 ? Math.Min(1, value) : fraction;
    }

    public static double ToFinite(double value) =>
        double.IsNaN(value) || double.IsInfinity(value) ? 0 : value;

    public static string Describe(string? text) => text is null ? "(null)" : $"'{text}'";
}
