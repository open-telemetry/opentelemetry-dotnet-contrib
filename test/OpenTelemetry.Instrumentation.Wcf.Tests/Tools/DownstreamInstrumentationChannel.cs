// <copyright file="DownstreamInstrumentationChannel.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.ServiceModel.Channels;

namespace OpenTelemetry.Instrumentation.Wcf.Tests.Tools;

public class DownstreamInstrumentationChannel : DispatchProxy
{
    public const string DownstreamInstrumentationSourceName = "DownstreamInstrumentationSource";
    private static readonly ActivitySource DownstreamInstrumentationSource = new ActivitySource(DownstreamInstrumentationSourceName);
    private static bool failNextReceive;

    private object Target { get; set; }

    public static TChannel Create<TChannel>(TChannel target)
    {
        var proxy = Create<TChannel, DownstreamInstrumentationChannel>() as DownstreamInstrumentationChannel;
        proxy.Target = target;
        return (TChannel)(object)proxy;
    }

    public static void FailNextReceive(bool shouldFail = true)
    {
        failNextReceive = shouldFail;
    }

    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        var toInstrument = new[]
        {
            nameof(IRequestChannel.Request),
            nameof(IRequestChannel.BeginRequest),
            nameof(IOutputChannel.Send),
            nameof(IOutputChannel.BeginSend),
        };

        using var activity = toInstrument.Contains(targetMethod.Name) ? DownstreamInstrumentationSource.StartActivity("DownstreamInstrumentation") : null;

        var receiveMethods = new[]
        {
            nameof(IInputChannel.Receive),
            nameof(IInputChannel.BeginReceive),
            nameof(IInputChannel.TryReceive),
            nameof(IInputChannel.BeginTryReceive),
        };

        if (failNextReceive && receiveMethods.Contains(targetMethod.Name))
        {
            failNextReceive = false;
            throw new Exception();
        }

        return targetMethod.Invoke(this.Target, args);
    }
}
