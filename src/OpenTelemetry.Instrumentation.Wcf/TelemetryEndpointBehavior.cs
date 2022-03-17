// <copyright file="TelemetryEndpointBehavior.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace OpenTelemetry.Instrumentation.Wcf
{
#if NETFRAMEWORK
    /// <summary>
    /// An <see cref="IEndpointBehavior"/> implementation which adds the <see
    /// cref="TelemetryClientMessageInspector"/> to client endpoints and the
    /// <see cref="TelemetryDispatchMessageInspector"/> to service endpoints.
    /// </summary>
#else
    /// <summary>
    /// An <see cref="IEndpointBehavior"/> implementation which adds the <see
    /// cref="TelemetryClientMessageInspector"/> to client endpoints.
    /// </summary>
#endif
    public class TelemetryEndpointBehavior : IEndpointBehavior
    {
        /// <inheritdoc/>
        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        /// <inheritdoc/>
        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            ApplyClientBehaviorToClientRuntime(clientRuntime);
        }

        /// <inheritdoc/>
        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
#if NETFRAMEWORK
            ApplyDispatchBehaviorToEndpoint(endpointDispatcher);
#endif
        }

        /// <inheritdoc/>
        public void Validate(ServiceEndpoint endpoint)
        {
        }

        internal static void ApplyClientBehaviorToClientRuntime(ClientRuntime clientRuntime)
        {
            var actionMappings = new Dictionary<string, ActionMetadata>(StringComparer.OrdinalIgnoreCase);

            foreach (var clientOperation in clientRuntime.ClientOperations)
            {
                actionMappings[clientOperation.Action] = new ActionMetadata
                {
                    ContractName = $"{clientRuntime.ContractNamespace}{clientRuntime.ContractName}",
                    OperationName = clientOperation.Name,
                };
            }

            clientRuntime.ClientMessageInspectors.Add(new TelemetryClientMessageInspector(actionMappings));
        }

#if NETFRAMEWORK
        internal static void ApplyDispatchBehaviorToEndpoint(EndpointDispatcher endpointDispatcher)
        {
            var actionMappings = new Dictionary<string, ActionMetadata>(StringComparer.OrdinalIgnoreCase);

            foreach (var dispatchOperation in endpointDispatcher.DispatchRuntime.Operations)
            {
                actionMappings[dispatchOperation.Action] = new ActionMetadata
                {
                    ContractName = $"{endpointDispatcher.ContractNamespace}{endpointDispatcher.ContractName}",
                    OperationName = dispatchOperation.Name,
                };
            }

            endpointDispatcher.DispatchRuntime.MessageInspectors.Add(new TelemetryDispatchMessageInspector(actionMappings));
        }
#endif
    }
}
