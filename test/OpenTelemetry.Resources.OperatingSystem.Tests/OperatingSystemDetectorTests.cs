// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Resources.OperatingSystem.Test;

public class OperatingSystemDetectorTests
{
    [Fact]
    public void TestOperatingSystemAttributes()
    {
        var resource = ResourceBuilder.CreateEmpty().AddDetector(new OperatingSystemDetector()).Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);

        var operatingSystems = new[] { "windows", "linux", "darwin" };

        Assert.Single(resourceAttributes);

        Assert.True(resourceAttributes.ContainsKey(OperatingSystemSemanticConventions.AttributeOperatingSystemType));

        Assert.Contains(resourceAttributes[OperatingSystemSemanticConventions.AttributeOperatingSystemType], operatingSystems);
    }
}
