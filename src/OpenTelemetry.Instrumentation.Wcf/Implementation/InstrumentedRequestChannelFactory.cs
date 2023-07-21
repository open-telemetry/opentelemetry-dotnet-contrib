// <copyright file="InstrumentedRequestChannelFactory.cs" company="OpenTelemetry Authors">
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

internal class InstrumentedRequestChannelFactory : InstrumentedChannelFactory, IChannelFactory<IRequestChannel>
{
    public InstrumentedRequestChannelFactory(IChannelFactory<IRequestChannel> inner)
        : base(inner)
    {
    }

    private new IChannelFactory<IRequestChannel> Inner { get => (IChannelFactory<IRequestChannel>)base.Inner; }

    IRequestChannel IChannelFactory<IRequestChannel>.CreateChannel(EndpointAddress to, Uri via)
    {
        return new InstrumentedRequestChannel(this.Inner.CreateChannel(to, via));
    }

    IRequestChannel IChannelFactory<IRequestChannel>.CreateChannel(EndpointAddress to)
    {
        return new InstrumentedRequestChannel(this.Inner.CreateChannel(to));
    }
}
