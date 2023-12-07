// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.ServiceModel.Channels;

namespace OpenTelemetry.Instrumentation.Wcf.Tests.Tools;

public class DownstreamInstrumentationChannelFactory<TChannel> : DispatchProxy
    where TChannel : notnull
{
    public IChannelFactory<TChannel>? Target { get; set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var returnValue = targetMethod!.Invoke(this.Target, args);
        if (targetMethod.Name == nameof(IChannelFactory<TChannel>.CreateChannel))
        {
            return DownstreamInstrumentationChannel.Create((TChannel)returnValue!);
        }

        return returnValue;
    }
}
