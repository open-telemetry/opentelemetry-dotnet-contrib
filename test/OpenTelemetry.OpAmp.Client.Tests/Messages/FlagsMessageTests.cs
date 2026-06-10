// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Messages;
using OpAmpProto = OpAmp.Proto.V1;

namespace OpenTelemetry.OpAmp.Client.Tests.Messages;

public class FlagsMessageTests
{
    [Theory]
    [InlineData(OpAmpProto.ServerToAgentFlags.Unspecified, ServerCommands.None)]
    [InlineData(OpAmpProto.ServerToAgentFlags.ReportFullState, ServerCommands.ReportFullState)]
    [InlineData(OpAmpProto.ServerToAgentFlags.ReportAvailableComponents, ServerCommands.ReportAvailableComponents)]
    [InlineData(OpAmpProto.ServerToAgentFlags.ReportFullState | OpAmpProto.ServerToAgentFlags.ReportAvailableComponents, ServerCommands.ReportFullState | ServerCommands.ReportAvailableComponents)]
    internal void Constructor_WithValidMessage_InitializesFlagsMessage(OpAmpProto.ServerToAgentFlags flags, ServerCommands expectedCommands)
    {
        // Act
        var flagsMessage = new FlagsMessage(flags);

        // Assert
        Assert.Equal(expectedCommands, flagsMessage.Flags);
    }
}
