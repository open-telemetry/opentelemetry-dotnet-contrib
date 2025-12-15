// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Google.Protobuf;
using Xunit;

namespace OpenTelemetry.OpAmp.Client.Tests.Messages;

public class RemoteConfigMessageTests
{
    private const string JsonString = "{ \"myKey\": \"this is a value\" }";
    private const string YamlString = "enabled: true";
    private const string HashString = "dummy-hash";
    private const string Config1Name = "config1";
    private const string Config2Name = "config2";
    private const string JsonContentType = "application/json";
    private const string YamlContentType = "application/yaml";

    [Fact]
    public void Constructor_WithValidConfig_InitializesConfigMap()
    {
        // Arrange
        var agentRemoteConfig = this.CreateAgentRemoteConfig();

        // Act
        var remoteConfigMessage = new Client.Messages.RemoteConfigMessage(agentRemoteConfig);

        // Assert
        Assert.Equal(2, remoteConfigMessage.AgentConfigMap.Count);
        Assert.True(remoteConfigMessage.AgentConfigMap.ContainsKey(Config1Name));
        Assert.True(remoteConfigMessage.AgentConfigMap.ContainsKey(Config2Name));
    }

    [Fact]
    public void Constructor_WithEmptyConfigMap_InitializesEmptyDictionary()
    {
        // Arrange
        var agentRemoteConfig = new global::OpAmp.Proto.V1.AgentRemoteConfig
        {
            Config = new global::OpAmp.Proto.V1.AgentConfigMap(),
            ConfigHash = ByteString.CopyFromUtf8(HashString),
        };

        // Act
        var remoteConfigMessage = new Client.Messages.RemoteConfigMessage(agentRemoteConfig);

        // Assert
        Assert.Empty(remoteConfigMessage.AgentConfigMap);
    }

    [Fact]
    public void GetConfigHashBytes_WithValidHash_ReturnsExpectedBytes()
    {
        // Arrange
        var agentRemoteConfig = this.CreateAgentRemoteConfig();
        var remoteConfigMessage = new Client.Messages.RemoteConfigMessage(agentRemoteConfig);

        // Act
        var hashBytes = remoteConfigMessage.GetConfigHashBytes();

        // Assert
        Assert.Equal(Encoding.UTF8.GetByteCount(HashString), hashBytes.Length);
        Assert.Equal(HashString, Encoding.UTF8.GetString(hashBytes));
    }

    [Fact]
    public void GetConfigHashUtf8String_WithValidHash_ReturnsExpectedString()
    {
        // Arrange
        var agentRemoteConfig = this.CreateAgentRemoteConfig();
        var remoteConfigMessage = new Client.Messages.RemoteConfigMessage(agentRemoteConfig);

        // Act
        var hashString = remoteConfigMessage.GetConfigHashUtf8String();

        // Assert
        Assert.Equal(HashString, hashString);
    }

    [Fact]
    public void HashLength_WithValidHash_ReturnsExpectedLength()
    {
        // Arrange
        var agentRemoteConfig = this.CreateAgentRemoteConfig();
        var remoteConfigMessage = new Client.Messages.RemoteConfigMessage(agentRemoteConfig);

        // Act & Assert
        Assert.Equal(Encoding.UTF8.GetByteCount(HashString), remoteConfigMessage.HashLength);
        Assert.Equal(remoteConfigMessage.GetConfigHashBytes().Length, remoteConfigMessage.HashLength);
    }

    [Fact]
    public void TryGetConfigHash_WithSufficientBuffer_ReturnsTrueAndWritesBytes()
    {
        // Arrange
        var agentRemoteConfig = this.CreateAgentRemoteConfig();
        var remoteConfigMessage = new Client.Messages.RemoteConfigMessage(agentRemoteConfig);
        Span<byte> hashSpan = stackalloc byte[remoteConfigMessage.HashLength];

        // Act
        var result = remoteConfigMessage.TryGetConfigHash(hashSpan, out int bytesWritten);

        // Assert
        Assert.True(result);
        Assert.Equal(remoteConfigMessage.HashLength, bytesWritten);
        Assert.Equal(HashString, Encoding.UTF8.GetString(hashSpan.ToArray()));
    }

    [Fact]
    public void TryGetConfigHash_WithInsufficientBuffer_ReturnsFalse()
    {
        // Arrange
        var agentRemoteConfig = this.CreateAgentRemoteConfig();
        var remoteConfigMessage = new Client.Messages.RemoteConfigMessage(agentRemoteConfig);
        Span<byte> hashSpan = stackalloc byte[remoteConfigMessage.HashLength - 1];

        // Act
        var result = remoteConfigMessage.TryGetConfigHash(hashSpan, out int bytesWritten);

        // Assert
        Assert.False(result);
        Assert.Equal(0, bytesWritten);
    }

