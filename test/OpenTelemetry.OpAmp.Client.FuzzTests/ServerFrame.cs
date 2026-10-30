// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using OpAmp.Proto.V1;

namespace OpenTelemetry.OpAmp.Client.FuzzTests;

/// <summary>
/// A well formed message from the server, built from a seed so that a failure can be replayed.
/// </summary>
public sealed class ServerFrame
{
    private static readonly string[] TextFragments = ["a", "b", "config", ".yaml", "/", " ", "\u00e9", "\u65e5\u672c", "\U0001F600", "\0", "text/yaml"];

    public ServerFrame(int seed)
    {
        this.Seed = seed;
        this.Message = Build(new Random(seed));
        this.Bytes = this.Message.ToByteArray();
    }

    public int Seed { get; }

    internal ServerToAgent Message { get; }

    internal byte[] Bytes { get; }

    public override string ToString() => $"Seed: {this.Seed}, Message: {JsonFormatter.Default.Format(this.Message)}";

    internal static byte[] RandomBytes(Random random, int maxLength)
    {
        var bytes = new byte[random.Next(0, maxLength + 1)];
        random.NextBytes(bytes);
        return bytes;
    }

    private static ServerToAgent Build(Random random)
    {
        var message = new ServerToAgent();

        if (Chance(random))
        {
            message.InstanceUid = ByteString.CopyFrom(RandomBytes(random, 20));
        }

        if (Chance(random))
        {
            message.ErrorResponse = new ServerErrorResponse
            {
                Type = (ServerErrorResponseType)Pick(random, 0, 1, 2, 3, 17, -1),
                ErrorMessage = Text(random),
            };
        }

        if (Chance(random))
        {
            var remoteConfig = new AgentRemoteConfig { ConfigHash = ByteString.CopyFrom(RandomBytes(random, 40)) };

            if (Chance(random))
            {
                remoteConfig.Config = new AgentConfigMap();

                for (var i = random.Next(0, 4); i > 0; i--)
                {
                    remoteConfig.Config.ConfigMap[Text(random)] = new AgentConfigFile
                    {
                        Body = ByteString.CopyFrom(RandomBytes(random, 300)),
                        ContentType = Text(random),
                    };
                }
            }

            message.RemoteConfig = remoteConfig;
        }

        if (Chance(random))
        {
            message.ConnectionSettings = new ConnectionSettingsOffers
            {
                Hash = ByteString.CopyFrom(RandomBytes(random, 40)),
                Opamp = new OpAMPConnectionSettings { DestinationEndpoint = Text(random) },
            };
        }

        if (Chance(random))
        {
            message.PackagesAvailable = new PackagesAvailable { AllPackagesHash = ByteString.CopyFrom(RandomBytes(random, 40)) };
        }

        message.Flags = Chance(random) ? RandomFlags(random) : 0;
        message.Capabilities = Chance(random) ? RandomFlags(random) : 0;

        if (Chance(random))
        {
            message.AgentIdentification = new AgentIdentification { NewInstanceUid = ByteString.CopyFrom(RandomBytes(random, 20)) };
        }

        if (Chance(random))
        {
            message.Command = new ServerToAgentCommand { Type = (CommandType)Pick(random, 0, 1, 7) };
        }

        if (Chance(random))
        {
            var customCapabilities = new CustomCapabilities();

            for (var i = random.Next(0, 4); i > 0; i--)
            {
                customCapabilities.Capabilities.Add(Text(random));
            }

            message.CustomCapabilities = customCapabilities;
        }

        if (Chance(random))
        {
            message.CustomMessage = new CustomMessage
            {
                Capability = Text(random),
                Type = Text(random),
                Data = ByteString.CopyFrom(RandomBytes(random, 300)),
            };
        }

        return message;
    }

    private static bool Chance(Random random) => random.Next(2) == 0;

    private static T Pick<T>(Random random, params T[] values) => values[random.Next(values.Length)];

    private static string Text(Random random) =>
        string.Concat(Enumerable.Range(0, random.Next(0, 5)).Select(_ => TextFragments[random.Next(TextFragments.Length)]));

    private static ulong RandomFlags(Random random) => random.Next(5) switch
    {
        0 => 1UL << random.Next(0, 7),
        1 => (ulong)random.Next(0, 128),
        2 => 1UL << random.Next(0, 64),
        3 => ulong.MaxValue,
        _ => ((ulong)(uint)random.Next() << 32) | (uint)random.Next(),
    };
}
