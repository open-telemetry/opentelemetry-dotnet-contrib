// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Resources.ProcessRuntime.Tests;

public class ProcessRuntimeDetectorTests
{
    [Fact]
    public void TestProcessRuntimeAttributes()
    {
        var resource = ResourceBuilder.CreateEmpty().AddProcessRuntimeDetector().Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal(3, resourceAttributes.Count);

#if NETFRAMEWORK
        Assert.IsType<string>(resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeDescription]);
        Assert.Matches(@"^\.NET Framework \d+\.\d+\.\d+\.\d+$", (string)resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeDescription]);
        Assert.IsType<string>(resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeName]);
        Assert.Equal(".NET Framework", resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeName]);
        Assert.IsType<string>(resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeVersion]);
        Assert.Matches(@"^4.(0.0|[5-9](.[1-2]))?$", (string)resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeVersion]);
#else
        Assert.IsType<string>(resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeDescription]);
        Assert.Matches(@"^\.NET \d+\.\d+\.\d+(\-(preview|rc)\.\d+\.\d+\.\d+)?$", (string)resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeDescription]);
        Assert.IsType<string>(resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeName]);
        Assert.Equal(".NET", resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeName]);
        Assert.IsType<string>(resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeVersion]);
        Assert.Matches(@"^(([1-3,5-9])|(\d{2,}))\.\d+\.\d+$", (string)resourceAttributes[ProcessRuntimeSemanticConventions.AttributeProcessRuntimeVersion]);
#endif
    }
}
