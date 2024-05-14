// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Resources.ProcessRuntime.Tests;

public class ProcessRuntimeDetectorTests
{
    [Fact]
    public void TestProcessRuntimeAttributes()
    {
        var resource = ResourceBuilder.CreateEmpty().AddDetector(new ProcessRuntimeDetector()).Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);

        Assert.Equal(3, resourceAttributes.Count);

#if NETFRAMEWORK
        Assert.Matches(@"^\.NET Framework \d+\.\d+\.\d+\.\d+$", resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeDescription]);
        Assert.Equal(".NET Framework", resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeName]);
        Assert.Matches(@"^4.[1-9](.[1-2])?$", resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeVersion]);
#else
        Assert.Matches(@"^\.NET \d+\.\d+\.\d+$", resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeDescription]);
        Assert.Equal(".NET", resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeName]);
        Assert.Matches(@"^\d+\.\d+\.\d+$", resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeVersion]);
#endif
    }
}
