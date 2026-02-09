// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.AWS;
using OpenTelemetry.Instrumentation.AWSLambda.Implementation;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWSLambda.Tests.Implementation;

public class AWSLambdaResourceDetectorTests : IDisposable
{
    private const string SymlinkPath = "/tmp/.otel-account-id";

    public AWSLambdaResourceDetectorTests()
    {
        this.Cleanup();
    }

    public void Dispose()
    {
        this.Cleanup();
    }

#if NET
    [Fact]
    public void Detect_WithAccountIdSymlink_SetsCloudAccountId()
    {
        const string expectedAccountId = "123456789012";

        // Create a symlink whose target is the raw account ID string.
        File.CreateSymbolicLink(SymlinkPath, expectedAccountId);

        var conventions = new AWSSemanticConventions(SemanticConventionVersion.Latest);
        var detector = new AWSLambdaResourceDetector(conventions);

        var resource = detector.Detect();
        var attributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.True(attributes.ContainsKey("cloud.account.id"));
        Assert.Equal(expectedAccountId, attributes["cloud.account.id"]);
    }

    [Fact]
    public void Detect_WithAccountIdSymlink_PreservesLeadingZeros()
    {
        const string expectedAccountId = "000123456789";

        File.CreateSymbolicLink(SymlinkPath, expectedAccountId);

        var conventions = new AWSSemanticConventions(SemanticConventionVersion.Latest);
        var detector = new AWSLambdaResourceDetector(conventions);

        var resource = detector.Detect();
        var attributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal(expectedAccountId, attributes["cloud.account.id"]);
    }
#endif

    [Fact]
    public void Detect_WithoutAccountIdSymlink_DoesNotThrow()
    {
        // Ensure the symlink does not exist.
        if (File.Exists(SymlinkPath))
        {
            File.Delete(SymlinkPath);
        }

        var conventions = new AWSSemanticConventions(SemanticConventionVersion.Latest);
        var detector = new AWSLambdaResourceDetector(conventions);

        var resource = detector.Detect();
        var attributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.False(attributes.ContainsKey("cloud.account.id"));
    }

    private void Cleanup()
    {
        try
        {
            if (File.Exists(SymlinkPath))
            {
                File.Delete(SymlinkPath);
            }
        }
        catch
        {
            // Ignore cleanup errors.
        }
    }
}
