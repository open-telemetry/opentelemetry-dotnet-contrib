// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using OpenTelemetry.AWS;
using OpenTelemetry.Instrumentation.AWSLambda.Implementation;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWSLambda.Tests.Implementation;

public class AWSLambdaResourceDetectorTests : IDisposable
{
    private const string SymlinkPath = "/tmp/.otel-aws-account-id";

    public AWSLambdaResourceDetectorTests()
    {
        this.Cleanup();
    }

    public void Dispose()
    {
        this.Cleanup();
    }

#if NET
    [Theory]
    [InlineData("")]
    [InlineData("000")]
    public void Detect_WithAccountIdSymlink_SetsCloudAccountId(string prefix)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Symlinks at /tmp/ are only available on Linux (Lambda runtime).
            return;
        }

        var expectedAccountId = prefix + Random.Shared.NextInt64(100000000, 999999999).ToString();

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