    [Theory]
    [InlineData(Config1Name, JsonContentType, JsonString)]
    [InlineData(Config2Name, YamlContentType, YamlString)]
    public void AgentConfigFile_Properties_ReturnExpectedValues(string configName, string contentType, string bodyContent)
    {
        // Arrange
        var agentRemoteConfig = this.CreateAgentRemoteConfig();
        var remoteConfigMessage = new Client.Messages.RemoteConfigMessage(agentRemoteConfig);

        // Act
        var configFile = remoteConfigMessage.AgentConfigMap[configName];

        // Assert
        Assert.Equal(configName, configFile.Name);
        Assert.Equal(contentType, configFile.ContentType);
        Assert.Equal(Encoding.UTF8.GetByteCount(bodyContent), configFile.BodyLength);
    }

    [Fact]
    public void AgentConfigFile_GetBodyBytes_ReturnsExpectedContent()
    {
        // Arrange
        var agentRemoteConfig = this.CreateAgentRemoteConfig();
        var remoteConfigMessage = new Client.Messages.RemoteConfigMessage(agentRemoteConfig);

        // Act
        var config1 = remoteConfigMessage.AgentConfigMap[Config1Name];
        var bodyBytes = config1.GetBodyBytes();

        // Assert
        Assert.Equal(bodyBytes.Length, config1.BodyLength);
        Assert.Equal(JsonString, Encoding.UTF8.GetString(bodyBytes));
    }

    [Fact]
    public void AgentConfigFile_TryGetBody_WithSufficientBuffer_ReturnsTrueAndWritesBytes()
    {
        // Arrange
        var agentRemoteConfig = this.CreateAgentRemoteConfig();
        var remoteConfigMessage = new Client.Messages.RemoteConfigMessage(agentRemoteConfig);
        var config1 = remoteConfigMessage.AgentConfigMap[Config1Name];
        Span<byte> bodySpan = stackalloc byte[config1.BodyLength];

        // Act
        var result = config1.TryGetBody(bodySpan, out int bytesWritten);

        // Assert
        Assert.True(result);
        Assert.Equal(config1.BodyLength, bytesWritten);
        Assert.Equal(JsonString, Encoding.UTF8.GetString(bodySpan.ToArray()));
    }

    [Fact]
    public void AgentConfigFile_TryGetBody_WithInsufficientBuffer_ReturnsFalse()
    {
        // Arrange
        var agentRemoteConfig = this.CreateAgentRemoteConfig();
        var remoteConfigMessage = new Client.Messages.RemoteConfigMessage(agentRemoteConfig);
        var config1 = remoteConfigMessage.AgentConfigMap[Config1Name];
        Span<byte> bodySpan = stackalloc byte[config1.BodyLength - 1];

        // Act
        var result = config1.TryGetBody(bodySpan, out int bytesWritten);

        // Assert
        Assert.False(result);
        Assert.Equal(0, bytesWritten);
    }

#if NET
    [Fact]
    public void AgentConfigFile_JsonContent_CanBeDeserialized()
    {
        // Arrange
        var agentRemoteConfig = this.CreateAgentRemoteConfig();
        var remoteConfigMessage = new Client.Messages.RemoteConfigMessage(agentRemoteConfig);
        var config1 = remoteConfigMessage.AgentConfigMap[Config1Name];
        Span<byte> bodySpan = stackalloc byte[config1.BodyLength];
        Assert.True(config1.TryGetBody(bodySpan, out _));

        // Act
        var config = System.Text.Json.JsonSerializer.Deserialize<Config>(bodySpan);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("this is a value", config!.MyKey);
    }
#endif

    private global::OpAmp.Proto.V1.AgentRemoteConfig CreateAgentRemoteConfig()
    {
        var configMap = new global::OpAmp.Proto.V1.AgentConfigMap();

        configMap.ConfigMap.Add(Config1Name, new global::OpAmp.Proto.V1.AgentConfigFile
        {
            Body = ByteString.CopyFromUtf8(JsonString),
            ContentType = JsonContentType,
        });

        configMap.ConfigMap.Add(Config2Name, new global::OpAmp.Proto.V1.AgentConfigFile
        {
            Body = ByteString.CopyFromUtf8(YamlString),
            ContentType = YamlContentType,
        });

        return new global::OpAmp.Proto.V1.AgentRemoteConfig
        {
            Config = configMap,
            ConfigHash = ByteString.CopyFromUtf8(HashString),
        };
    }

#if NET
    private class Config
    {
        [System.Text.Json.Serialization.JsonPropertyName("myKey")]
        public string? MyKey { get; set; }
    }
#endif
}
