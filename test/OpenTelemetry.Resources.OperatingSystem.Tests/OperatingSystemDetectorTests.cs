// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using Xunit;

namespace OpenTelemetry.Resources.OperatingSystem.Test;

public class OperatingSystemDetectorTests
{
    [Fact]
    public void TestOperatingSystemAttributes()
    {
        var resource = ResourceBuilder.CreateEmpty().AddOperatingSystemDetector().Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);

        var expectedPlatform =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OperatingSystemSemanticConventions.OperatingSystemsValues.Windows :
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? OperatingSystemSemanticConventions.OperatingSystemsValues.Linux :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? OperatingSystemSemanticConventions.OperatingSystemsValues.Darwin :
            throw new PlatformNotSupportedException("Unknown platform");

        Assert.Single(resourceAttributes);

        Assert.True(resourceAttributes.ContainsKey(OperatingSystemSemanticConventions.AttributeOperatingSystemType));

        Assert.Equal(resourceAttributes[OperatingSystemSemanticConventions.AttributeOperatingSystemType], expectedPlatform);
    }
}
