// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Services.Heartbeat;
using OpenTelemetry.OpAmp.Client.Messages;

namespace OpenTelemetry.OpAmp.Client.Tests.DataGenerators;

internal sealed class FrameBuilderTestData : TheoryData<string>
{
    private static readonly Dictionary<string, (Func<IFrameBuilder, IFrameBuilder> AddMessage, Func<AgentToServer, object> PropertyFetcher)> TestCases = new()
    {
        ["AgentDescription"] = (fb => fb.AddAgentDescription(), m => m.AgentDescription),
        ["Health"] = (fb => fb.AddHealth(new HealthReport { IsHealthy = true }), m => m.Health),
        ["AgentDisconnect"] = (fb => fb.AddAgentDisconnect(), m => m.AgentDisconnect),
        ["RemoteConfigStatus Applied"] = (fb => fb.AddRemoteConfigStatus(new RemoteConfigStatusReport([1, 2, 3], RemoteConfigStatusCode.Applied)), m => m.RemoteConfigStatus),
        ["RemoteConfigStatus Applying"] = (fb => fb.AddRemoteConfigStatus(new RemoteConfigStatusReport([1, 2, 3], RemoteConfigStatusCode.Applying)), m => m.RemoteConfigStatus),
        ["RemoteConfigStatus Unset"] = (fb => fb.AddRemoteConfigStatus(new RemoteConfigStatusReport([1, 2, 3], RemoteConfigStatusCode.Unset)), m => m.RemoteConfigStatus),
    };

    public FrameBuilderTestData()
    {
        foreach (var name in TestCases.Keys)
        {
            this.Add(name);
        }
    }

    public static (Func<IFrameBuilder, IFrameBuilder> AddMessage, Func<AgentToServer, object> PropertyFetcher) GetTestCase(string name)
        => TestCases[name];
}
