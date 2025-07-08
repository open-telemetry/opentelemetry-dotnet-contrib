// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Utils;
using Xunit;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class OpAmpHeaderHelperTests
{
    [Theory]
    [InlineData(0, new byte[] { 0x00 })]
    [InlineData(1, new byte[] { 0x01 })]
    [InlineData(127, new byte[] { 0x7F })]
    [InlineData(128, new byte[] { 0x80, 0x01 })]
    [InlineData(987654321, new byte[] { 0xB1, 0xD1, 0xF9, 0xD6, 0x03 })]
    [InlineData(ulong.MaxValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 })]
    public void OpAmpHeaderHelper_Encode(ulong value, byte[] verifiedEncoded)
    {
        var buffer = new ArraySegment<byte>(new byte[10]);
        var encodedLength = OpAmpWsHeaderHelper.EncodeVarint64(buffer, value);

        Assert.Equal(verifiedEncoded.Length, encodedLength);
        Assert.Equal(verifiedEncoded, buffer.Slice(0, encodedLength).ToArray());
    }

    [Theory]
    [InlineData(0, 1, new byte[] { 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
    [InlineData(1, 1, new byte[] { 0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
    [InlineData(127, 1, new byte[] { 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
    [InlineData(128, 2, new byte[] { 0x80, 0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
    [InlineData(987654321, 5, new byte[] { 0xB1, 0xD1, 0xF9, 0xD6, 0x03, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
    [InlineData(ulong.MaxValue, 10, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 })]
    public void OpAmpHeaderHelper_Decode(ulong value, int verifiedLength, byte[] buffer)
    {
        var result = OpAmpWsHeaderHelper.DecodeVarint64(new ArraySegment<byte>(buffer), out int bytesRead);

        Assert.Equal(value, result);
        Assert.Equal(verifiedLength, bytesRead);
    }
}
