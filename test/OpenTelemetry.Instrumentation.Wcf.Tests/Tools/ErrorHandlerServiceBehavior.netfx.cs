// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Collections.ObjectModel;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace OpenTelemetry.Instrumentation.Wcf.Tests.Tools;

internal class ErrorHandlerServiceBehavior(EventWaitHandle handle, Action<Exception> action) : IServiceBehavior
{
    public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters)
    {
    }

    public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
    {
        foreach (var dispatcher in serviceHostBase.ChannelDispatchers.Cast<ChannelDispatcher>())
        {
            dispatcher.ErrorHandlers.Add(new ErrorHandler(handle, action));
        }
    }

    public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
    {
    }
}

#endif
