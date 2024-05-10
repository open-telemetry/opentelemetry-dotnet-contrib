// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.ResourceDetectors.Process.Tests;

public class ProcessDetectorTests
{
    [Fact]
    public void TestProcessAttributes()
    {
        var resource = ResourceBuilder.CreateEmpty().AddDetector(new ProcessDetector()).Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal(2, resourceAttributes.Count);

        Assert.IsType<string>(resourceAttributes[ProcessSemanticConventions.AttributeProcessOwner]);
        Assert.IsType<long>(resourceAttributes[ProcessSemanticConventions.AttributeProcessPid]);
    }
}
