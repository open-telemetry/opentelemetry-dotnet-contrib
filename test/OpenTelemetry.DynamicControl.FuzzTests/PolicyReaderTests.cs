// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.Json;
using FsCheck.Xunit;
using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Sources;

namespace OpenTelemetry.DynamicControl.FuzzTests;

public static class PolicyReaderTests
{
    private const int MaxTest = 1_000;

    private const int MaxMembers = 4;

    private static readonly PolicyReader[] Readers =
        [TraceSamplingRatePolicyReader.Instance, LogLevelPolicyReader.Instance];

    // Includes known, unknown, and case-mismatched names; repeats exercise duplicate members.
    private static readonly string[] MemberNames = ["probability", "level", "future", "Probability"];

    [Property(MaxTest = MaxTest)]
    public static void Read_WithArbitraryValue_AlwaysReturnsAWellFormedResult(
        byte shapeSelector,
        byte[]? memberSelectors,
        byte textSelector,
        string? text,
        double number)
    {
        var json = BuildValue(shapeSelector, memberSelectors, textSelector, text, number);

        using var document = JsonDocument.Parse(json);

        foreach (var reader in Readers)
        {
            AssertWellFormed(reader, document.RootElement, json);
        }
    }

    [Property(MaxTest = MaxTest)]
    public static void TraceSamplingRatePolicyReader_ReadsAnyProbabilityInRange(double raw, bool quoted, bool wrapped)
    {
        var probability = FuzzInput.ToProbability(raw);
        var literal = probability.ToString("G17", CultureInfo.InvariantCulture);
        var json = Compose(quoted ? Quote(literal) : literal, wrapped ? "probability" : null);

        using var document = JsonDocument.Parse(json);

        var policy = AssertWellFormed(TraceSamplingRatePolicyReader.Instance, document.RootElement, json);

        Assert.NotNull(policy);
        Assert.Equal(probability, Assert.IsType<TraceSamplingRatePolicy>(policy).SamplingProbability);
    }

    [Property(MaxTest = MaxTest)]
    public static void LogLevelPolicyReader_ReadsAnyAcceptedToken(byte selector, int caseMask, bool wrapped)
    {
        var token = FuzzInput.MutateCase(FuzzInput.AcceptedLogLevelToken(selector), caseMask);
        var json = Compose(Quote(token), wrapped ? "level" : null);

        using var document = JsonDocument.Parse(json);

        var policy = AssertWellFormed(LogLevelPolicyReader.Instance, document.RootElement, json);

        Assert.NotNull(policy);
        Assert.NotEqual(DiagnosticLogLevel.Unspecified, Assert.IsType<LogLevelPolicy>(policy).MinimumLevel);
    }

    private static TelemetryPolicy? AssertWellFormed(PolicyReader reader, in JsonElement value, string json)
    {
        var result = reader.Read(value);

        if (!result.TryGetPolicy(out var policy))
        {
            Assert.NotEqual(PolicyRejectionReason.None, result.Reason);
            Assert.NotNull(result.Error);
            Assert.NotEmpty(result.Error);

            return null;
        }

        Assert.Equal(PolicyRejectionReason.None, result.Reason);
        Assert.Null(result.Error);
        Assert.Equal(reader.PolicyType, policy.PolicyType);
        Assert.Equal(reader.PolicyId, policy.Id);
        Assert.Equal(reader.PolicyName, policy.Name);

        return policy;
    }

    private static string Quote(string value) => $"\"{value}\"";

    private static string Compose(string value, string? memberName) =>
        memberName is null ? value : $"{{\"{memberName}\":{value}}}";

    private static string BuildValue(
        byte shapeSelector,
        byte[]? memberSelectors,
        byte textSelector,
        string? text,
        double number)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteValue(writer, shapeSelector, memberSelectors ?? [], textSelector, text, number);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteValue(
        Utf8JsonWriter writer,
        byte shapeSelector,
        byte[] memberSelectors,
        byte textSelector,
        string? text,
        double number)
    {
        // Readers only inspect one object level; object values are weighted for member lookup coverage.
        switch (shapeSelector % 8)
        {
            case 0:
                writer.WriteNumberValue(FuzzInput.ToFinite(number));
                break;

            case 1:
                writer.WriteStringValue(ScalarText(textSelector, text, number));
                break;

            case 2:
                writer.WriteBooleanValue((textSelector & 1) == 0);
                break;

            case 3:
                writer.WriteNullValue();
                break;

            case 4:
                writer.WriteStartArray();
                writer.WriteStringValue(ScalarText(textSelector, text, number));
                writer.WriteEndArray();
                break;

            default:
                writer.WriteStartObject();

                for (var i = 0; i < Math.Min(memberSelectors.Length, MaxMembers); i++)
                {
                    var selector = memberSelectors[i];

                    writer.WritePropertyName(MemberNames[selector % MemberNames.Length]);
                    WriteMemberValue(writer, selector, textSelector, text, number);
                }

                writer.WriteEndObject();
                break;
        }
    }

    private static void WriteMemberValue(
        Utf8JsonWriter writer,
        byte selector,
        byte textSelector,
        string? text,
        double number)
    {
        switch ((selector / MemberNames.Length) % 6)
        {
            case 0:
                writer.WriteNumberValue(FuzzInput.ToFinite(number));
                break;

            case 1:
                writer.WriteStringValue(ScalarText(textSelector, text, number));
                break;

            case 2:
                writer.WriteBooleanValue((textSelector & 1) == 0);
                break;

            case 3:
                writer.WriteNullValue();
                break;

            case 4:
                writer.WriteStartObject();
                writer.WriteEndObject();
                break;

            default:
                writer.WriteStartArray();
                writer.WriteEndArray();
                break;
        }
    }

    private static string ScalarText(byte textSelector, string? text, double number) =>
        (textSelector % 4) switch
        {
            0 => FuzzInput.AcceptedLogLevelToken((byte)(textSelector / 4)),
            1 => number.ToString("R", CultureInfo.InvariantCulture),
            2 => FuzzInput.MapIntoNumericPool(text),
            _ => text ?? string.Empty,
        };
}
