// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Sources;

namespace OpenTelemetry.DynamicControl.Tests;

// Deterministic randomized-input tests. Every generator is driven by a fixed seed, so a
// failure reproduces exactly; the assertions state contracts that must hold for any
// input rather than expectations about specific values.
public class PolicyValueRandomizedTests
{
    private const int ParserIterations = 10_000;
    private const int ReaderIterations = 2_000;

    private const string WildCharacterPool = "0123456789.+-eE \t\u00A0abcxyz\u221E,'\"\\\uD800\uDC00";

    [Fact]
    public void TraceSamplingRateParser_WithRandomText_NeverThrowsAndUpholdsItsContract()
    {
        var random = new Random(20260825);
        var accepted = 0;

        for (var i = 0; i < ParserIterations; i++)
        {
            var text = NextWildText(random);

            var parsed = TraceSamplingRateParser.TryParse(text, out var probability);

            if (parsed)
            {
                accepted++;
                Assert.False(double.IsNaN(probability), text);
                Assert.False(double.IsInfinity(probability), text);
                Assert.False(double.IsNegative(probability), text);

                if (probability <= 1)
                {
                    Assert.True(
                        TraceSamplingRatePolicy.TryCreate(new PolicyId("id"), "name", probability, out _, out var error),
                        $"'{text}' parsed to {probability}, which the model rejects: {error}");
                }
            }
            else
            {
                Assert.Equal(default, probability);
            }
        }

        Assert.True(accepted > 0, "The generator produced no accepted values; the test lost its power.");
    }

    [Fact]
    public void DiagnosticLogLevelParser_WithRandomText_NeverThrowsAndUpholdsItsContract()
    {
        var random = new Random(20260826);
        var accepted = 0;

        for (var i = 0; i < ParserIterations; i++)
        {
            var fromAcceptedToken = random.Next(3) == 0;
            var text = fromAcceptedToken
                ? MutateCase(random, NextAcceptedToken(random))
                : NextRandomChars(random, WildCharacterPool, 0, 16);

            var parsed = DiagnosticLogLevelParser.TryParse(text, out var level);

            if (fromAcceptedToken)
            {
                Assert.True(parsed, $"'{text}' is a case-mutated accepted token, which the parser rejects.");
            }

            if (parsed)
            {
                accepted++;
                Assert.NotEqual(DiagnosticLogLevel.Unspecified, level);
                Assert.True(
                    LogLevelPolicy.TryCreate(new PolicyId("id"), "name", level, out _, out var error),
                    $"'{text}' parsed to a level the model rejects: {error}");
            }
            else
            {
                Assert.Equal(DiagnosticLogLevel.Unspecified, level);
            }
        }

        Assert.True(accepted > 0, "The generator produced no accepted values; the test lost its power.");
    }

    [Fact]
    public void Readers_WithRandomJsonValues_NeverThrowAndAlwaysReturnAWellFormedResult()
    {
        var random = new Random(20260827);
        PolicyReader[] readers = [TraceSamplingRatePolicyReader.Instance, LogLevelPolicyReader.Instance];
        var policies = 0;
        var rejections = 0;

        for (var i = 0; i < ReaderIterations; i++)
        {
            using var document = CreateRandomDocument(random);

            foreach (var reader in readers)
            {
                var result = reader.Read(document.RootElement);

                if (result.TryGetPolicy(out var policy))
                {
                    policies++;
                    Assert.Equal(PolicyRejectionReason.None, result.Reason);
                    Assert.Null(result.Error);
                    Assert.Equal(reader.PolicyType, policy.PolicyType);
                    Assert.Equal(reader.PolicyId, policy.Id);
                }
                else
                {
                    rejections++;
                    Assert.NotEqual(PolicyRejectionReason.None, result.Reason);
                    Assert.NotNull(result.Error);
                    Assert.NotEmpty(result.Error);
                }
            }
        }

        Assert.True(policies > 0, "The generator produced no accepted values; the test lost its power.");
        Assert.True(rejections > 0, "The generator produced no rejected values; the test lost its power.");
    }

    private static string NextWildText(Random random) =>
        random.Next(4) switch
        {
            // Bias toward numeric-looking text so the accepted path is exercised.
            0 => random.NextDouble().ToString("R", CultureInfo.InvariantCulture),
            1 => (random.NextDouble() * Math.Pow(10, random.Next(-320, 320)) * (random.Next(2) == 0 ? 1 : -1))
                .ToString("R", CultureInfo.InvariantCulture),
            _ => NextRandomChars(random, WildCharacterPool, 0, 24),
        };

    private static string NextAcceptedToken(Random random) =>
        DiagnosticLogLevelParser.AcceptedTokenValues[random.Next(DiagnosticLogLevelParser.AcceptedTokenValues.Length)];

    private static string MutateCase(Random random, string token)
    {
        var chars = token.ToCharArray();

        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = random.Next(2) == 0 ? char.ToUpperInvariant(chars[i]) : char.ToLowerInvariant(chars[i]);
        }

        var mutated = new string(chars);
        return random.Next(2) == 0 ? " " + mutated + "\t" : mutated;
    }

    private static string NextRandomChars(Random random, string pool, int minLength, int maxLength)
    {
        var length = random.Next(minLength, maxLength + 1);
        var chars = new char[length];

        for (var i = 0; i < length; i++)
        {
            chars[i] = pool[random.Next(pool.Length)];
        }

        return new string(chars);
    }

    private static JsonDocument CreateRandomDocument(Random random)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteRandomValue(writer, random, depth: 0);
        }

        return JsonDocument.Parse(stream.ToArray());
    }

    private static void WriteRandomValue(Utf8JsonWriter writer, Random random, int depth)
    {
        switch (random.Next(depth >= 3 ? 6 : 8))
        {
            case 0:
                writer.WriteNumberValue(random.NextDouble());
                break;
            case 1:
                writer.WriteNumberValue((random.NextDouble() - 0.5) * Math.Pow(10, random.Next(-300, 300)));
                break;
            case 2:
                writer.WriteStringValue(NextJsonText(random));
                break;
            case 3:
                writer.WriteBooleanValue(random.Next(2) == 0);
                break;
            case 4:
                writer.WriteNullValue();
                break;
            case 5:
                writer.WriteNumberValue(random.Next(-10, 10));
                break;
            case 6:
                writer.WriteStartObject();

                // Random member names may repeat, exercising the repeated-member path.
                var members = random.Next(4);
                for (var i = 0; i < members; i++)
                {
                    writer.WritePropertyName(NextMemberName(random));
                    WriteRandomValue(writer, random, depth + 1);
                }

                writer.WriteEndObject();
                break;
            default:
                writer.WriteStartArray();

                var elements = random.Next(3);
                for (var i = 0; i < elements; i++)
                {
                    WriteRandomValue(writer, random, depth + 1);
                }

                writer.WriteEndArray();
                break;
        }
    }

    private static string NextMemberName(Random random) =>
        random.Next(4) switch
        {
            0 => "probability",
            1 => "level",
            2 => "future",
            _ => NextRandomChars(random, "abcdefgh", 1, 8),
        };

    private static string NextJsonText(Random random) =>
        random.Next(3) switch
        {
            0 => NextAcceptedToken(random),
            1 => random.NextDouble().ToString("R", CultureInfo.InvariantCulture),
            _ => NextRandomChars(random, "0123456789.+-eE abcxyz", 0, 12),
        };
}
