// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal.Services.Heartbeat;
using OpenTelemetry.OpAmp.Client.Messages;
using OpenTelemetry.OpAmp.Client.Settings;
using OpenTelemetry.OpAmp.Client.Tests.DataGenerators;
using OpenTelemetry.OpAmp.Client.Tests.Mocks;
using OpenTelemetry.OpAmp.Client.Tests.Tools;
using Xunit;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class OpAmpClientTests
{
    [Fact]
    public async Task OpAmpClient_SubscribeAndUnsubscribe()
    {
        using var opAmpServer = new OpAmpFakeHttpServer(false);
        var opAmpEndpoint = opAmpServer.Endpoint;

        using var mockListener = new MockListener();

        using var client = new OpAmpClient(o =>
        {
            o.ServerUrl = opAmpEndpoint;
            o.Heartbeat.IsEnabled = false;
        });
        client.Subscribe(mockListener);

        await client.StartAsync();

        // We don't currently have a direct way to send a message from the client to the server to trigger a response, so
        // this depends on the heartbeat messages from the server to the client.

        // Wait for the initial identification message response
        mockListener.WaitForMessages(TimeSpan.FromSeconds(1));

        Assert.Single(mockListener.Messages);

        await client.SendHeartbeatAsync(new HealthReport
        {
            StartTime = GetCurrentTimeInNanoseconds(),
            StatusTime = GetCurrentTimeInNanoseconds(),
            IsHealthy = true,
            Status = "OK",
        });

        // Wait for the heartbeat response
        mockListener.WaitForMessages(TimeSpan.FromSeconds(1));

        Assert.Equal(2, mockListener.Messages.Count);

        client.Unsubscribe(mockListener);

        await client.SendHeartbeatAsync(new HealthReport
        {
            StartTime = GetCurrentTimeInNanoseconds(),
            StatusTime = GetCurrentTimeInNanoseconds(),
            IsHealthy = true,
            Status = "OK",
        });

        mockListener.WaitForMessages(TimeSpan.FromSeconds(1));

        var serverFrames = opAmpServer.GetFrames();

        // We should have received 3 frames on the server: identification, heartbeat 1, heartbeat 2
        // The client should have received 2 messages before we unsubscribed.
        Assert.Equal(2, mockListener.Messages.Count);
        Assert.Equal(3, serverFrames.Count);

        await client.StopAsync();

        static ulong GetCurrentTimeInNanoseconds()
        {
            return (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000; // Convert to nanoseconds
        }
    }

    [Fact]
    public async Task DoesNotEmitHeartbeat_WhenDisabled()
    {
        using var opAmpServer = new OpAmpFakeHttpServer(false);
        var opAmpEndpoint = opAmpServer.Endpoint;

        using var mockListener = new MockListener();
        using var client = new OpAmpClient(o =>
        {
            o.ServerUrl = opAmpEndpoint;
            o.Heartbeat.IsEnabled = false;
        });
        client.Subscribe(mockListener);

        await client.StartAsync();

        mockListener.WaitForMessages(TimeSpan.FromSeconds(2));

        var frames = opAmpServer.GetFrames();

        // Only the identification message should be received
        Assert.Single(frames);
        Assert.Single(mockListener.Messages);

        await client.StopAsync();
    }

    [Theory]
    [ClassData(typeof(CapabilityTestData))]
    internal async Task SendsExpectedCapabilities(
        Action<OpAmpClientSettings> configure,
        IEnumerable<AgentCapabilities> expectedCapabilities,
        IEnumerable<AgentCapabilities> notExpectedCapabilities)
    {
        using var opAmpServer = new OpAmpFakeHttpServer(false);
        var opAmpEndpoint = opAmpServer.Endpoint;
        configure += o => o.ServerUrl = opAmpEndpoint;

        using var mockListener = new MockListener();
        using var client = new OpAmpClient(configure);
        client.Subscribe(mockListener);

        await client.StartAsync();

        var frames = opAmpServer.GetFrames();

        Assert.True(frames.Count >= 1, "Expecting at least one server frame.");

        var identificationFrame = frames[0];
        var capabilities = (AgentCapabilities)identificationFrame.Capabilities;

        foreach (var expectedCapability in expectedCapabilities)
        {
            Assert.True(capabilities.HasFlag(expectedCapability), $"Expected capabilities to include {expectedCapability}.");
        }

        foreach (var notExpectedCapability in notExpectedCapabilities)
        {
            Assert.False(capabilities.HasFlag(notExpectedCapability), $"Expected capabilities not to include {notExpectedCapability}.");
        }

        await client.StopAsync();
    }

    [Fact]
    internal async Task SendsEffectiveConfigFile_IsDisabledAndThrows()
    {
        // Setup OpAMP server
        using var opAmpServer = new OpAmpFakeHttpServer(false);
        var opAmpEndpoint = opAmpServer.Endpoint;

        using var client = new OpAmpClient(o =>
        {
            o.ServerUrl = opAmpEndpoint;
        });

        // Setup OpAMP content
        var configFileContents = Encoding.UTF8.GetBytes("test");
        var configFile = new EffectiveConfigFile(configFileContents, "plain/text", "my-configuration-file.txt");

        // Act
        await client.StartAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendEffectiveConfigAsync([configFile]));
        await client.StopAsync();
    }

    [Fact]
    internal async Task SendsEffectiveConfigFile_FromMemory()
    {
        // Setup OpAMP server
        using var opAmpServer = new OpAmpFakeHttpServer(false);
        var opAmpEndpoint = opAmpServer.Endpoint;

        using var client = new OpAmpClient(o =>
        {
            o.ServerUrl = opAmpEndpoint;
            o.EffectiveConfigurationReporting.EnableReporting = true;
        });

        // Setup OpAMP content
        const string fileName = "my-configuration-file.json";
        const string fileContentType = "application/json";
        const string fileContent =
            """
            {
                "version": 1.0,
                "name": "my-configuration",
                "is-active": true
            }
            """;
        var configFileContents = Encoding.UTF8.GetBytes(fileContent);
        var configFile = new EffectiveConfigFile(configFileContents, fileContentType, fileName);

        // Act
        await client.StartAsync();
        await client.SendEffectiveConfigAsync([configFile]);
        await client.StopAsync();

        // Assert received frames
        var frames = opAmpServer.GetFrames();
        var actualConfig = frames[1].EffectiveConfig.ConfigMap.ConfigMap[fileName];
        var actualContent = actualConfig.Body.ToStringUtf8();

        Assert.Equal(3, frames.Count); // 3 frames: 1 identification, 2 effective config, 3 disconnect
        Assert.Equal(fileContent, actualContent);
        Assert.Equal(fileContentType, actualConfig.ContentType);
    }

    [Fact]
    internal async Task SendsEffectiveConfigFile_FromFile()
    {
        // Setup OpAMP server
        using var opAmpServer = new OpAmpFakeHttpServer(false);
        var opAmpEndpoint = opAmpServer.Endpoint;

        using var client = new OpAmpClient(o =>
        {
            o.ServerUrl = opAmpEndpoint;
            o.EffectiveConfigurationReporting.EnableReporting = true;
        });

        // Setup OpAMP content
        const string fileContentType = "application/json";
        const string fileContent =
            """
            {
                "version": 1.0,
                "name": "my-configuration",
                "is-active": true
            }
            """;
        using var tempConfigFile = TempFile.Create(fileContent);
        var configFile = EffectiveConfigFile.CreateFromFilePath(tempConfigFile.FilePath, fileContentType);

        // Act
        await client.StartAsync();
        await client.SendEffectiveConfigAsync([configFile]);
        await client.StopAsync();

        // Assert received frames
        var frames = opAmpServer.GetFrames();
        var actualConfig = frames[1].EffectiveConfig.ConfigMap.ConfigMap[tempConfigFile.FileName];
        var actualContent = actualConfig.Body.ToStringUtf8();

        Assert.Equal(3, frames.Count); // 3 frames: 1 identification, 2 effective config, 3 disconnect
        Assert.Equal(fileContent, actualContent);
        Assert.Equal(fileContentType, actualConfig.ContentType);
    }

    internal class CapabilityTestData
        : TheoryData<Action<OpAmpClientSettings>, IEnumerable<AgentCapabilities>, IEnumerable<AgentCapabilities>>
    {
        public CapabilityTestData()
        {
            this.Add(o => o.Heartbeat.IsEnabled = false, [], [AgentCapabilities.ReportsHeartbeat, AgentCapabilities.ReportsHealth]);
            this.Add(o => o.Heartbeat.IsEnabled = true, [AgentCapabilities.ReportsHeartbeat, AgentCapabilities.ReportsHealth], []);
            this.Add(o => o.RemoteConfiguration.AcceptsRemoteConfig = true, [AgentCapabilities.AcceptsRemoteConfig], []);
            this.Add(o => o.RemoteConfiguration.AcceptsRemoteConfig = false, [], [AgentCapabilities.AcceptsRemoteConfig]);
            this.Add(o => o.EffectiveConfigurationReporting.EnableReporting = true, [AgentCapabilities.ReportsEffectiveConfig], []);
            this.Add(o => o.EffectiveConfigurationReporting.EnableReporting = false, [], [AgentCapabilities.ReportsEffectiveConfig]);
        }
    }
}
