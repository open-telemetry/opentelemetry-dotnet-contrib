// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace OpenTelemetry.Resources.Process.Tests;

public class ProcessDetectorTests
{
    [Fact]
    public void TestProcessAttributes()
    {
        var resource = ResourceBuilder.CreateEmpty().AddProcessDetector().Build();

        Assert.NotNull(resource);
        Assert.StartsWith("https://opentelemetry.io/schemas/", resource.SchemaUrl);

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.IsType<string>(resourceAttributes[ProcessSemanticConventions.AttributeProcessOwner]);
        Assert.IsType<long>(resourceAttributes[ProcessSemanticConventions.AttributeProcessPid]);

        var creationTime = Assert.IsType<string>(resourceAttributes[ProcessSemanticConventions.AttributeProcessCreationTime]);

        // The creation time must be an ISO 8601 timestamp normalized to UTC.
        Assert.EndsWith("Z", creationTime, StringComparison.Ordinal);
        Assert.True(DateTime.TryParse(creationTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed), $"Failed to parse creation time '{creationTime}'.");
        Assert.Equal(DateTimeKind.Utc, parsed.Kind);
    }
}
