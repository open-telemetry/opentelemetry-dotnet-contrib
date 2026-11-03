// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Messages;
using OpenTelemetry.OpAmp.Client.Messages;

namespace OpenTelemetry.OpAmp.Client.FuzzTests;

/// <summary>
/// Listens for every kind of message the frame processor can dispatch.
/// </summary>
internal sealed class ServerMessageRecorder
{
    public ServerMessageRecorder(FrameProcessor processor)
    {
        processor.Subscribe(this.Envelope);
        processor.Subscribe(this.ErrorResponse);
        processor.Subscribe(this.RemoteConfig);
        processor.Subscribe(this.ConnectionSettings);
        processor.Subscribe(this.PackagesAvailable);
        processor.Subscribe(this.Capabilities);
        processor.Subscribe(this.Flags);
        processor.Subscribe(this.AgentIdentification);
        processor.Subscribe(this.Command);
        processor.Subscribe(this.CustomCapabilities);
        processor.Subscribe(this.CustomMessage);
    }

    public RecordingListener<ServerToAgentMessage> Envelope { get; } = new();

    public RecordingListener<ErrorResponseMessage> ErrorResponse { get; } = new();

    public RecordingListener<RemoteConfigMessage> RemoteConfig { get; } = new();

    public RecordingListener<ConnectionSettingsMessage> ConnectionSettings { get; } = new();

    public RecordingListener<PackagesAvailableMessage> PackagesAvailable { get; } = new();

    public RecordingListener<ServerCapabilitiesMessage> Capabilities { get; } = new();

    public RecordingListener<FlagsMessage> Flags { get; } = new();

    public RecordingListener<AgentIdentificationMessage> AgentIdentification { get; } = new();

    public RecordingListener<CommandMessage> Command { get; } = new();

    public RecordingListener<CustomCapabilitiesMessage> CustomCapabilities { get; } = new();

    public RecordingListener<CustomMessageMessage> CustomMessage { get; } = new();

    public int Count =>
        this.Envelope.Messages.Count +
        this.ErrorResponse.Messages.Count +
        this.RemoteConfig.Messages.Count +
        this.ConnectionSettings.Messages.Count +
        this.PackagesAvailable.Messages.Count +
        this.Capabilities.Messages.Count +
        this.Flags.Messages.Count +
        this.AgentIdentification.Messages.Count +
        this.Command.Messages.Count +
        this.CustomCapabilities.Messages.Count +
        this.CustomMessage.Messages.Count;

    /// <summary>
    /// Asserts that the messages dispatched are exactly those which the message that was received contains.
    /// </summary>
    /// <param name="sent">The message the server sent.</param>
    public void AssertDispatched(ServerToAgent sent)
    {
        Assert.Equal(sent, Assert.Single(this.Envelope.Messages).Message);

        AssertDispatched(sent.ErrorResponse, this.ErrorResponse, (expected, actual) => Assert.Equal(expected, actual.ErrorResponse));
        AssertDispatched(sent.ConnectionSettings, this.ConnectionSettings, (expected, actual) => Assert.Equal(expected, actual.ConnectionSettings));
        AssertDispatched(sent.PackagesAvailable, this.PackagesAvailable, (expected, actual) => Assert.Equal(expected, actual.PackagesAvailable));
        AssertDispatched(sent.AgentIdentification, this.AgentIdentification, (expected, actual) => Assert.Equal(expected, actual.AgentIdentification));
        AssertDispatched(sent.Command, this.Command, (expected, actual) => Assert.Equal(expected, actual.Command));

        AssertDispatched(sent.RemoteConfig, this.RemoteConfig, (expected, actual) =>
        {
            Assert.Equal(expected.ConfigHash.ToByteArray(), actual.ConfigHash.ToArray());

            var expectedFiles = expected.Config?.ConfigMap ?? [];
            Assert.Equal(expectedFiles.Count, actual.AgentConfigMap.Count);

            foreach (var file in expectedFiles)
            {
                Assert.True(actual.AgentConfigMap.TryGetValue(file.Key, out var actualFile), file.Key);
                Assert.Equal(file.Key, actualFile.Name);
                Assert.Equal(file.Value.Body.ToByteArray(), actualFile.Body.ToArray());
                Assert.Equal(file.Value.ContentType, actualFile.ContentType ?? string.Empty);
            }
        });

        AssertDispatched(sent.CustomCapabilities, this.CustomCapabilities, (expected, actual) =>
            Assert.Equal(expected.Capabilities, actual.Capabilities));

        AssertDispatched(sent.CustomMessage, this.CustomMessage, (expected, actual) =>
        {
            Assert.Equal(expected.Capability, actual.Capability);
            Assert.Equal(expected.Type, actual.Type);
            Assert.Equal(expected.Data.ToByteArray(), actual.Data.ToArray());
        });

        // Whether the flags and the capabilities were dispatched is checked here, and their values by AssertFlagsAndCapabilities.
        Assert.Equal(sent.Capabilities != 0 ? 1 : 0, this.Capabilities.Messages.Count);
        Assert.Equal(sent.Flags != 0 ? 1 : 0, this.Flags.Messages.Count);
    }

    /// <summary>
    /// Asserts that the flags and the capabilities dispatched are the 64 bits the server sent.
    /// </summary>
    /// <param name="sent">The message the server sent.</param>
    public void AssertFlagsAndCapabilities(ServerToAgent sent)
    {
        if (sent.Capabilities != 0)
        {
            Assert.Equal(sent.Capabilities, (ulong)Assert.Single(this.Capabilities.Messages).Capabilities);
        }

        if (sent.Flags != 0)
        {
            Assert.Equal(sent.Flags, (ulong)Assert.Single(this.Flags.Messages).Flags);
        }
    }

    private static void AssertDispatched<TSent, TMessage>(
        TSent? sent,
        RecordingListener<TMessage> listener,
        Action<TSent, TMessage> assertEqual)
        where TSent : class
        where TMessage : OpAmpMessage
    {
        if (sent is null)
        {
            Assert.Empty(listener.Messages);
        }
        else
        {
            assertEqual(sent, Assert.Single(listener.Messages));
        }
    }
}
