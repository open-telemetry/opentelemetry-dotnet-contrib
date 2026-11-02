// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Messages;
using OpAmpProto = OpAmp.Proto.V1;

namespace OpenTelemetry.OpAmp.Client.Tests.Messages;

public class ServerCapabilitiesMessageTests
{
    [Theory]
    [InlineData(OpAmpProto.ServerCapabilities.Unspecified, ServerSentCapabilities.None)]
    [InlineData(OpAmpProto.ServerCapabilities.AcceptsStatus, ServerSentCapabilities.AcceptsStatus)]
    [InlineData(OpAmpProto.ServerCapabilities.OffersRemoteConfig, ServerSentCapabilities.OffersRemoteConfig)]
    [InlineData(OpAmpProto.ServerCapabilities.AcceptsEffectiveConfig, ServerSentCapabilities.AcceptsEffectiveConfig)]
    [InlineData(OpAmpProto.ServerCapabilities.OffersPackages, ServerSentCapabilities.OffersPackages)]
    [InlineData(OpAmpProto.ServerCapabilities.AcceptsPackagesStatus, ServerSentCapabilities.AcceptsPackagesStatus)]
    [InlineData(OpAmpProto.ServerCapabilities.OffersConnectionSettings, ServerSentCapabilities.OffersConnectionSettings)]
    [InlineData(OpAmpProto.ServerCapabilities.AcceptsConnectionSettingsRequest, ServerSentCapabilities.AcceptsConnectionSettingsRequest)]
    [InlineData(OpAmpProto.ServerCapabilities.AcceptsStatus | OpAmpProto.ServerCapabilities.OffersPackages, ServerSentCapabilities.AcceptsStatus | ServerSentCapabilities.OffersPackages)]
    internal void Constructor_WithValidMessage_InitializesFlagsMessage(OpAmpProto.ServerCapabilities capabilities, ServerSentCapabilities expectedCommands)
    {
        // Act
        var flagsMessage = new ServerCapabilitiesMessage(capabilities);

        // Assert
        Assert.Equal(expectedCommands, flagsMessage.Capabilities);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(1UL << 31)]
    [InlineData(1UL << 32)]
    [InlineData(1UL << 63)]
    [InlineData(ulong.MaxValue)]
    internal void Constructor_WithCapabilitiesWiderThan32Bits_PreservesEveryBit(ulong capabilities)
    {
        // Act
        var message = new ServerCapabilitiesMessage(capabilities);

        // Assert
        Assert.Equal(capabilities, (ulong)message.Capabilities);
    }
}
