// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Providers;

namespace OpenTelemetry.DynamicControl.Tests;

public class PolicyProviderPayloadTests
{
    [Fact]
    public void Content_PreservedVerbatim()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var payload = new PolicyProviderPayload(bytes);

        Assert.Equal(bytes, payload.Content.ToArray());
    }

    [Fact]
    public void Content_EmptyIsLegal()
    {
        var payload = new PolicyProviderPayload([]);

        Assert.True(payload.Content.IsEmpty, "Content should be empty when constructed from an empty span.");
    }

    [Fact]
    public void Version_DefaultsToEmpty()
    {
        var payload = new PolicyProviderPayload([]);

        Assert.True(payload.Version.IsEmpty, "Version should default to empty when not specified.");
        Assert.Equal(PolicyProviderVersion.Empty, payload.Version);
    }

    [Fact]
    public void Version_ExplicitVersionIsPreserved()
    {
        var version = new PolicyProviderVersion("v1.2.3");
        var payload = new PolicyProviderPayload([], version);

        Assert.Equal(version, payload.Version);
        Assert.Equal("v1.2.3", payload.Version.Value);
    }

    [Fact]
    public void Content_InputBufferChanged_PreservesSnapshot()
    {
        byte[] bytes = [1, 2, 3];
        var payload = new PolicyProviderPayload(bytes);

        bytes[0] = 99;

        Assert.Equal([1, 2, 3], payload.Content.ToArray());
    }

    [Fact]
    public void Content_SlicedInput_CopiesOnlySelectedBytes()
    {
        byte[] bytes = [0, 0xC3, 0x28, 0];
        var payload = new PolicyProviderPayload(bytes.AsSpan(1, 2));

        Array.Clear(bytes, 0, bytes.Length);

        Assert.Equal([0xC3, 0x28], payload.Content.ToArray());
    }

    [Fact]
    public void Dispose_ClearsContent()
    {
        var payload = new PolicyProviderPayload([1, 2, 3]);

        payload.Dispose();

        Assert.True(payload.Content.IsEmpty, "Content should be empty after Dispose returns the pooled buffer.");
    }

    [Fact]
    public void Dispose_CalledMoreThanOnce_DoesNotThrow()
    {
        var payload = new PolicyProviderPayload([1, 2, 3]);

        payload.Dispose();
        var exception = Record.Exception(payload.Dispose);

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_EmptyContent_DoesNotThrow()
    {
        var payload = new PolicyProviderPayload([]);

        var exception = Record.Exception(payload.Dispose);

        Assert.Null(exception);
    }
}
