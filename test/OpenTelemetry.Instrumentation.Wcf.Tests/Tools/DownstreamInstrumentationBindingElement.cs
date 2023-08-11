// <copyright file="DownstreamInstrumentationBindingElement.cs" company="OpenTelemetry Authors">
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

public class DownstreamInstrumentationBindingElement : BindingElement
{
    public override BindingElement Clone()
    {
        return new DownstreamInstrumentationBindingElement();
    }

    public override T GetProperty<T>(BindingContext context)
    {
        return context.GetInnerProperty<T>();
    }

    public override IChannelFactory<TChannel> BuildChannelFactory<TChannel>(BindingContext context)
    {
        var proxy = DispatchProxy.Create<IChannelFactory<TChannel>, DownstreamInstrumentationChannelFactory<TChannel>>()
            as DownstreamInstrumentationChannelFactory<TChannel>;
        proxy.Target = base.BuildChannelFactory<TChannel>(context);
        return (IChannelFactory<TChannel>)proxy;
    }
}
