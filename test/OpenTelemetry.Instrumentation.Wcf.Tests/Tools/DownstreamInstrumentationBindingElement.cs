// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.ServiceModel.Channels;

namespace OpenTelemetry.Instrumentation.Wcf.Tests.Tools;

#pragma warning disable CA1515 // Make class internal, public is needed for WCF
public class DownstreamInstrumentationBindingElement : BindingElement
#pragma warning restore CA1515 // Make class internal, public is needed for WCF
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
        var proxy = (DownstreamInstrumentationChannelFactory<TChannel>)DispatchProxy.Create<IChannelFactory<TChannel>, DownstreamInstrumentationChannelFactory<TChannel>>();
        proxy.Target = base.BuildChannelFactory<TChannel>(context);
        return (IChannelFactory<TChannel>)proxy;
    }
}
