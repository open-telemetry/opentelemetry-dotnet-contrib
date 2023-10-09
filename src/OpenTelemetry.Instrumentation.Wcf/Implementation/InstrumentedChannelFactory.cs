// <copyright file="InstrumentedChannelFactory.cs" company="OpenTelemetry Authors">
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
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

internal sealed class InstrumentedChannelFactory<TChannel> : InstrumentedChannelFactoryBase<IChannelFactory<TChannel>>, IChannelFactory<TChannel>
{
    private readonly CustomBinding binding;

    public InstrumentedChannelFactory(IChannelFactory<TChannel> inner, CustomBinding binding)
        : base(inner)
    {
        this.binding = binding;
    }

    TChannel IChannelFactory<TChannel>.CreateChannel(EndpointAddress to, Uri via)
    {
        return this.WrapChannel(this.Inner.CreateChannel(to, via));
    }

    TChannel IChannelFactory<TChannel>.CreateChannel(EndpointAddress to)
    {
        return this.WrapChannel(this.Inner.CreateChannel(to));
    }

    private TChannel WrapChannel(TChannel innerChannel)
    {
        if (typeof(TChannel) == typeof(IRequestChannel) || typeof(TChannel) == typeof(IRequestSessionChannel))
        {
            return (TChannel)(IRequestChannel)new InstrumentedRequestChannel((IRequestChannel)innerChannel);
        }

        if (typeof(TChannel) == typeof(IDuplexChannel) || typeof(TChannel) == typeof(IDuplexSessionChannel))
        {
            return (TChannel)(IDuplexChannel)new InstrumentedDuplexChannel((IDuplexChannel)innerChannel, this.binding.SendTimeout);
        }

        throw new NotImplementedException();
    }
}
