// <copyright file="InstrumentedDuplexSessionChannelFactory.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

internal class InstrumentedDuplexSessionChannelFactory : InstrumentedChannelFactory, IChannelFactory<IDuplexSessionChannel>
{
    private TimeSpan telemetryTimeOut;

    public InstrumentedDuplexSessionChannelFactory(IChannelFactory<IDuplexSessionChannel> inner, TimeSpan telemetryTimeOut)
        : base(inner)
    {
        this.telemetryTimeOut = telemetryTimeOut;
    }

    private new IChannelFactory<IDuplexSessionChannel> Inner { get => (IChannelFactory<IDuplexSessionChannel>)base.Inner; }

    IDuplexSessionChannel IChannelFactory<IDuplexSessionChannel>.CreateChannel(EndpointAddress to)
    {
        Guard.ThrowIfNull(to);

        return new InstrumentedDuplexSessionChannel(this.Inner.CreateChannel(to), this.telemetryTimeOut);
    }

    IDuplexSessionChannel IChannelFactory<IDuplexSessionChannel>.CreateChannel(EndpointAddress to, Uri via)
    {
        Guard.ThrowIfNull(to);
        Guard.ThrowIfNull(via);

        return new InstrumentedDuplexSessionChannel(this.Inner.CreateChannel(to, via), this.telemetryTimeOut);
    }
}
