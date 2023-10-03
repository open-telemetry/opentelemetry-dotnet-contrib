// <copyright file="TelemetryServiceBehavior.cs" company="OpenTelemetry Authors">
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

#if NETFRAMEWORK
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Wcf;

/// <summary>
/// An <see cref="IServiceBehavior"/> implementation to add the telemetry to service operations.
/// </summary>
public class TelemetryServiceBehavior : IServiceBehavior
{
    /// <inheritdoc />
    public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, System.Collections.ObjectModel.Collection<ServiceEndpoint> endpoints, System.ServiceModel.Channels.BindingParameterCollection bindingParameters)
    {
    }

    /// <inheritdoc/>
    public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
    {
        Guard.ThrowIfNull(serviceHostBase);

        foreach (var channelDispatcherBase in serviceHostBase.ChannelDispatchers)
        {
            var channelDispatcher = (ChannelDispatcher)channelDispatcherBase;
            foreach (var endpointDispatcher in channelDispatcher.Endpoints)
            {
                TelemetryEndpointBehavior.ApplyDispatchBehaviorToEndpoint(endpointDispatcher);
            }
        }
    }

    /// <inheritdoc/>
    public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
    {
    }
}
#endif
