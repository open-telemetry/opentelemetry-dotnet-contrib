// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Messages;

namespace OpenTelemetry.OpAmp.Client.Tests.Messages;

public class FlagsMessageTests
{
    [Theory]
    [InlineData(global::OpAmp.Proto.V1.ServerToAgentFlags.Unspecified, ServerCommands.None)]
    [InlineData(global::OpAmp.Proto.V1.ServerToAgentFlags.ReportFullState, ServerCommands.ReportFullState)]
    [InlineData(global::OpAmp.Proto.V1.ServerToAgentFlags.ReportAvailableComponents, ServerCommands.ReportAvailableComponents)]
    [InlineData(global::OpAmp.Proto.V1.ServerToAgentFlags.ReportFullState | global::OpAmp.Proto.V1.ServerToAgentFlags.ReportAvailableComponents, ServerCommands.ReportFullState | ServerCommands.ReportAvailableComponents)]
    internal void Constructor_WithValidMessage_InitializesFlagsMessage(global::OpAmp.Proto.V1.ServerToAgentFlags flags, ServerCommands expectedCommands)
    {
        // Act
        var flagsMessage = new FlagsMessage(flags);

        // Assert
        Assert.Equal(expectedCommands, flagsMessage.Flags);
    }
}
