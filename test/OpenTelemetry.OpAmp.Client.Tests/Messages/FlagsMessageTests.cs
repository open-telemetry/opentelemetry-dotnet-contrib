// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Messages;
using OpAmpProto = OpAmp.Proto.V1;

namespace OpenTelemetry.OpAmp.Client.Tests.Messages;

public class FlagsMessageTests
{
    [Theory]
    [InlineData(OpAmpProto.ServerToAgentFlags.Unspecified, ServerSentFlags.None)]
    [InlineData(OpAmpProto.ServerToAgentFlags.ReportFullState, ServerSentFlags.ReportFullState)]
    [InlineData(OpAmpProto.ServerToAgentFlags.ReportAvailableComponents, ServerSentFlags.ReportAvailableComponents)]
    [InlineData(OpAmpProto.ServerToAgentFlags.ReportFullState | OpAmpProto.ServerToAgentFlags.ReportAvailableComponents, ServerSentFlags.ReportFullState | ServerSentFlags.ReportAvailableComponents)]
    internal void Constructor_WithValidMessage_InitializesFlagsMessage(OpAmpProto.ServerToAgentFlags flags, ServerSentFlags expectedCommands)
    {
        // Act
        var flagsMessage = new FlagsMessage(flags);

        // Assert
        Assert.Equal(expectedCommands, flagsMessage.Flags);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(1UL << 31)]
    [InlineData(1UL << 32)]
    [InlineData(1UL << 63)]
    [InlineData(ulong.MaxValue)]
    internal void Constructor_WithFlagsWiderThan32Bits_PreservesEveryBit(ulong flags)
    {
        // Act
        var flagsMessage = new FlagsMessage(flags);

        // Assert
        Assert.Equal(flags, (ulong)flagsMessage.Flags);
    }
}
