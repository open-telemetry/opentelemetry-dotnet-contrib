// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using FsCheck.Xunit;
using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Sources;

namespace OpenTelemetry.DynamicControl.FuzzTests;

public static class PolicyReaderTests
{
    private const int MaxTest = 1_000;

    private static readonly PolicyReader[] Readers =
        [TraceSamplingRatePolicyReader.Instance, LogLevelPolicyReader.Instance];

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(Generators)])]
    public static void Read_WithArbitraryValue_AlwaysReturnsAWellFormedResult(FuzzedJsonValue value)
    {
        var json = value.Json;

        using var document = JsonDocument.Parse(json);

        foreach (var reader in Readers)
        {
            AssertWellFormed(reader, document.RootElement);
        }
    }

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(Generators)])]
    public static void TraceSamplingRatePolicyReader_ReadsAnyProbabilityInRange(FuzzedProbability raw, bool quoted, bool wrapped)
    {
        var probability = raw.Value;
        var literal = probability.ToString("G17", CultureInfo.InvariantCulture);
        var json = Compose(quoted ? Quote(literal) : literal, wrapped ? "probability" : null);

        using var document = JsonDocument.Parse(json);

        var policy = AssertWellFormed(TraceSamplingRatePolicyReader.Instance, document.RootElement);

        Assert.NotNull(policy);
        Assert.Equal(probability, Assert.IsType<TraceSamplingRatePolicy>(policy).SamplingProbability);
    }

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(Generators)])]
    public static void LogLevelPolicyReader_ReadsAnyAcceptedToken(AcceptedLogLevelToken raw, bool wrapped)
    {
        var json = Compose(Quote(raw.Token), wrapped ? "level" : null);

        using var document = JsonDocument.Parse(json);

        var policy = AssertWellFormed(LogLevelPolicyReader.Instance, document.RootElement);

        Assert.NotNull(policy);
        Assert.NotEqual(DiagnosticLogLevel.Unspecified, Assert.IsType<LogLevelPolicy>(policy).MinimumLevel);
    }

    private static TelemetryPolicy? AssertWellFormed(PolicyReader reader, in JsonElement value)
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
}
