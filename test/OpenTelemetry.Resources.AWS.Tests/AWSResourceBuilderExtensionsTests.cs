// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.AWS.Tests;

public class AWSResourceBuilderExtensionsTests
{
    [Fact]
    public void AddAWSEBSDetector_NullBuilder_Throws() =>
        Assert.Throws<ArgumentNullException>("builder", () => AWSResourceBuilderExtensions.AddAWSEBSDetector(null!));

    [Fact]
    public void AddAWSEBSDetector_ReturnsBuilderAndInvokesConfigure()
    {
        var builder = ResourceBuilder.CreateEmpty();
        var configured = false;

        var result = builder.AddAWSEBSDetector(options =>
        {
            Assert.NotNull(options);
            configured = true;
        });

        Assert.Same(builder, result);
        Assert.True(configured);
    }

    [Fact]
    public void AddAWSEBSDetector_WithoutConfigure_ReturnsBuilder()
    {
        var builder = ResourceBuilder.CreateEmpty();

        Assert.Same(builder, builder.AddAWSEBSDetector());
    }

    [Fact]
    public void AddAWSEC2Detector_NullBuilder_Throws() =>
        Assert.Throws<ArgumentNullException>("builder", () => AWSResourceBuilderExtensions.AddAWSEC2Detector(null!));

    [Fact]
    public void AddAWSEC2Detector_ReturnsBuilderAndInvokesConfigure()
    {
        var builder = ResourceBuilder.CreateEmpty();
        var configured = false;

        var result = builder.AddAWSEC2Detector(options =>
        {
            Assert.NotNull(options);
            configured = true;
        });

        Assert.Same(builder, result);
        Assert.True(configured);
    }

    [Fact]
    public void AddAWSEC2Detector_WithoutConfigure_ReturnsBuilder()
    {
        var builder = ResourceBuilder.CreateEmpty();

        Assert.Same(builder, builder.AddAWSEC2Detector());
    }

#if NET
    [Fact]
    public void AddAWSECSDetector_NullBuilder_Throws() =>
        Assert.Throws<ArgumentNullException>("builder", () => AWSResourceBuilderExtensions.AddAWSECSDetector(null!));

    [Fact]
    public void AddAWSECSDetector_ReturnsBuilderAndInvokesConfigure()
    {
        var builder = ResourceBuilder.CreateEmpty();
        var configured = false;

        var result = builder.AddAWSECSDetector(options =>
        {
            Assert.NotNull(options);
            configured = true;
        });

        Assert.Same(builder, result);
        Assert.True(configured);
    }

    [Fact]
    public void AddAWSECSDetector_WithoutConfigure_ReturnsBuilder()
    {
        var builder = ResourceBuilder.CreateEmpty();

        Assert.Same(builder, builder.AddAWSECSDetector());
    }

    [Fact]
    public void AddAWSEKSDetector_NullBuilder_Throws() =>
        Assert.Throws<ArgumentNullException>("builder", () => AWSResourceBuilderExtensions.AddAWSEKSDetector(null!));

    [Fact]
    public void AddAWSEKSDetector_ReturnsBuilderAndInvokesConfigure()
    {
        var builder = ResourceBuilder.CreateEmpty();
        var configured = false;

        var result = builder.AddAWSEKSDetector(options =>
        {
            Assert.NotNull(options);
            configured = true;
        });

        Assert.Same(builder, result);
        Assert.True(configured);
    }

    [Fact]
    public void AddAWSEKSDetector_WithoutConfigure_ReturnsBuilder()
    {
        var builder = ResourceBuilder.CreateEmpty();

        Assert.Same(builder, builder.AddAWSEKSDetector());
    }
#endif
}
