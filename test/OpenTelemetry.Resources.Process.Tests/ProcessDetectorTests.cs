// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Resources.Process.Tests;

public class ProcessDetectorTests
{
    [Fact]
    public void TestProcessAttributes()
    {
        var resource = ResourceBuilder.CreateEmpty().AddProcessDetector().Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
#if NET
        Assert.Equal(9, resourceAttributes.Count);
#else
        Assert.Equal(8, resourceAttributes.Count);
#endif

        Assert.IsType<long>(resourceAttributes[ProcessSemanticConventions.AttributeProcessArgsCount]);
        Assert.IsType<string>(resourceAttributes[ProcessSemanticConventions.AttributeProcessExecName]);
#if NET
        Assert.IsType<string>(resourceAttributes[ProcessSemanticConventions.AttributeProcessExecPath]);
#endif
        Assert.IsType<bool>(resourceAttributes[ProcessSemanticConventions.AttributeProcessInteractive]);
        Assert.IsType<string>(resourceAttributes[ProcessSemanticConventions.AttributeProcessOwner]);
        Assert.IsType<long>(resourceAttributes[ProcessSemanticConventions.AttributeProcessPid]);

        Assert.IsType<string>(resourceAttributes[ProcessSemanticConventions.AttributeProcessStartTime]);
        Assert.IsType<string>(resourceAttributes[ProcessSemanticConventions.AttributeProcessTitle]);
        Assert.IsType<string>(resourceAttributes[ProcessSemanticConventions.AttributeProcessWorkingDir]);
    }

    [Fact]
    public void TestProcessAttributesCommand()
    {
        var resource = ResourceBuilder.CreateEmpty().AddProcessDetector(new ProcessDetectorOptions()
        {
            IncludeCommand = true,
        }).Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
#if NET
        Assert.Equal(11, resourceAttributes.Count);
#else
        Assert.Equal(10, resourceAttributes.Count);
#endif

        Assert.IsType<long>(resourceAttributes[ProcessSemanticConventions.AttributeProcessArgsCount]);
        Assert.IsType<string[]>(resourceAttributes[ProcessSemanticConventions.AttributeProcessCommandArgs]);
        Assert.IsType<string>(resourceAttributes[ProcessSemanticConventions.AttributeProcessCommandLine]);
        Assert.IsType<string>(resourceAttributes[ProcessSemanticConventions.AttributeProcessExecName]);
#if NET
        Assert.IsType<string>(resourceAttributes[ProcessSemanticConventions.AttributeProcessExecPath]);
#endif
        Assert.IsType<bool>(resourceAttributes[ProcessSemanticConventions.AttributeProcessInteractive]);
        Assert.IsType<string>(resourceAttributes[ProcessSemanticConventions.AttributeProcessOwner]);
        Assert.IsType<long>(resourceAttributes[ProcessSemanticConventions.AttributeProcessPid]);

        Assert.IsType<string>(resourceAttributes[ProcessSemanticConventions.AttributeProcessStartTime]);
        Assert.IsType<string>(resourceAttributes[ProcessSemanticConventions.AttributeProcessTitle]);
        Assert.IsType<string>(resourceAttributes[ProcessSemanticConventions.AttributeProcessWorkingDir]);
    }
}
