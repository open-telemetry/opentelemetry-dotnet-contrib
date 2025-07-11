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

    [Theory]
#if NET
    [InlineData(true, 11)]
    [InlineData(false, 9)]
#else
    [InlineData(true, 10)]
    [InlineData(false, 8)]
#endif
    public void TestProcessAttributesOptions(bool include, int count)
    {
        var resource = ResourceBuilder.CreateEmpty().AddProcessDetector(new ProcessDetectorOptions()
        {
            IncludeCommand = include,
        }).Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        Assert.Equal(count, resourceAttributes.Count);

        Assert.IsType<long>(resourceAttributes[ProcessSemanticConventions.AttributeProcessArgsCount]);
        if (include)
        {
            Assert.IsType<string[]>(resourceAttributes[ProcessSemanticConventions.AttributeProcessCommandArgs]);
            Assert.IsType<string>(resourceAttributes[ProcessSemanticConventions.AttributeProcessCommandLine]);
        }

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

    [Theory]
#if NET
    [InlineData(true, 11)]
    [InlineData(false, 9)]
#else
    [InlineData(true, 10)]
    [InlineData(false, 8)]
#endif
    public void TestProcessAttributesAction(bool include, int count)
    {
        var resource = ResourceBuilder.CreateEmpty().AddProcessDetector(x =>
            {
                x.IncludeCommand = include;
            }).Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        Assert.Equal(count, resourceAttributes.Count);

        Assert.IsType<long>(resourceAttributes[ProcessSemanticConventions.AttributeProcessArgsCount]);
        if (include)
        {
            Assert.IsType<string[]>(resourceAttributes[ProcessSemanticConventions.AttributeProcessCommandArgs]);
            Assert.IsType<string>(resourceAttributes[ProcessSemanticConventions.AttributeProcessCommandLine]);
        }

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
