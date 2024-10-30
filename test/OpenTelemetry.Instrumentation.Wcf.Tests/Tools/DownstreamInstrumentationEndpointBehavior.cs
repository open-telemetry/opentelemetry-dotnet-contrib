// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace OpenTelemetry.Instrumentation.Wcf.Tests.Tools;

#pragma warning disable CA1515 // Make class internal, public is needed for WCF
public class DownstreamInstrumentationEndpointBehavior : IEndpointBehavior
#pragma warning restore CA1515 // Make class internal, public is needed for WCF
{
    public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
    {
    }

    public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
    {
        var newBinding = new CustomBinding(endpoint.Binding);
        newBinding.Elements.Insert(0, new DownstreamInstrumentationBindingElement());
        endpoint.Binding = newBinding;
    }

    public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
    {
    }

    public void Validate(ServiceEndpoint endpoint)
    {
    }
}
