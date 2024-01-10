// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.ResourceDetectors.Host.Tests;

public class HostDetectorTests
{
    [Fact]
    public void TestHostAttributes()
    {
        var resource = ResourceBuilder.CreateEmpty().AddDetector(new HostDetector()).Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);

        Assert.Single(resourceAttributes);

        Assert.NotEmpty(resourceAttributes[HostSemanticConventions.AttributeHostName]);
    }
}
