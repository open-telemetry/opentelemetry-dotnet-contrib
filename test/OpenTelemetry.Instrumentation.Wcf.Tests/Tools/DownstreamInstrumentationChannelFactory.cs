// <copyright file="DownstreamInstrumentationChannelFactory.cs" company="OpenTelemetry Authors">
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

using System.Reflection;
using System.ServiceModel.Channels;

namespace OpenTelemetry.Instrumentation.Wcf.Tests.Tools;

public class DownstreamInstrumentationChannelFactory<TChannel> : DispatchProxy
{
    public IChannelFactory<TChannel> Target { get; set; }

    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        var returnValue = targetMethod.Invoke(this.Target, args);
        if (targetMethod.Name == nameof(IChannelFactory<TChannel>.CreateChannel))
        {
            return DownstreamInstrumentationChannel.Create((TChannel)returnValue);
        }

        return returnValue;
    }
}
