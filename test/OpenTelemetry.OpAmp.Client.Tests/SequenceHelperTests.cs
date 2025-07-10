// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using OpenTelemetry.OpAmp.Client.Utils;
using Xunit;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class SequenceHelperTests
{
    [Fact]
    public void SequenceHelperTests_ReadAllSequence()
    {
        var buffer1 = new byte[] { 0x01, 0x02, 0x03 };
        var buffer2 = new byte[] { 0x04, 0x05, 0x06, 0x07 };
        var buffer3 = new byte[] { 0x08, 0x09 };
        var endIndex = buffer3.Length;

        var sequence = SequenceHelper.CreateSequenceFromBuffers([buffer1, buffer2, buffer3], endIndex);
        var array = sequence.ToArray();

        var expected = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 };
        Assert.Equal(expected, array);
    }
}
